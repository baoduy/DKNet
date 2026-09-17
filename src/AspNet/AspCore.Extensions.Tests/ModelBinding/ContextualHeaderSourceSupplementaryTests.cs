using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests.ModelBinding;

/// <summary>
///     Supplementary coverage for <c>FromRequestHeaderAttribute</c>/<c>ContextualSourceOperationTransformer</c>
///     beyond the frozen DRK-1524 §5 acceptance tests (<see cref="ContextualHeaderSourceEndToEndTests" />,
///     <see cref="ContextualSourceOpenApiTests" />): a request type declaring two independently-named header
///     members, proving both are populated and both published, and that the published parameter is never
///     advertised as required with a non-string schema.
/// </summary>
public class ContextualHeaderSourceSupplementaryTests
{
    #region Methods

    private static async Task<WebApplication> BuildHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(MultiHeaderProbeEndpointConfig).Assembly)
            .AddChildBus(
                "Memory",
                mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(MultiHeaderProbeEndpointConfig).Assembly)));
        builder.Services.AddContextualRequestPopulation();
        builder.Services.AddOpenApi();

        var app = builder.Build();
        app.UseEndpointConfigs(
            o =>
            {
                o.EnableVersioning = false;
                o.RequireAuthorization = false;
            },
            typeof(MultiHeaderProbeEndpointConfig).Assembly);
        app.MapOpenApi();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task BothHeadersPresent_HandlerObservesBothValues()
    {
        var app = await BuildHostAsync();
        try
        {
            using var client = app.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/probe-multi-header/two-headers")
            {
                Content = JsonContent.Create(new TwoHeaderProbeCommand())
            };
            request.Headers.Add("X-First", "one");
            request.Headers.Add("X-Second", "two");
            var response = await client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
            body.ShouldNotBeNull();
            body.Name.ShouldBe("one|two"); // never just the first header's value
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PublishedOperation_DeclaresBothHeaderParameters_NeitherRequiredNorNonString()
    {
        var app = await BuildHostAsync();
        try
        {
            using var client = app.GetTestClient();
            var response = await client.GetAsync("/openapi/v1.json");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var parameters = document.RootElement
                .GetProperty("paths").GetProperty("/probe-multi-header/two-headers").GetProperty("post")
                .GetProperty("parameters").EnumerateArray().ToList();

            var names = parameters.Select(p => p.GetProperty("name").GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            names.ShouldContain("X-First");
            names.ShouldContain("X-Second");

            foreach (var parameter in parameters)
            {
                parameter.GetProperty("in").GetString().ShouldBe("header");
                // "required" is omitted from the document entirely when false — its absence IS the false case.
                (!parameter.TryGetProperty("required", out var required) || !required.GetBoolean()).ShouldBeTrue();
                parameter.GetProperty("schema").GetProperty("type").GetString().ShouldBe("string");
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    #endregion
}
