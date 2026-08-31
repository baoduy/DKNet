using System.Net;
using System.Net.Http.Json;
using AspCore.Extensions.Tests.Fixtures;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Exercises <c>MapActionById</c> — same route-id-wins-over-body-id and 200-with-body contract as
///     <c>MapPutById</c> (<see cref="MapPutByIdTests" />); the only difference is the HTTP method registered,
///     which the caller supplies as a plain string rather than a fixed verb.
/// </summary>
public class MapActionByIdTests(EndpointTestHost host) : IClassFixture<EndpointTestHost>
{
    #region Methods

    [Fact]
    public async Task Patch_RouteIdDiffersFromBodyId_HandlerReceivesTheRouteId()
    {
        var routeId = Guid.NewGuid();

        var response = await host.Client.PatchAsync(
            $"/t/things/{routeId}/archive",
            JsonContent.Create(new { Id = Guid.NewGuid() }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<string>();
        body.ShouldBe($"archived:{routeId}");
    }

    [Fact]
    public async Task Put_AgainstAnActionRouteRegisteredOnlyForPatch_IsRejected()
    {
        // MapActionById registers exactly the verb it was given ("PATCH" for this route) — proving that,
        // not just that PATCH itself dispatches, is the point of this test.
        var response = await host.Client.PutAsJsonAsync($"/t/things/{Guid.NewGuid()}/archive", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    #endregion
}
