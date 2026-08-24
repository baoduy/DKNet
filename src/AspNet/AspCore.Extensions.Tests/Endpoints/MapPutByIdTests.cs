using System.Net;
using System.Net.Http.Json;
using AspCore.Extensions.Tests.Fixtures;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Exercises <c>MapPutById</c> — the route-bound key must win over any <c>Id</c> present in the request
///     body before dispatch, and a successful result must surface as 200 with the response payload.
/// </summary>
public class MapPutByIdTests(EndpointTestHost host) : IClassFixture<EndpointTestHost>
{
    #region Methods

    [Fact]
    public async Task Put_RouteIdDiffersFromBodyId_HandlerReceivesTheRouteId()
    {
        var routeId = Guid.NewGuid();

        var response = await host.Client.PutAsJsonAsync(
            $"/t/things/{routeId}",
            new RenameThingRequest { Id = Guid.NewGuid(), Name = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<string>();
        body.ShouldBe($"{routeId}:x");
    }

    #endregion
}
