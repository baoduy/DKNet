using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     DRK-1355 §5 scenarios 1, 2, 4 and 5 (@integration), driven over <see cref="GadgetExclusionTestHost" />'s
///     real <see cref="Microsoft.AspNetCore.TestHost.TestServer" />. The spec's "Product" entity with update
///     members "UpdatePrice"/"Rename" and action members "Approve"/"Archive" maps onto
///     <see cref="SlimBus.Generators.Tests.Domain.Catalog.Gadget" />'s "UpdatePrice"/"Rename" and
///     "Approve"/"Discontinue" (§7 slice notes): "Discontinue" is a real Gadget action, so it stands in for the
///     spec's "Archive".
/// </summary>
public sealed class PerMemberRouteExclusionTests(GadgetExclusionTestHost host) : IClassFixture<GadgetExclusionTestHost>
{
    private async Task<Guid> CreateGadgetAsync(string groupUrl)
    {
        var created = await host.Client.PostAsJsonAsync(groupUrl, new { name = "g", price = 1m });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await created.Content.ReadFromJsonAsync<GadgetDto>();
        return dto!.Id;
    }

    [Fact]
    public async Task Scenario1_ExcludingRenameByName_KeepsUpdatePriceRoutePublished()
    {
        var id = await CreateGadgetAsync("/gadgets-no-rename");

        var updatePrice = await host.Client.PutAsJsonAsync($"/gadgets-no-rename/{id}", new { price = 42m });
        updatePrice.StatusCode.ShouldBe(HttpStatusCode.OK);

        var rename = await host.Client.PutAsJsonAsync($"/gadgets-no-rename/{id}/rename", new { name = "renamed" });
        rename.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Scenario2_ExcludingDiscontinueByName_KeepsApproveRoutePublished()
    {
        var id = await CreateGadgetAsync("/gadgets-no-discontinue");

        var approve = await host.Client.PostAsJsonAsync($"/gadgets-no-discontinue/{id}/approve", new { });
        approve.StatusCode.ShouldBe(HttpStatusCode.OK);

        var discontinue = await host.Client.PostAsJsonAsync($"/gadgets-no-discontinue/{id}/discontinue", new { });
        discontinue.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Scenario4_ExcludingUpdatePriceByName_DoesNotMoveRenameToTheVacatedAddress()
    {
        var id = await CreateGadgetAsync("/gadgets-no-update-price");

        var rename = await host.Client.PutAsJsonAsync($"/gadgets-no-update-price/{id}/rename", new { name = "renamed" });
        rename.StatusCode.ShouldBe(HttpStatusCode.OK);

        // "{id}" is shared by GET/DELETE, which stay registered — an unmatched PUT there yields 405
        // (RFC 9110 §15.5.6), the same shared-template reasoning already established for scenario 6 of
        // PerRouteScopedAuthorizationTests. The proof that "UpdatePrice" itself is gone, not moved, is that
        // "Rename" above still answers only at its own address, never at "{id}".
        var updatePrice = await host.Client.PutAsJsonAsync($"/gadgets-no-update-price/{id}", new { price = 42m });
        updatePrice.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Scenario5_ExcludingRenameByName_LeavesUntouchedKindsPublished()
    {
        var id = await CreateGadgetAsync("/gadgets-no-rename");

        var getById = await host.Client.GetAsync($"/gadgets-no-rename/{id}");
        getById.StatusCode.ShouldBe(HttpStatusCode.OK);

        var delete = await host.Client.DeleteAsync($"/gadgets-no-rename/{id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}

/// <summary>
///     DRK-1355 §5 scenario outline "Excluding a whole kind still removes every route of that kind"
///     (@integration) — the pre-existing CrudOp-kind <c>Exclude</c> overload (spec R2, unchanged, §3 rows
///     6-7 KEEP), driven over the shared <see cref="GadgetTestHost" /> since it exercises none of the new
///     by-name API and is therefore safe alongside the `@existing` <c>GadgetCrudSliceTests</c> baseline.
/// </summary>
public sealed class WholeKindExclusionStillRemovesEveryRouteTests(GadgetTestHost host) : IClassFixture<GadgetTestHost>
{
    [Fact]
    public async Task ExcludingTheWholeUpdateKind_RemovesBothUpdateRoutes()
    {
        var created = await host.Client.PostAsJsonAsync("/gadgets-no-updates", new { name = "g", price = 1m });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await created.Content.ReadFromJsonAsync<GadgetDto>();

        // "{id}" is shared by GET/DELETE, which stay registered — an unmatched PUT there yields 405
        // (RFC 9110 §15.5.6), not 404 (same reasoning as PerRouteScopedAuthorizationTests scenario 6).
        var updatePrice = await host.Client.PutAsJsonAsync($"/gadgets-no-updates/{dto!.Id}", new { price = 2m });
        updatePrice.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);

        // "{id}/rename" has no other method registered on it, so it is simply gone.
        var rename = await host.Client.PutAsJsonAsync($"/gadgets-no-updates/{dto.Id}/rename", new { name = "renamed" });
        rename.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExcludingTheWholeActionKind_RemovesBothActionRoutes()
    {
        var created = await host.Client.PostAsJsonAsync("/gadgets-no-actions", new { name = "g", price = 1m });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await created.Content.ReadFromJsonAsync<GadgetDto>();

        var approve = await host.Client.PostAsJsonAsync($"/gadgets-no-actions/{dto!.Id}/approve", new { });
        approve.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var discontinue = await host.Client.PostAsJsonAsync($"/gadgets-no-actions/{dto.Id}/discontinue", new { });
        discontinue.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
