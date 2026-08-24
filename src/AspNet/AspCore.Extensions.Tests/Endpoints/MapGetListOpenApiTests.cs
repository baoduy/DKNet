using AspCore.Extensions.Tests.Fixtures;
using System.Text.Json;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Pins how <c>MapGetList</c>'s parameters are published in the OpenAPI document. The <c>filter</c>
///     parameter is the one at risk: it binds through <see cref="IParsable{TSelf}" />, and if the generator
///     described it as an object schema instead of an array of strings, every Swagger UI and generated client
///     would present an unusable input for the endpoint's main feature.
/// </summary>
public class MapGetListOpenApiTests(PagingTestHost host) : IClassFixture<PagingTestHost>
{
    #region Methods

    [Fact]
    public async Task OpenApiDocument_FilterParameter_IsAnArrayOfPlainValuesWithTheDescription()
    {
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));

        var parameter = FindWidgetListParameter(document, "filter");

        // The wire format is a repeated colon-separated string, so the schema must be an array whose items are
        // NOT a generated object of Field/Operation/Value — that shape would have every Swagger UI and client
        // generator offering a JSON form the endpoint cannot bind. ListFilter's JsonConverter keeps the object
        // schema out; the exporter cannot see through a custom converter, so items stay unconstrained ("any",
        // rendered as free text), which is why the description has to carry the format.
        var schema = parameter.GetProperty("schema");
        schema.GetProperty("type").GetString().ShouldBe("array");
        if (schema.TryGetProperty("items", out var items))
        {
            items.TryGetProperty("$ref", out _).ShouldBeFalse();
            items.TryGetProperty("properties", out _).ShouldBeFalse();
        }

        parameter.GetProperty("description").GetString()!.ShouldContain("field:operation:value");
    }

    [Fact]
    public async Task OpenApiDocument_SearchAndOrderByParameters_AreStringsWithDescriptions()
    {
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));

        var search = FindWidgetListParameter(document, "search");
        search.GetProperty("schema").GetProperty("type").GetString().ShouldBe("string");
        search.GetProperty("description").GetString()!.ShouldContain("2 characters");

        var orderBy = FindWidgetListParameter(document, "orderBy");
        orderBy.GetProperty("schema").GetProperty("type").GetString().ShouldBe("string");
    }

    /// <summary>Finds one query parameter of the widgets list operation by its published name.</summary>
    private static JsonElement FindWidgetListParameter(JsonDocument document, string name)
    {
        var parameters = document.RootElement
            .GetProperty("paths")
            .GetProperty("/p/widgets")
            .GetProperty("get")
            .GetProperty("parameters");

        foreach (var parameter in parameters.EnumerateArray())
        {
            if (parameter.GetProperty("name").GetString() == name) return parameter;
        }

        throw new InvalidOperationException(
            $"Parameter '{name}' is not published on GET /p/widgets. Published: " +
            string.Join(", ", parameters.EnumerateArray().Select(p => p.GetProperty("name").GetString())));
    }

    #endregion
}
