using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;
using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.Responses;
using DKNet.EfCore.Specifications;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     BDD-style coverage of DRK-1149 "Configurable page-size ceiling" — the ceiling that used to be a
///     hard-coded 100 is now a host-configurable <see cref="ListQueryOptions" />, defaulting to 1000, settable
///     from configuration or in code, and validated at startup. <see cref="MapGetListPagingTests" /> keeps the
///     paging-mechanics coverage (ordering, page-walking, next/previous); this class owns the ceiling itself.
/// </summary>
public class MapGetListPageSizeCeilingTests(PagingCeilingTestHost host) : IClassFixture<PagingCeilingTestHost>
{
    #region Methods

    // Given: a host that never calls AddListQueryOptions and never binds DKNet:ListQuery (R5 — IOptions<T>
    // still resolves to the type's defaults for an app that does neither).
    // When: the caller asks for exactly the new default ceiling.
    // Then: all 1000 rows are actually served, not just echoed back (R1).
    [Fact]
    public async Task UnconfiguredHost_PageSizeAtNewDefaultCeiling_ServesOneThousand()
    {
        var response = await host.Client.GetAsync("/p/widgets?pageSize=1000");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.PageSize.ShouldBe(1000);
        page.Items.Count.ShouldBe(1000);
    }

    // Given: a page size far above the default ceiling.
    // When: the request asks for pageSize=100000.
    // Then: the ceiling clamps it down (never a 400 — R2) and the reported size is the effective ceiling.
    [Fact]
    public async Task UnconfiguredHost_PageSizeAboveCeiling_ClampsToTheDefaultCeilingWithoutRejection()
    {
        var response = await host.Client.GetAsync("/p/widgets?pageSize=100000");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.PageSize.ShouldBe(1000);
        page.Items.Count.ShouldBe(1000);
    }

    // Given: the published OpenAPI description for the pageSize parameter.
    // When: a client reads the description alone, without ever calling the endpoint.
    // Then: it no longer hard-codes a ceiling number (the ceiling is host-configurable now) but still tells the
    // caller a maximum is enforced.
    [Fact]
    public async Task PageSizeDescription_NoLongerHardCodesACeilingNumber()
    {
        var response = await host.Client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var pageSizeParam = document.RootElement
            .GetProperty("paths").GetProperty("/p/widgets").GetProperty("get").GetProperty("parameters")
            .EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "pageSize");

        var description = pageSizeParam.GetProperty("description").GetString();
        description.ShouldNotBeNull();
        description.ShouldContain("configured maximum");
        description.ShouldNotContain("100");
    }

    // Given: a host that binds DKNet:ListQuery:MaxPageSize to 2000 from configuration.
    // When: the caller requests pageSize=2000.
    // Then: all 2000 rows are served (the ceiling is raised from configuration, not just in code).
    [Fact]
    public async Task CeilingBoundFromConfiguration_RaisesTheServedCeiling()
    {
        await using var app = await BuildHostAsync(
            builder =>
            {
                builder.Configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { [$"{ListQueryOptions.ConfigSectionName}:MaxPageSize"] = "2000" });
                builder.Services.Configure<ListQueryOptions>(
                    builder.Configuration.GetSection(ListQueryOptions.ConfigSectionName));
            },
            seedWidgetCount: 2100);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/widgets?pageSize=2000");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.PageSize.ShouldBe(2000);
        page.Items.Count.ShouldBe(2000);
    }

    // Given: a host that calls AddListQueryOptions(o => o.MaxPageSize = 50).
    // When: the caller asks for pageSize=1000, far above that lowered ceiling.
    // Then: the served page is clamped down to 50, not the type's built-in default of 1000.
    [Fact]
    public async Task CeilingLoweredInCode_ClampsBelowTheBuiltInDefault()
    {
        await using var app = await BuildHostAsync(
            builder => builder.Services.AddListQueryOptions(o => o.MaxPageSize = 50),
            seedWidgetCount: 60);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/widgets?pageSize=1000");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.PageSize.ShouldBe(50);
        page.Items.Count.ShouldBe(50);
    }

    // Given: a host whose configured MaxPageSize (5) sits below its DefaultPageSize (the built-in 20).
    // When: the caller omits pageSize entirely.
    // Then: the ceiling still wins — the served page is the configured MaxPageSize, not the higher default.
    [Fact]
    public async Task CeilingBelowDefault_StillWinsWhenPageSizeIsOmitted()
    {
        await using var app = await BuildHostAsync(
            builder => builder.Services.AddListQueryOptions(o => o.MaxPageSize = 5),
            seedWidgetCount: 25);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/widgets");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.PageSize.ShouldBe(5);
        page.Items.Count.ShouldBe(5);
    }

    // Given: a host that configures MaxPageSize as 0, below the type's [Range(1, int.MaxValue)].
    // When: the host starts.
    // Then: startup fails with an options-validation error (R4) instead of letting an invalid ceiling degrade
    // a running endpoint.
    [Fact]
    public async Task InvalidConfiguredCeiling_FailsAtStartup()
    {
        async Task Act() => await BuildHostAsync(
            builder => builder.Services.AddListQueryOptions(o => o.MaxPageSize = 0),
            seedWidgetCount: 0);

        await Should.ThrowAsync<OptionsValidationException>(Act);
    }

    /// <summary>Builds and starts a throwaway <c>MapGetList</c> host with the given DI/configuration tweak.</summary>
    /// <param name="configureBuilder">Applies the scenario's configuration or DI registration before the host builds.</param>
    /// <param name="seedWidgetCount">Number of widget rows to seed before the host starts.</param>
    private static async Task<WebApplication> BuildHostAsync(
        Action<WebApplicationBuilder> configureBuilder,
        int seedWidgetCount)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
        var dbName = Guid.NewGuid().ToString("N");
        builder.Services.AddDbContext<WidgetDbContext>(o => o.UseInMemoryDatabase(dbName));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<WidgetDbContext>());
        builder.Services.AddSpecRepo<WidgetDbContext>();
        configureBuilder(builder);

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WidgetDbContext>();
            for (var i = 1; i <= seedWidgetCount; i++) db.Widgets.Add(new WidgetEntity(Guid.NewGuid(), $"widget-{i}"));
            await db.SaveChangesAsync();
        }

        app.MapGroup("/p").MapGetList<WidgetEntity, WidgetModel>("/widgets");

        await app.StartAsync();
        return app;
    }

    #endregion
}
