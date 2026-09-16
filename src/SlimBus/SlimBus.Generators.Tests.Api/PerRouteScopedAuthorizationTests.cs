using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     DRK-1327 §5 scenarios 1, 2, 3, 6, 7 (@integration), driven over <see cref="GadgetAuthTestHost" />'s real
///     <see cref="Microsoft.AspNetCore.TestHost.TestServer" />. Dana/Mei/Ravi are principals of
///     <see cref="ScopedTestAuthHandler" /> differing only in the <c>X-Test-Scopes</c> header they send.
///     The spec's "Product" entity with update routes "Rename" and "ChangePrice" maps onto
///     <see cref="SlimBus.Generators.Tests.Domain.Catalog.Gadget" />'s "Rename" and "UpdatePrice" (DRK-1327 §7
///     slice notes): "UpdatePrice" is declared first and keeps the plain <c>{id}</c> route; "Rename" is declared
///     second (DRK-1336 row 7) and takes <c>{id}/rename</c>.
/// </summary>
public sealed class PerRouteScopedAuthorizationTests(GadgetAuthTestHost host) : IClassFixture<GadgetAuthTestHost>
{
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? scopes = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(ScopedTestAuthHandler.UserHeader, "principal-under-test");
        if (scopes is not null) request.Headers.Add(ScopedTestAuthHandler.ScopesHeader, scopes);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await host.Client.SendAsync(request);
    }

    private async Task<Guid> CreateGadgetAsync(string groupUrl)
    {
        var created = await SendAsync(HttpMethod.Post, groupUrl, body: new { name = "g", price = 1m });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await created.Content.ReadFromJsonAsync<GadgetDto>();
        return dto!.Id;
    }

    [Fact]
    public async Task Scenario1_RouteScopeOnUpdatePrice_RefusesDanaThereButAllowsHerOnRename()
    {
        var id = await CreateGadgetAsync("/gadgets-route-scope");

        // Dana holds no scope — refused on the route-scoped "UpdatePrice" ({id}).
        var updatePrice = await SendAsync(HttpMethod.Put, $"/gadgets-route-scope/{id}", body: new { price = 2m });
        updatePrice.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Dana is allowed on "Rename" ({id}/rename) — the route setting never touched it.
        var rename = await SendAsync(HttpMethod.Put, $"/gadgets-route-scope/{id}/rename", body: new { name = "renamed" });
        rename.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Scenario2_OpKindScopeOnUpdate_RefusesDanaOnBothUpdateRoutes()
    {
        var id = await CreateGadgetAsync("/gadgets-op-scope");

        var updatePrice = await SendAsync(HttpMethod.Put, $"/gadgets-op-scope/{id}", body: new { price = 2m });
        updatePrice.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var rename = await SendAsync(HttpMethod.Put, $"/gadgets-op-scope/{id}/rename", body: new { name = "renamed" });
        rename.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Scenario3_OpKindAndRouteScopeBothApply_MeiRefusedRaviAllowed()
    {
        var id = await CreateGadgetAsync("/gadgets-both-scopes");

        // Mei holds only "product.write" — the op-kind scope — but "UpdatePrice" also needs "product.price".
        var mei = await SendAsync(HttpMethod.Put, $"/gadgets-both-scopes/{id}", scopes: "product.write", body: new { price = 2m });
        mei.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Ravi holds both scopes — allowed.
        var ravi = await SendAsync(HttpMethod.Put, $"/gadgets-both-scopes/{id}", scopes: "product.write,product.price", body: new { price = 2m });
        ravi.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Scenario6_ExcludingDelete_DropsItsScopeSettingAndReportsNoError()
    {
        var id = await CreateGadgetAsync("/gadgets-excluded-delete-scope");

        // The delete route is excluded, so its "product.write" scope setting never applies — DELETE {id}
        // shares its route template with GET/PUT {id}, so an unmatched method yields 405 (RFC 9110 §15.5.6),
        // not a 401/403 a registered-but-guarded DELETE would have answered. 405 is the proof the route was
        // never registered. Registration itself must not have thrown either.
        var delete = await SendAsync(HttpMethod.Delete, $"/gadgets-excluded-delete-scope/{id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Scenario7_NoConfiguration_EveryRouteStaysOpenEvenWithAuthWiredIntoTheHost()
    {
        // No X-Test-Scopes / X-Test-User header at all — an anonymous-scoped caller.
        var created = await host.Client.PostAsJsonAsync("/gadgets-unconfigured", new { name = "g", price = 1m });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await created.Content.ReadFromJsonAsync<GadgetDto>();

        var updated = await host.Client.PutAsJsonAsync($"/gadgets-unconfigured/{dto!.Id}", new { price = 2m });
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);

        var renamed = await host.Client.PutAsJsonAsync($"/gadgets-unconfigured/{dto.Id}/rename", new { name = "renamed" });
        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);

        // "all five routes are published" (§7 slice note) — GET {id} and GET / included, not just the
        // create/update/delete routes exercised above.
        var byId = await host.Client.GetAsync($"/gadgets-unconfigured/{dto.Id}");
        byId.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await host.Client.GetAsync("/gadgets-unconfigured");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleted = await host.Client.DeleteAsync($"/gadgets-unconfigured/{dto.Id}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
