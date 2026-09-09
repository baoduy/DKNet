using System.Net;
using System.Net.Http.Json;
using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.Responses;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Specifications;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     BDD-style coverage of DRK-1166 "Default page size and default activity window" §7 — the
///     <c>fromDate</c>/<c>toDate</c> query parameters and the host-configurable default recent-activity window
///     they replace, exercised through a real HTTP pipeline against a real EF Core InMemory store. Each fact
///     builds its own throwaway host (mirroring <see cref="MapGetListPageSizeCeilingTests" />'s
///     <c>BuildHostAsync</c>) so a scenario's seed and configuration never leak into another's assertions.
/// </summary>
public sealed class MapGetListActivityWindowTests
{
    #region Methods

    // Given: an order created 10 days ago and one created 2 years ago, neither ever updated.
    // When: the order list is requested without naming any date bound.
    // Then: only the 10-day-old order — inside the default 3-month window — comes back.
    [Fact]
    public async Task BareRequest_CoversTheLastThreeMonths()
    {
        var now = DateTimeOffset.UtcNow;
        var recent = Guid.NewGuid();
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            null,
            db => db.Orders.AddRange(
                new OrderEntity(recent, now.AddDays(-10)),
                new OrderEntity(Guid.NewGuid(), now.AddYears(-2))),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/x/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(recent);
    }

    // Given: an order created 2 years ago but updated 5 days ago.
    // When: the order list is requested without naming any date bound.
    // Then: it still matches — R4's OR means a recent update rescues an old creation.
    [Fact]
    public async Task OldRecordEditedRecently_IsStillRecentActivity()
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            null,
            db => db.Orders.Add(new OrderEntity(id, now.AddYears(-2), now.AddDays(-5))),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/x/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(id);
    }

    // Given: an order created 10 days ago and updated 10 days ago — both comfortably inside any bound below.
    // When: the caller supplies only a toDate of 1 day ago, leaving the lower side open.
    // Then: the order still matches on CreatedOn alone, proving a single supplied bound never demands both
    // timestamps satisfy it.
    [Fact]
    public async Task RecentRecord_WithOnlyToDateSupplied_StillMatches()
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            null,
            db => db.Orders.Add(new OrderEntity(id, now.AddDays(-10), now.AddDays(-10))),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync($"/x/orders?toDate={EscapeDate(now.AddDays(-1))}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(id);
    }

    // Given: an order created 2 years ago and never updated.
    // When: the caller supplies fromDate=3 years ago.
    // Then: the caller's own bound replaces the host default entirely (R3) and the order matches.
    [Fact]
    public async Task CallersOwnFromDate_ReplacesTheDefaultWindowEntirely()
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            null,
            db => db.Orders.Add(new OrderEntity(id, now.AddYears(-2))),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync($"/x/orders?fromDate={EscapeDate(now.AddYears(-3))}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(id);
    }

    // Given: an order created 10 days ago and one created 2 years ago, neither ever updated.
    // When: the caller supplies only toDate=1 year ago.
    // Then: the lower side stays open, but the recent order is excluded and only the 2-year-old one matches.
    [Fact]
    public async Task ToDateOnly_LeavesTheLowerSideOpen()
    {
        var now = DateTimeOffset.UtcNow;
        var old = Guid.NewGuid();
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            null,
            db => db.Orders.AddRange(
                new OrderEntity(Guid.NewGuid(), now.AddDays(-10)),
                new OrderEntity(old, now.AddYears(-2))),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync($"/x/orders?toDate={EscapeDate(now.AddYears(-1))}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(old);
    }

    // Given: an order created 2 years ago and one created 10 days ago, neither ever updated.
    // When: the caller supplies fromDate=DateTimeOffset.MinValue.
    // Then: both orders — all of history — are reachable.
    [Fact]
    public async Task FromDateAtMinValue_ReachesAllHistory()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            null,
            db => db.Orders.AddRange(
                new OrderEntity(Guid.NewGuid(), now.AddYears(-2)),
                new OrderEntity(Guid.NewGuid(), now.AddDays(-10))),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync($"/x/orders?fromDate={EscapeDate(DateTimeOffset.MinValue)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(2);
    }

    // Given: no seeded data of consequence — the request must be refused before it ever reaches the database.
    // When: the caller supplies a fromDate later than toDate.
    // Then: the request is refused with 400 naming both bounds, never answered with an empty page (R5).
    [Fact]
    public async Task FromDateAfterToDate_IsRefusedNotAnsweredEmpty()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(null, _ => { }, "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync(
            $"/x/orders?fromDate={EscapeDate(now)}&toDate={EscapeDate(now.AddDays(-1))}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadAsStringAsync();
        problem.ShouldContain("fromDate");
        problem.ShouldContain("toDate");
    }

    // Given: a host configured with a 24-month default window, and an order created 1 year ago.
    // When: the order list is requested without naming any date bound.
    // Then: the order matches — the host lengthened the window past the built-in 3 months.
    [Fact]
    public async Task HostMayLengthenTheDefaultWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            o => o.DefaultActivityWindowMonths = 24,
            db => db.Orders.Add(new OrderEntity(id, now.AddYears(-1))),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/x/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(id);
    }

    // Given: a host configured with DefaultActivityWindowMonths=0, and an order created 5 years ago.
    // When: the order list is requested without naming any date bound.
    // Then: the order matches — the host switched the window off, so a bare request lists all history (R12).
    [Fact]
    public async Task HostMaySwitchTheDefaultWindowOff()
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            o => o.DefaultActivityWindowMonths = 0,
            db => db.Orders.Add(new OrderEntity(id, now.AddYears(-5))),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/x/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(id);
    }

    // Given: a widget record — a listed type that carries no audit timestamps at all.
    // When: the caller supplies a fromDate.
    // Then: the request succeeds and the bound is ignored, not refused (R7).
    [Fact]
    public async Task DateBounds_AreIgnoredNotRefused_ForRecordsWithoutAuditTimestamps()
    {
        var id = Guid.NewGuid();
        await using var app = await BuildHostAsync<WidgetEntity, WidgetModel>(
            null,
            db => db.Widgets.Add(new WidgetEntity(id, "widget-no-audit")),
            "/widgets");
        using var client = app.GetTestClient();

        var response = await client.GetAsync(
            $"/x/widgets?fromDate={EscapeDate(DateTimeOffset.UtcNow.AddDays(-1))}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.Items.Single().Id.ShouldBe(id);
    }

    // Given: 5 orders created within the last month and 500 orders created 2 years ago.
    // When: the order list is requested without naming any date bound.
    // Then: the reported total narrows to 5 — the window is part of the specification's filter, so the count
    // and the records always agree (R8).
    [Fact]
    public async Task WindowNarrowsTheReportedTotalToMatch()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            null,
            db =>
            {
                for (var i = 1; i <= 5; i++) db.Orders.Add(new OrderEntity(Guid.NewGuid(), now.AddDays(-i)));
                for (var i = 1; i <= 500; i++) db.Orders.Add(new OrderEntity(Guid.NewGuid(), now.AddYears(-2)));
            },
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/x/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(5);
    }

    // Given: a cancelled order and an open order both created 10 days ago, and an open order created 2 years ago.
    // When: the list is requested filtered to open orders, naming no date bound.
    // Then: only the recent open order matches — the window ANDs with the caller's filter (R9), narrowing it
    // rather than replacing it.
    [Fact]
    public async Task WindowCombinesWithTheCallersOwnFilter()
    {
        var now = DateTimeOffset.UtcNow;
        var openRecent = Guid.NewGuid();
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            null,
            db => db.Orders.AddRange(
                new OrderEntity(Guid.NewGuid(), now.AddDays(-10), status: "Cancelled"),
                new OrderEntity(openRecent, now.AddDays(-10), status: "Open"),
                new OrderEntity(Guid.NewGuid(), now.AddYears(-2), status: "Open")),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/x/orders?filter=status:Equal:Open");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(openRecent);
    }

    // Given: an order created 2 years ago and one created 10 days ago, neither ever updated.
    // When: the list is requested filtered to orders created after 3 years ago, naming no date bound.
    // Then: only the 10-day-old order matches — the caller's own timestamp filter narrows within the default
    // window rather than replacing it.
    [Fact]
    public async Task CallersOwnTimestampFilter_NarrowsWithinTheWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var recent = Guid.NewGuid();
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            null,
            db => db.Orders.AddRange(
                new OrderEntity(Guid.NewGuid(), now.AddYears(-2)),
                new OrderEntity(recent, now.AddDays(-10))),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync(
            $"/x/orders?filter=createdOn:GreaterThan:{EscapeDate(now.AddYears(-3))}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(recent);
    }

    // Given: three orders created 5, 10 and 20 days ago, none updated — all inside the default window.
    // When: the order list is requested without naming any date bound.
    // Then: all three come back, newest created first — the window narrows rows, never reorders them (R10).
    [Fact]
    public async Task OrderingIsUnchangedByTheWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var newest = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var oldest = Guid.NewGuid();
        await using var app = await BuildHostAsync<OrderEntity, OrderModel>(
            null,
            db => db.Orders.AddRange(
                new OrderEntity(newest, now.AddDays(-5)),
                new OrderEntity(middle, now.AddDays(-10)),
                new OrderEntity(oldest, now.AddDays(-20))),
            "/orders");
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/x/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Id).ShouldBe([newest, middle, oldest]);
    }

    // Given: a listed type (TicketEntity) implementing IEntity<Guid> and IAuditedProperties directly.
    // When: the test inspects it via reflection.
    // Then: it is NOT assignable to IAuditedEntity<Guid> — proving the scenario below actually exercises R6's
    // wider recognition rule rather than the narrower one the ordering default uses.
    [Fact]
    public void TicketEntity_ImplementsIAuditedPropertiesDirectly_ButIsNotAnIAuditedEntity()
    {
        typeof(IAuditedProperties).IsAssignableFrom(typeof(TicketEntity)).ShouldBeTrue();
        typeof(IAuditedEntity<Guid>).IsAssignableFrom(typeof(TicketEntity)).ShouldBeFalse();
    }

    // Given: a listed type carrying audit timestamps but not assignable to IAuditedEntity<TKey> (see the
    // reflection fact above), with one record created 10 days ago and one created 2 years ago.
    // When: its list endpoint is requested without naming any date bound.
    // Then: only the 10-day-old record matches — the window recognises it despite the narrower ordering check
    // never would (R6, required by the spec, not optional).
    [Fact]
    public async Task WiderRecognitionRule_WindowsATypeNotAssignableToIAuditedEntity()
    {
        var now = DateTimeOffset.UtcNow;
        var recent = Guid.NewGuid();
        await using var app = await BuildHostAsync<TicketEntity, TicketModel>(
            null,
            db => db.Tickets.AddRange(
                new TicketEntity { Id = recent, CreatedOn = now.AddDays(-10) },
                new TicketEntity { Id = Guid.NewGuid(), CreatedOn = now.AddYears(-2) }),
            "/tickets");
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/x/tickets");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<TicketModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(recent);
    }

    /// <summary>URL-encodes an instant for use as a <c>fromDate</c>/<c>toDate</c> query value.</summary>
    private static string EscapeDate(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O"));

    /// <summary>
    ///     Builds and starts a throwaway <c>MapGetList</c> host over <see cref="WidgetDbContext" />, mirroring
    ///     <see cref="MapGetListPageSizeCeilingTests" />'s <c>BuildHostAsync</c>.
    /// </summary>
    /// <typeparam name="TEntity">Entity type the endpoint lists.</typeparam>
    /// <typeparam name="TModel">Model type each entity is projected to.</typeparam>
    /// <param name="configureOptions">Configures <see cref="ListQueryOptions" />; <see langword="null" /> for defaults.</param>
    /// <param name="seed">Seeds the store before the host starts.</param>
    /// <param name="endpoint">The list endpoint's route, mapped under <c>/x</c>.</param>
    private static async Task<WebApplication> BuildHostAsync<TEntity, TModel>(
        Action<ListQueryOptions>? configureOptions,
        Action<WidgetDbContext> seed,
        string endpoint)
        where TEntity : class, IEntity<Guid>
        where TModel : class
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
        var dbName = Guid.NewGuid().ToString("N");
        builder.Services.AddDbContext<WidgetDbContext>(o => o.UseInMemoryDatabase(dbName));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<WidgetDbContext>());
        builder.Services.AddSpecRepo<WidgetDbContext>();
        if (configureOptions is not null) builder.Services.AddListQueryOptions(configureOptions);

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WidgetDbContext>();
            seed(db);
            await db.SaveChangesAsync();
        }

        app.MapGroup("/x").MapGetList<TEntity, TModel>(endpoint);

        await app.StartAsync();
        return app;
    }

    #endregion
}
