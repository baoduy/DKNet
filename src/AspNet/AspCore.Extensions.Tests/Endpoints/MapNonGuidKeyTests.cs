using System.Net;
using System.Net.Http.Json;
using AspCore.Extensions.Tests.Fixtures;
using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Coverage for the <c>TKey</c>-generic <c>MapGetById</c> / <c>MapGetList</c> / <c>MapDeleteById</c> overloads.
///     The pre-existing suite pins the <see cref="Guid" /> path only, which cannot catch a key type that fails to
///     bind from the route or whose id-equality predicate does not translate — so these assert an <see cref="int" />
///     key (a struct key with no framework special-casing) and a <see cref="string" /> key (which does not
///     implement <see cref="IParsable{TSelf}" />, the case a stricter constraint would have shut out).
/// </summary>
public class MapNonGuidKeyTests(EndpointTestHost host) : IClassFixture<EndpointTestHost>
{
    #region Methods

    [Fact]
    public async Task MapDeleteById_WithIntKey_Returns204_AndTheRowIsGone()
    {
        var response = await host.Client.DeleteAsync($"/t/sprockets/{host.DeletableSprocketId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WidgetDbContext>();
        (await db.Sprockets.AnyAsync(s => s.Id == host.DeletableSprocketId)).ShouldBeFalse();
    }

    [Fact]
    public async Task MapGetById_WithIntKey_Returns200_AndTheMatchingRow()
    {
        var response = await host.Client.GetAsync("/t/sprockets/2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var model = await response.Content.ReadFromJsonAsync<SprocketModel>();
        model.ShouldNotBeNull();
        model.Id.ShouldBe(2);
        model.Name.ShouldBe("sprocket-two");
    }

    [Fact]
    public async Task MapGetById_WithIntKey_ForAnUnknownId_Returns404()
    {
        var response = await host.Client.GetAsync("/t/sprockets/4242");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MapGetById_WithIntKey_ForANonNumericSegment_Returns400()
    {
        // An id that cannot be parsed into TKey is a malformed request, not a missing row — minimal-API route
        // binding rejects it with 400 before the handler runs, which is also what the Guid path already does
        // for a non-Guid segment. Pinned so a future switch to a looser binding (accepting the raw string and
        // parsing inside the handler, turning this into a 404) surfaces as a deliberate contract change.
        var response = await host.Client.GetAsync("/t/sprockets/not-a-number");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MapGetById_WithStringKey_Returns200_AndTheMatchingRow()
    {
        var response = await host.Client.GetAsync("/t/coupons/SUMMER-2026");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var model = await response.Content.ReadFromJsonAsync<CouponModel>();
        model.ShouldNotBeNull();
        model.Id.ShouldBe("SUMMER-2026");
        model.Label.ShouldBe("Summer sale");
    }

    [Fact]
    public async Task MapGetById_WithStringKey_ForAnUnknownId_Returns404()
    {
        var response = await host.Client.GetAsync("/t/coupons/NOPE-1999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MapGetList_WithIntKey_OrdersByIdDescendingByDefault()
    {
        // The default ordering falls back to Id descending for a non-audited entity. Ids 1-3 are stable
        // (99 belongs to the delete scenario), so the descending sequence is the assertion that the generic
        // Id ordering path resolves the key property rather than throwing or ordering arbitrarily.
        var response = await host.Client.GetAsync("/t/sprockets");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<SprocketModel>>();
        page.ShouldNotBeNull();
        page.Items.Where(i => i.Id <= 3).Select(i => i.Id).ShouldBe([3, 2, 1]);
    }

    [Fact]
    public async Task MapGetList_WithIntKey_FiltersOnTheProjectedKey()
    {
        var response = await host.Client.GetAsync("/t/sprockets?filter=id:Equal:2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<SprocketModel>>();
        page.ShouldNotBeNull();
        page.Items.Select(i => i.Name).ShouldBe(["sprocket-two"]);
    }

    #endregion
}
