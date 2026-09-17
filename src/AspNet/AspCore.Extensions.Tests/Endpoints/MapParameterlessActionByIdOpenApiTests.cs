using AspCore.Extensions.Tests.Fixtures;
using System.Text.Json;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     DRK-1453 R5: a bodyless action route's OpenAPI operation must declare no required request body — a
///     client generator that assumes one exists would send a body the route never reads.
/// </summary>
public class MapParameterlessActionByIdOpenApiTests(EndpointTestHost host) : IClassFixture<EndpointTestHost>
{
    #region Methods

    [Fact]
    public async Task OpenApiDocument_BodylessActionOperation_DeclaresNoRequestBody()
    {
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));

        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/t/things/{id}/bodyless-action")
            .GetProperty("post");

        operation.TryGetProperty("requestBody", out _).ShouldBeFalse();
    }

    #endregion
}
