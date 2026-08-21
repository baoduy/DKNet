using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;
using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.Responses;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     BDD-style coverage of the <c>MapGetList</c> paging-hardening feature (DRK-519 §5, "Standard list endpoint
///     paging"): default page/size, ceiling/floor clamps, the published ceiling description, newest-first
///     ordering (audited and non-audited), lossless page-walking, and next/previous flags — exercised through a
///     real HTTP pipeline against a real EF Core InMemory store (see <see cref="PagingTestHost" />).
/// </summary>
public class MapGetListPagingTests(PagingTestHost host) : IClassFixture<PagingTestHost>
{
    #region Methods

    // Given: a bare list request with no query parameters at all (the DRK-513 reversal — this used to 400).
    // When: the request hits MapGetList.
    // Then: it succeeds and serves page 1 at the default size of 20.
    [Fact]
    public async Task BareRequest_NoQueryParams_ReturnsPageOneOfTwenty()
    {
        var response = await host.Client.GetAsync("/p/widgets");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.PageNumber.ShouldBe(1);
        page.PageSize.ShouldBe(20);
        page.Items.Count.ShouldBe(20);
    }

    // Given: a page size far above the published ceiling.
    // When: the request asks for pageSize=100000.
    // Then: the server clamps it down to 100 and actually serves 100 rows (not just echoes the clamp back).
    [Fact]
    public async Task PageSizeAboveCeiling_ClampsToOneHundred()
    {
        var response = await host.Client.GetAsync("/p/widgets?pageSize=100000");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.PageSize.ShouldBe(100);
        page.Items.Count.ShouldBe(100);
    }

    // Given: a page size below the minimum of one.
    // When: the request asks for pageSize=0 or a negative value.
    // Then: the server falls back to the default of 20 rather than returning an empty or error response.
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task PageSizeBelowOne_FallsBackToTwenty(int requestedPageSize)
    {
        var response = await host.Client.GetAsync($"/p/widgets?pageSize={requestedPageSize}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.PageSize.ShouldBe(20);
        page.Items.Count.ShouldBe(20);
    }

    // Given: the published OpenAPI description for the pageSize parameter on a MapGetList endpoint.
    // When: a client (or client-generator) reads the API description alone, without ever calling the endpoint.
    // Then: the 100-row ceiling is discoverable from that description text.
    [Fact]
    public async Task PageSizeCeiling_IsDiscoverableInThePublishedApiDescription()
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
        description.ShouldContain("100");
    }

    // Given: an audited entity where two rows share the same CreatedOn instant and one row is strictly newer.
    // When: the list endpoint serves the default (newest-first) ordering.
    // Then: the strictly-newer row leads, and the CreatedOn tie is broken by Id (higher Id first).
    [Fact]
    public async Task AuditedEntity_OrdersByCreatedOnDescendingThenIdDescending()
    {
        var response = await host.Client.GetAsync("/p/gadgets?pageSize=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<GadgetModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Name).ShouldBe(["gadget-newest", "gadget-tie-high", "gadget-tie-low"]);
    }

    // Given: 205 seeded rows and a page size of 20 (11 pages, last page holding the 5 remainder rows).
    // When: every page from 1 through PageCount is walked in order.
    // Then: every seeded row is visited exactly once — no duplicates, no skips.
    [Fact]
    public async Task WalkingEveryPage_VisitsEverySeededRowExactlyOnce()
    {
        var first = await host.Client.GetFromJsonAsync<PagedResponse<WidgetModel>>("/p/widgets?pageNumber=1&pageSize=20");
        first.ShouldNotBeNull();
        var pageCount = first.PageCount;

        var visitedIds = new List<Guid>();
        for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            var page = await host.Client.GetFromJsonAsync<PagedResponse<WidgetModel>>(
                $"/p/widgets?pageNumber={pageNumber}&pageSize=20");
            page.ShouldNotBeNull();
            visitedIds.AddRange(page.Items.Select(i => i.Id));
        }

        visitedIds.Count.ShouldBe(PagingTestHost.SeededWidgetCount);
        visitedIds.Distinct().Count().ShouldBe(PagingTestHost.SeededWidgetCount);
        visitedIds.ToHashSet().SetEquals(host.SeededWidgetIds).ShouldBeTrue();
    }

    // Given: 205 seeded rows at page size 20 (11 pages total).
    // When: a middle page (5 of 11) is requested.
    // Then: both a next page and a previous page are reported.
    [Fact]
    public async Task MiddlePage_ReportsBothNextAndPreviousPage()
    {
        var response = await host.Client.GetAsync("/p/widgets?pageNumber=5&pageSize=20");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.HasNextPage.ShouldBeTrue();
        page.HasPreviousPage.ShouldBeTrue();
    }

    // Given: 205 seeded rows at page size 20 (11 pages total, the last holding the 5-row remainder).
    // When: the last page (11 of 11) is requested.
    // Then: no next page is reported, but a previous page is.
    [Fact]
    public async Task LastPage_ReportsNoNextPageButReportsPreviousPage()
    {
        var response = await host.Client.GetAsync("/p/widgets?pageNumber=11&pageSize=20");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.PageCount.ShouldBe(11);
        page.Items.Count.ShouldBe(5);
        page.HasNextPage.ShouldBeFalse();
        page.HasPreviousPage.ShouldBeTrue();
    }

    #endregion
}
