using System.Net;
using System.Net.Http.Json;
using AspCore.Extensions.Tests.Fixtures;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Exercises <c>MapParameterlessActionById</c> — DRK-1453 R1-R4: a generated action route whose command
///     carries no member besides the route key must accept a request with no body at all, must still accept
///     (and ignore) a posted body, must bind the target id only from the route, and must reject any verb it
///     was not registered for, exactly like <c>MapActionById</c> (<see cref="MapActionByIdTests" />).
/// </summary>
public class MapParameterlessActionByIdTests(EndpointTestHost host) : IClassFixture<EndpointTestHost>
{
    #region Methods

    [Fact]
    public async Task Post_WithNoBodyAndNoContentType_Returns200WithResponsePayload()
    {
        // R1: an empty HttpContent carries no Content-Type header, so this proves no body AND no content
        // negotiation is required for the route to dispatch.
        var routeId = Guid.NewGuid();

        var response = await host.Client.PostAsync($"/t/things/{routeId}/bodyless-action", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<string>();
        body.ShouldBe($"bodyless:{routeId}");
    }

    [Fact]
    public async Task Post_WithEmptyJsonBody_StillReturns200AndBodyIsIgnored()
    {
        // R3: a bodyless action route still accepts a posted JSON body; it must never answer 415/400 for it.
        var routeId = Guid.NewGuid();

        var response = await host.Client.PostAsJsonAsync($"/t/things/{routeId}/bodyless-action", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<string>();
        body.ShouldBe($"bodyless:{routeId}");
    }

    [Fact]
    public async Task Post_RouteIdDiffersFromBodyId_HandlerReceivesTheRouteId()
    {
        // R4: the target id always comes from the route, never from a body — even when a caller posts one.
        var routeId = Guid.NewGuid();

        var response = await host.Client.PostAsJsonAsync(
            $"/t/things/{routeId}/bodyless-action",
            new { Id = Guid.NewGuid() });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<string>();
        body.ShouldBe($"bodyless:{routeId}");
    }

    [Fact]
    public async Task Put_AgainstAnActionRouteRegisteredOnlyForPost_IsRejected()
    {
        // MapParameterlessActionById registers exactly the verb it was given ("POST" for this route).
        var response = await host.Client.PutAsync($"/t/things/{Guid.NewGuid()}/bodyless-action", null);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    #endregion
}
