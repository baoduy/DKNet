using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests.ModelBinding;

/// <summary>
///     End-to-end HTTP-level proof of DRK-1524 §5: a request member declared via
///     <see cref="FromRequestHeaderAttribute" /> is populated from a named HTTP request header, before validation
///     and before the handler, case-insensitively, the caller's body can never override it, a duplicate header
///     fills the member with the first value sent, and a missing header is not a refusal — with or without a
///     configured fallback. Claim-filled members' unaffected behaviour is already covered by
///     <see cref="ContextualRequestPopulationEndToEndTests" />; the published-description proof for a header
///     parameter lives in <see cref="ContextualSourceOpenApiTests" />.
/// </summary>
public class ContextualHeaderSourceEndToEndTests
{
    #region Methods

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(ProbeEndpointConfig).Assembly)
            .AddChildBus(
                "Memory",
                mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(ProbeEndpointConfig).Assembly)));
        builder.Services.AddAuthorization();
        builder.Services.AddValidatorsFromAssemblyContaining<ValidatedByHeaderCommandValidator>();
        return builder;
    }

    private static async Task<WebApplication> StartAppAsync(WebApplicationBuilder builder)
    {
        var app = builder.Build();
        app.UseEndpointConfigs(
            o =>
            {
                o.EnableVersioning = false;
                o.RequireAuthorization = false; // no caller identity needed to prove a header-sourced member
            },
            typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        return app;
    }

    // --- DRK-1524 §5 "A member declared from a header arrives filled" (both header-name spellings) --------------

    [Theory]
    [InlineData("Idempotency-Key")]
    [InlineData("idempotency-key")]
    public async Task HeaderPresent_HandlerObservesHeaderValue(string spelling)
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        var app = await StartAppAsync(builder);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/probe/by-header")
        {
            Content = JsonContent.Create(new ByHeaderProbeCommand())
        };
        request.Headers.Add(spelling, "k-77");
        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("k-77");
        await app.StopAsync();
    }

    // --- DRK-1524 §5 "The member is filled before the request is validated" -------------------------------------

    [Fact]
    public async Task HeaderPresent_ValidationRunsAfterPopulation_NotRefused()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        var app = await StartAppAsync(builder);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/probe/validated-by-header")
        {
            Content = JsonContent.Create(new ValidatedByHeaderCommand())
        };
        request.Headers.Add("Idempotency-Key", "k-77");
        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK); // the NotEmpty() rule would reject an empty value
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("k-77");
        await app.StopAsync();
    }

    // --- DRK-1524 §5 "treasury-ops cannot set the member through the request body" -----------------------------

    [Fact]
    public async Task HeaderPresent_BodyValueOverwritten_HeaderValueWins()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        var app = await StartAppAsync(builder);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/probe/by-header")
        {
            Content = JsonContent.Create(new ByHeaderProbeCommand { IdempotencyKey = "forged" })
        };
        request.Headers.Add("Idempotency-Key", "k-77");
        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("k-77"); // never "forged"
        await app.StopAsync();
    }

    // --- DRK-1524 §5 "A header sent twice fills the member with the first value" --------------------------------

    [Fact]
    public async Task HeaderSentTwice_HandlerObservesFirstValue()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        var app = await StartAppAsync(builder);
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/probe/by-header")
        {
            Content = JsonContent.Create(new ByHeaderProbeCommand())
        };
        request.Headers.Add("Idempotency-Key", new[] { "k-77", "k-88" });
        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("k-77"); // the first value sent, never a joined "k-77,k-88"
        await app.StopAsync();
    }

    // --- DRK-1524 §5 "A missing header is not a refusal" ---------------------------------------------------------

    [Fact]
    public async Task HeaderAbsent_NoFallbackConfigured_HandlerObservesDefault_NotRefused()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        var app = await StartAppAsync(builder);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe/by-header", new ByHeaderProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK); // an absent header is never a refusal
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("(null)"); // handler's stand-in for the property's type default
        await app.StopAsync();
    }

    // --- DRK-1524 §5 "A missing header takes the configured fallback on an anonymous group" ---------------------

    [Fact]
    public async Task HeaderAbsent_FallbackConfigured_RequireAuthorizationFalse_HandlerObservesFallback()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation(o => o.SystemAccountFallback = "system-account");
        var app = await StartAppAsync(builder);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe/by-header", new ByHeaderProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("system-account");
        await app.StopAsync();
    }

    #endregion
}
