using System.Net;
using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests;

/// <summary>
///     Proves <c>IContextualSource</c>-declared members (e.g. <c>[FromClaim]</c>) are excluded from the published
///     OpenAPI description — for a JSON-body-bound command via <c>ContextualSourceSchemaTransformer</c>, and for
///     an <c>[AsParameters]</c>/query-bound one via <c>ContextualSourceOperationTransformer</c> (DRK-565) —
///     against a real generated <c>/openapi/v1.json</c>, the same pattern <c>MapGetListPagingTests</c> already
///     uses for this repo's other published-description assertions.
/// </summary>
public class ContextualSourceOpenApiTests
{
    #region Methods

    private static async Task<WebApplication> BuildHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(ProbeEndpointConfig).Assembly)
            .AddChildBus(
                "Memory",
                mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(ProbeEndpointConfig).Assembly)));
        builder.Services.AddValidatorsFromAssemblyContaining<AttributedValidatedCommandValidator>();
        builder.Services.AddContextualRequestPopulation();
        builder.Services.AddOpenApi();

        var app = builder.Build();
        app.UseEndpointConfigs(
            o =>
            {
                o.EnableVersioning = false;
                o.RequireAuthorization = false; // generating the doc needs no authenticated caller
            },
            typeof(ProbeEndpointConfig).Assembly);
        app.MapOpenApi();
        await app.StartAsync();
        return app;
    }

    /// <summary>Resolves <paramref name="schema" /> through its <c>$ref</c> against <c>components/schemas</c> when it is a reference, otherwise returns it unchanged.</summary>
    private static JsonElement ResolveSchema(JsonElement document, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference)) return schema;

        var schemaName = reference.GetString()!.Split('/')[^1];
        return document.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
    }

    [Fact]
    public async Task JsonBodySchema_DeclaredMemberExcluded_NonDeclaredSiblingPresent()
    {
        var app = await BuildHostAsync();
        try
        {
            using var client = app.GetTestClient();
            var response = await client.GetAsync("/openapi/v1.json");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rawSchema = document.RootElement
                .GetProperty("paths").GetProperty("/probe/attributed-validated").GetProperty("post")
                .GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema");
            var schema = ResolveSchema(document.RootElement, rawSchema);
            var propertyNames = schema.GetProperty("properties").EnumerateObject()
                .Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            propertyNames.ShouldNotContain("byUser"); // declared via [FromClaim] -> excluded
            propertyNames.ShouldContain("name"); // not declared -> still advertised as caller input
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task QueryOperationParameters_DeclaredMemberExcluded_NonDeclaredSiblingPresent()
    {
        var app = await BuildHostAsync();
        try
        {
            using var client = app.GetTestClient();
            var response = await client.GetAsync("/openapi/v1.json");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var parameters = document.RootElement
                .GetProperty("paths").GetProperty("/probe/by-user-query-with-name").GetProperty("get")
                .GetProperty("parameters").EnumerateArray()
                .Select(p => p.GetProperty("name").GetString()!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            parameters.ShouldNotContain("byUser"); // declared via [FromClaim] -> excluded
            parameters.ShouldContain("name"); // not declared -> still advertised as a query parameter
        }
        finally
        {
            await app.StopAsync();
        }
    }

    #endregion
}
