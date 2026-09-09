using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;
using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.Responses;
using Microsoft.AspNetCore.TestHost;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     BDD-style coverage of DRK-1164 §5, "Default recent-activity window for audited records": a bare
///     <c>MapGetList</c> request over a type that carries audit timestamps is limited to the host's default
///     recent-activity window (three months unless reconfigured), the caller can replace that window with
///     <c>fromDate</c>/<c>toDate</c>, and the window is ignored — never refused — for a type that carries none.
///     Paging/ceiling defaults have their own suite (<see cref="MapGetListPagingTests" />,
///     <see cref="MapGetListPageSizeCeilingTests" />); this class owns the time dimension.
/// </summary>
public class MapGetListActivityWindowTests
{
    #region Methods

    // --- The default window ----------------------------------------------------------------------------------

    [Fact]
    public async Task BareRequest_OneOrderRecentOneOrderOld_ReturnsOnlyTheRecentOrder()
    {
        var now = DateTimeOffset.UtcNow;
        var recentId = Guid.NewGuid();
        var oldId = Guid.NewGuid();

        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            var recent = new OrderEntity(recentId, "recent", "Open");
            recent.Seed(now.AddDays(-10));
            var old = new OrderEntity(oldId, "old", "Open");
            old.Seed(now.AddYears(-2));
            db.Orders.AddRange(recent, old);
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Reference.ShouldBe("recent");
    }

    [Fact]
    public async Task OrderCreated2YearsAgoUpdated5DaysAgo_BareRequest_IsStillReturned()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            var order = new OrderEntity(Guid.NewGuid(), "edited-old", "Open");
            order.Seed(now.AddYears(-2), now.AddDays(-5));
            db.Orders.Add(order);
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Reference).ShouldBe(["edited-old"]);
    }

    // --- The caller's own bounds replace the default ----------------------------------------------------------

    [Fact]
    public async Task OrderCreated2YearsAgo_FromDateThreeYearsAgo_IsReturned()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            var order = new OrderEntity(Guid.NewGuid(), "ancient", "Open");
            order.Seed(now.AddYears(-2));
            db.Orders.Add(order);
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync($"/p/orders?fromDate={Uri.EscapeDataString($"{now.AddYears(-3):O}")}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Reference).ShouldBe(["ancient"]);
    }

    [Fact]
    public async Task TwoOrders_ToDateOneYearAgo_ReturnsOnlyTheOlderOrder()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            var recent = new OrderEntity(Guid.NewGuid(), "recent", "Open");
            recent.Seed(now.AddDays(-10));
            var old = new OrderEntity(Guid.NewGuid(), "old", "Open");
            old.Seed(now.AddYears(-2));
            db.Orders.AddRange(recent, old);
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync($"/p/orders?toDate={Uri.EscapeDataString($"{now.AddYears(-1):O}")}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Reference).ShouldBe(["old"]);
    }

    [Fact]
    public async Task TwoOrders_FromDateAtEarliestRepresentableMoment_ReturnsBoth()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            var old = new OrderEntity(Guid.NewGuid(), "old", "Open");
            old.Seed(now.AddYears(-2));
            var recent = new OrderEntity(Guid.NewGuid(), "recent", "Open");
            recent.Seed(now.AddDays(-10));
            db.Orders.AddRange(old, recent);
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync(
            $"/p/orders?fromDate={Uri.EscapeDataString($"{DateTimeOffset.MinValue:O}")}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(2);
    }

    [Fact]
    public async Task OrderAtBoundaryInstant_FromDateEqualsToDateEqualsThatInstant_IsReturned()
    {
        // R4's bounds are inclusive on both sides; fromDate == toDate must still match a row created exactly then.
        var instant = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            var order = new OrderEntity(Guid.NewGuid(), "on-the-instant", "Open");
            order.Seed(instant);
            db.Orders.Add(order);
        });
        using var client = app.GetTestClient();

        var boundary = Uri.EscapeDataString($"{instant:O}");
        var response = await client.GetAsync($"/p/orders?fromDate={boundary}&toDate={boundary}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Reference).ShouldBe(["on-the-instant"]);
    }

    // --- An impossible window is refused, not answered empty -------------------------------------------------

    [Fact]
    public async Task Orders_FromDateLaterThanToDate_Returns400NamingBothBounds()
    {
        await using var app = await ActivityWindowTestHost.BuildAsync(_ => { });
        using var client = app.GetTestClient();
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var response = await client.GetAsync(
            $"/p/orders?fromDate={Uri.EscapeDataString($"{from:O}")}&toDate={Uri.EscapeDataString($"{to:O}")}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadAsStringAsync();
        problem.ShouldContain("2026-09-01");
        problem.ShouldContain("2026-06-01");
    }

    // R1 runs before R2: the impossible-window rejection fires even for a type with no audit timestamps at all.
    [Fact]
    public async Task Widgets_FromDateLaterThanToDate_Returns400NamingBothBoundsBeforeTheAuditCheck()
    {
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
            db.Widgets.Add(new WidgetEntity(Guid.NewGuid(), "widget-1")));
        using var client = app.GetTestClient();
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var response = await client.GetAsync(
            $"/p/widgets?fromDate={Uri.EscapeDataString($"{from:O}")}&toDate={Uri.EscapeDataString($"{to:O}")}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadAsStringAsync();
        problem.ShouldContain("2026-09-01");
        problem.ShouldContain("2026-06-01");
    }

    // --- Bounds are ignored, never refused, where there are no audit timestamps at all ------------------------

    [Fact]
    public async Task Widget_FromDateOneDayAgo_SucceedsAndReturnsTheWidgetRegardless()
    {
        var now = DateTimeOffset.UtcNow;
        var widgetId = Guid.NewGuid();
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
            db.Widgets.Add(new WidgetEntity(widgetId, "ancient-widget")));
        using var client = app.GetTestClient();

        var response = await client.GetAsync(
            $"/p/widgets?fromDate={Uri.EscapeDataString($"{now.AddDays(-1):O}")}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Id).ShouldBe([widgetId]);
    }

    // --- Host configuration: lengthen or switch off the window ------------------------------------------------

    [Fact]
    public async Task HostSetsTwentyFourMonthWindow_OrderCreatedOneYearAgo_IsReturned()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(
            db =>
            {
                var order = new OrderEntity(Guid.NewGuid(), "year-old", "Open");
                order.Seed(now.AddYears(-1));
                db.Orders.Add(order);
            },
            builder => builder.Services.AddListQueryOptions(o => o.DefaultActivityWindowMonths = 24));
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Reference).ShouldBe(["year-old"]);
    }

    [Fact]
    public async Task HostSwitchesWindowOff_OrderCreatedFiveYearsAgo_IsReturned()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(
            db =>
            {
                var order = new OrderEntity(Guid.NewGuid(), "ancient", "Open");
                order.Seed(now.AddYears(-5));
                db.Orders.Add(order);
            },
            builder => builder.Services.AddListQueryOptions(o => o.DefaultActivityWindowMonths = 0));
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Reference).ShouldBe(["ancient"]);
    }

    [Fact]
    public async Task HostSwitchesWindowOff_ButCallerSuppliesExplicitBounds_BoundsStillApply()
    {
        // DefaultActivityWindowMonths = 0 only turns off the *default*; a caller's own fromDate/toDate always
        // apply (R3) regardless of the host's configured default.
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(
            db =>
            {
                var recent = new OrderEntity(Guid.NewGuid(), "recent", "Open");
                recent.Seed(now.AddDays(-10));
                var old = new OrderEntity(Guid.NewGuid(), "old", "Open");
                old.Seed(now.AddYears(-2));
                db.Orders.AddRange(recent, old);
            },
            builder => builder.Services.AddListQueryOptions(o => o.DefaultActivityWindowMonths = 0));
        using var client = app.GetTestClient();

        var response = await client.GetAsync(
            $"/p/orders?fromDate={Uri.EscapeDataString($"{now.AddDays(-30):O}")}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Reference).ShouldBe(["recent"]);
    }

    // --- The total narrows to match, and the window combines with other conditions -----------------------------

    [Fact]
    public async Task FiveRecentOrders_FiveHundredOldOrders_BareRequest_ReportedTotalIsFive()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            for (var i = 0; i < 5; i++)
            {
                var order = new OrderEntity(Guid.NewGuid(), $"recent-{i}", "Open");
                order.Seed(now.AddDays(-i));
                db.Orders.Add(order);
            }

            for (var i = 0; i < 500; i++)
            {
                var order = new OrderEntity(Guid.NewGuid(), $"old-{i}", "Open");
                order.Seed(now.AddYears(-2));
                db.Orders.Add(order);
            }
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/orders?pageSize=1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(5);
    }

    [Fact]
    public async Task CancelledAndOpenOrders_FilteredToOpenNoDateBound_ReturnsOnlyTheRecentOpenOrder()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            var cancelledRecent = new OrderEntity(Guid.NewGuid(), "cancelled-recent", "Cancelled");
            cancelledRecent.Seed(now.AddDays(-10));
            var openRecent = new OrderEntity(Guid.NewGuid(), "open-recent", "Open");
            openRecent.Seed(now.AddDays(-10));
            var openOld = new OrderEntity(Guid.NewGuid(), "open-old", "Open");
            openOld.Seed(now.AddYears(-2));
            db.Orders.AddRange(cancelledRecent, openRecent, openOld);
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/orders?filter=status:Equal:Open");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Reference).ShouldBe(["open-recent"]);
    }

    [Fact]
    public async Task InvoiceCreated10DaysAgoAnd2YearsAgo_BareRequest_ReturnsOnlyTheRecentInvoice()
    {
        // InvoiceEntity implements IAuditedProperties directly (not via AuditedEntity), so it sits outside the
        // narrower IAuditedEntity<TKey> set the existing newest-first ordering recognises — R2 must still key
        // the window on IAuditedProperties alone.
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            db.Invoices.Add(new InvoiceEntity(Guid.NewGuid(), "recent-invoice", now.AddDays(-10)));
            db.Invoices.Add(new InvoiceEntity(Guid.NewGuid(), "old-invoice", now.AddYears(-2)));
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/invoices");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<InvoiceModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Number).ShouldBe(["recent-invoice"]);
    }

    [Fact]
    public async Task OrdersCreated2YearsAgoAnd10DaysAgo_FilterCreatedAfterThreeYearsAgoNoDateBound_ReturnsOnlyTheRecentOrder()
    {
        // A caller's own timestamp filter (not fromDate/toDate) narrows *within* the default window rather than
        // replacing it — only naming fromDate/toDate itself replaces the default (R3, R5).
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            var old = new OrderEntity(Guid.NewGuid(), "old", "Open");
            old.Seed(now.AddYears(-2));
            var recent = new OrderEntity(Guid.NewGuid(), "recent", "Open");
            recent.Seed(now.AddDays(-10));
            db.Orders.AddRange(old, recent);
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync(
            $"/p/orders?filter=createdOn:GreaterThan:{Uri.EscapeDataString($"{now.AddYears(-3):O}")}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Reference).ShouldBe(["recent"]);
    }

    [Fact]
    public async Task ThreeOrdersFiveTenAndTwentyDaysOld_BareRequest_ReturnsAllNewestCreatedFirst()
    {
        var now = DateTimeOffset.UtcNow;
        await using var app = await ActivityWindowTestHost.BuildAsync(db =>
        {
            var five = new OrderEntity(Guid.NewGuid(), "five-days", "Open");
            five.Seed(now.AddDays(-5));
            var ten = new OrderEntity(Guid.NewGuid(), "ten-days", "Open");
            ten.Seed(now.AddDays(-10));
            var twenty = new OrderEntity(Guid.NewGuid(), "twenty-days", "Open");
            twenty.Seed(now.AddDays(-20));
            db.Orders.AddRange(twenty, five, ten);
        });
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/p/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OrderModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Reference).ShouldBe(["five-days", "ten-days", "twenty-days"]);
    }

    // --- Published parameter documentation is self-describing (§7 addition) ------------------------------------

    [Fact]
    public async Task OpenApiDocument_FromDateAndToDateParameters_DescribeTheDefaultWindowAndTheirOwnReplacement()
    {
        await using var app = await ActivityWindowTestHost.BuildAsync(_ => { });
        using var client = app.GetTestClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var parameters = document.RootElement
            .GetProperty("paths").GetProperty("/p/orders").GetProperty("get").GetProperty("parameters");

        var fromDate = parameters.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "fromDate");
        var toDate = parameters.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "toDate");

        var fromDateDescription = fromDate.GetProperty("description").GetString();
        fromDateDescription.ShouldNotBeNull();
        fromDateDescription.ShouldContain("3 months");
        fromDateDescription.ShouldContain("0001-01-01T00:00:00Z");

        var toDateDescription = toDate.GetProperty("description").GetString();
        toDateDescription.ShouldNotBeNull();
        toDateDescription.ShouldContain("replaces");
    }

    #endregion
}
