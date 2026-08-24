using System.Net;
using System.Net.Http.Json;
using AspCore.Extensions.Tests.Fixtures;
using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Responses;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Exercises <c>MapGetList</c>'s generic filtering and ordering — the <c>filter=property:operation:value</c>
///     and <c>orderBy</c>/<c>desc</c> query parameters — end to end through a real minimal-API pipeline over a real
///     EF Core store. Two properties matter beyond "the right rows come back":
///     <list type="bullet">
///         <item>
///             A filter the endpoint cannot honour is <b>rejected</b>. Silently dropping it would answer a
///             filtered query with the full unfiltered page, which a caller cannot distinguish from a genuine
///             "everything matched" — the worst possible failure mode for a paging endpoint.
///         </item>
///         <item>
///             Only fields the returned model declares are filterable/sortable, so making an endpoint generic
///             never widens what a caller can reach into.
///         </item>
///     </list>
///     Paging/clamping and the default newest-first ordering have their own suite
///     (see <see cref="MapGetListPagingTests" />).
/// </summary>
public class MapGetListFilteringTests(PagingTestHost host) : IClassFixture<PagingTestHost>
{
    #region Methods

    // --- Filtering: the requested predicate actually reaches the database ----------------------------------

    [Fact]
    public async Task MapGetList_ContainsFilter_ReturnsOnlyMatchingRows()
    {
        // "widget-20" is a substring of widget-20 and widget-200..205 — seven of the 205 seeded rows, so a
        // filter that silently failed open would show up as 205 rather than a plausible-looking small number.
        var response = await host.Client.GetAsync("/p/widgets?filter=name:Contains:widget-20&pageSize=100");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(7);
        page.Items.Select(i => i.Name).ShouldBe(
            ["widget-205", "widget-204", "widget-203", "widget-202", "widget-201", "widget-200", "widget-20"]);
    }

    [Fact]
    public async Task MapGetList_MultipleFilters_CombineWithAnd()
    {
        // Contains "widget-2" matches 23 rows; EndsWith "5" narrows it to widget-25 and widget-205. An OR
        // would return 45, and dropping either condition returns 23 or 21 — all three distinguishable.
        var response = await host.Client.GetAsync(
            "/p/widgets?filter=name:Contains:widget-2&filter=name:EndsWith:5&pageSize=100");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Name).OrderBy(n => n).ShouldBe(["widget-205", "widget-25"]);
    }

    [Fact]
    public async Task MapGetList_InFilterOnGuidProperty_CoercesEachValueToTheKeyType()
    {
        // In/NotIn bind through Dynamic LINQ's "@0.Contains(prop)", which needs a Guid[] — the comma-split
        // query string only yields string[], so without element-wise coercion this throws at parse time (500).
        var first = host.SeededWidgetIds[0];
        var third = host.SeededWidgetIds[2];

        var response = await host.Client.GetAsync($"/p/widgets?filter=id:In:{first},{third}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(2);
        page.Items.Select(i => i.Id).OrderBy(i => i).ShouldBe([first, third]);
    }

    [Fact]
    public async Task MapGetList_DateTimeOffsetFilter_ReturnsOnlyRowsInsideTheWindow()
    {
        // DateTimeOffset has no TypeCode, so Convert.ChangeType cannot reach it — an uncoerced string reaching
        // the parser is a 500. Two gadgets sit on the boundary instant and one a day later.
        var response = await host.Client.GetAsync(
            "/p/gadgets?filter=createdOn:GreaterThan:2026-01-01T00:00:00Z");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<GadgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(1);
        page.Items.Single().Name.ShouldBe("gadget-newest");
    }

    [Fact]
    public async Task MapGetList_SnakeCaseFieldName_ResolvesToTheModelProperty()
    {
        var response = await host.Client.GetAsync(
            "/p/gadgets?filter=created_on:LessThanOrEqual:2026-01-01T00:00:00Z");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<GadgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(2);
    }

    // --- Filtering: an unusable filter fails loudly instead of failing open -------------------------------

    [Fact]
    public async Task MapGetList_FilterOnFieldNotOnTheModel_Returns400WithTheReason()
    {
        // CreatedBy exists on the audited entity but not on GadgetModel: filtering by a column the caller
        // cannot see would turn the endpoint into an oracle for it. This rejection happens after binding, so
        // unlike a malformed filter it carries a reason the caller can act on — assert that it survives.
        var response = await host.Client.GetAsync("/p/gadgets?filter=createdBy:Equal:seed");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadAsStringAsync();
        problem.ShouldContain("createdBy");
        problem.ShouldContain(nameof(GadgetModel));
    }

    [Fact]
    public async Task MapGetList_FilterWithUnknownOperation_Returns400()
    {
        // An unrecognised operation fails ListFilter.TryParse, so minimal-API binding rejects it before the
        // handler runs. The valid operations are published on the parameter's OpenAPI description.
        var response = await host.Client.GetAsync("/p/widgets?filter=name:Frobnicate:widget-1");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MapGetList_MalformedFilter_Returns400()
    {
        // Not three colon-separated parts: also a binding-level rejection.
        var response = await host.Client.GetAsync("/p/widgets?filter=name-equals-widget-1");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MapGetList_FilterValueNotConvertibleToThePropertyType_Returns400()
    {
        // The regression that matters most: the underlying predicate builder skips a condition it cannot
        // coerce, so failing open here would answer 200 with all 205 rows.
        var response = await host.Client.GetAsync("/p/widgets?filter=id:Equal:not-a-guid");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // --- IsNull / IsNotNull: value-less operations end to end ----------------------------------------------

    [Fact]
    public async Task MapGetList_IsNotNullFilter_MatchesEveryRowWithAValue()
    {
        // The two-part form, no trailing colon — the ergonomic shape a caller will actually type.
        var response = await host.Client.GetAsync("/p/widgets?filter=name:IsNotNull&pageSize=100");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(PagingTestHost.SeededWidgetCount);
    }

    [Fact]
    public async Task MapGetList_IsNullFilterOnANonNullColumn_Returns200WithAnEmptyPage()
    {
        var response = await host.Client.GetAsync("/p/widgets?filter=name:IsNull");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(0);
    }

    // --- Abuse limits: bounded work per request -------------------------------------------------------------

    [Fact]
    public async Task MapGetList_MoreFiltersThanTheCap_Returns400WithTheLimit()
    {
        // Every condition costs reflection, parsing, and a SQL predicate; without a ceiling one request can
        // carry hundreds. 21 conditions is one past the cap.
        var query = string.Join('&', Enumerable.Range(1, 21).Select(i => $"filter=name:Contains:w{i}"));

        var response = await host.Client.GetAsync($"/p/widgets?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("20");
    }

    [Fact]
    public async Task MapGetList_ExactlyTheFilterCap_IsAccepted()
    {
        var query = string.Join('&', Enumerable.Range(1, 20).Select(i => $"filter=name:Contains:widget"));

        var response = await host.Client.GetAsync($"/p/widgets?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapGetList_SingleCharacterSearch_Returns400()
    {
        // One character ORs a LIKE '%x%' scan across every text column for almost no selectivity.
        var response = await host.Client.GetAsync("/p/widgets?search=w");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("2");
    }

    [Fact]
    public async Task MapGetList_TwoCharacterSearch_IsAccepted()
    {
        var response = await host.Client.GetAsync("/p/widgets?search=wi&pageSize=100");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(PagingTestHost.SeededWidgetCount);
    }

    // --- Free-text search across the model's text fields ---------------------------------------------------

    [Fact]
    public async Task MapGetList_Search_MatchesOnAnyTextFieldOfTheModel()
    {
        var response = await host.Client.GetAsync("/p/widgets?search=widget-20&pageSize=100");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(7);
    }

    [Fact]
    public async Task MapGetList_BlankSearch_LeavesTheListingUntouched()
    {
        // A blank search is absent, not "match nothing" — the endpoint must behave as if the parameter were
        // never supplied, or every UI that binds an empty search box to it returns zero rows.
        var response = await host.Client.GetAsync("/p/widgets?search=&pageSize=100");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(PagingTestHost.SeededWidgetCount);
    }

    [Fact]
    public async Task MapGetList_SearchMatchingNothing_Returns200WithAnEmptyPage()
    {
        var response = await host.Client.GetAsync("/p/widgets?search=no-such-widget");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task MapGetList_SearchWithFilter_CombinesWithAnd()
    {
        // search narrows to 23 rows, the filter to 21; together they leave widget-25 and widget-205. An OR
        // would return far more, so this distinguishes the two.
        var response = await host.Client.GetAsync(
            "/p/widgets?search=widget-2&filter=name:EndsWith:5&pageSize=100");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Name).OrderBy(n => n).ShouldBe(["widget-205", "widget-25"]);
    }

    [Fact]
    public async Task MapGetList_SearchValueMatchingANonTextField_DoesNotMatch()
    {
        // Only text fields are searched, so a pasted id finds nothing here — that is what 'filter' is for.
        // Asserting this pins the boundary: were non-text fields swept in, this would return one row.
        var response = await host.Client.GetAsync($"/p/widgets?search={host.SeededWidgetIds[0]}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.TotalItemCount.ShouldBe(0);
    }

    // --- Ordering: caller-chosen order replaces the endpoint default ---------------------------------------

    [Fact]
    public async Task MapGetList_OrderByModelField_ReplacesTheDefaultOrdering()
    {
        // Default ordering is Id descending, which would put widget-205 first; ascending by name is
        // lexicographic, so widget-1 sorts ahead of widget-2.
        var response = await host.Client.GetAsync("/p/widgets?orderBy=name&pageSize=3");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Name).ShouldBe(["widget-1", "widget-10", "widget-100"]);
    }

    [Fact]
    public async Task MapGetList_OrderByDescending_ReversesTheOrder()
    {
        var response = await host.Client.GetAsync("/p/widgets?orderBy=name&desc=true&pageSize=3");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WidgetModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Name).ShouldBe(["widget-99", "widget-98", "widget-97"]);
    }

    [Fact]
    public async Task MapGetList_OrderByFieldNotOnTheModel_Returns400()
    {
        // Without validation this reaches Expression.PropertyOrField, which throws — a 500, not a 400.
        var response = await host.Client.GetAsync("/p/gadgets?orderBy=createdBy");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion
}
