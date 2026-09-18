using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Specifications;
using DKNet.SlimBus.Extensions;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Shouldly;
using SlimBus.Generators.Tests.Api.Crud;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     Declares "accounts.write" for POST above the group and maps only the generated Create route (DRK-1542 §5
///     "A generated CRUD route is covered by the group declaration") — every other <see cref="CrudOp" /> is
///     excluded so this group's only served method is the one method the declaration covers.
/// </summary>
[EndpointGroupScopeAttribute("accounts.write", EndpointHttpMethods.Post)]
public sealed class ScopedGadgetCreateEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/scoped-gadgets";

    public void Map(RouteGroupBuilder group) =>
        group.MapGadgetCrud(o => o.Exclude(CrudOp.GetById, CrudOp.GetList, CrudOp.Update, CrudOp.Delete, CrudOp.Action));
}

/// <summary>
///     Real minimal-API host wiring <see cref="ScopedGadgetCreateEndpointConfig" /> through
///     <c>WebApplication.UseEndpointConfigs</c> — proves the group-scope declaration reaches a route registered by
///     generated CRUD, not just a hand-mapped one. Uses <see cref="ScopedTestAuthHandler" />'s scope-header
///     pattern from <see cref="GadgetAuthTestHost" />.
/// </summary>
public sealed class GroupScopeCrudTestHost : IAsyncLifetime, IDisposable
{
    private WebApplication? _app;
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    public void Dispose() => _connection?.Dispose();

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
        builder.Services.AddDbContext<GadgetDbContext>(o => o.UseSqlite(_connection));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<GadgetDbContext>());
        builder.Services.AddSpecRepo<GadgetDbContext>();

        builder.Services
            .AddSlimBusEfCoreInterceptor<GadgetDbContext>()
            .AddSlimMessageBus(mbb => mbb
                .AddJsonSerializer()
                .AddServicesFromAssembly(typeof(GroupScopeCrudTestHost).Assembly)
                .AddChildBus(
                    "Memory",
                    mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(GroupScopeCrudTestHost).Assembly)));

        builder.Services
            .AddAuthentication(ScopedTestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopedTestAuthHandler>(ScopedTestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("accounts.write", p => p.RequireClaim("scope", "accounts.write"));

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GadgetDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // No AddApiVersioning() call in this minimal host — EnableVersioning stays off so
        // UseEndpointConfigs's fail-fast versioning guard never fires.
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(GroupScopeCrudTestHost).Assembly);

        await app.StartAsync();
        _app = app;
        Client = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_connection is not null) await _connection.DisposeAsync();
    }
}

/// <summary>DRK-1542 §5 scenario "A generated CRUD route is covered by the group declaration" (@integration).</summary>
public sealed class GroupScopeCrudTests(GroupScopeCrudTestHost host) : IClassFixture<GroupScopeCrudTestHost>
{
    [Fact]
    public async Task Scenario3_GeneratedCreateRouteCoveredByGroupDeclaration_TreasuryOpsHoldingAccountsWriteSucceeds()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/scoped-gadgets")
        {
            Content = JsonContent.Create(new { name = "g", price = 1m })
        };
        request.Headers.Add(ScopedTestAuthHandler.UserHeader, "treasury-ops");
        request.Headers.Add(ScopedTestAuthHandler.ScopesHeader, "accounts.write");

        var response = await host.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
