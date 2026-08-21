using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests.ModelBinding;

/// <summary>
///     End-to-end HTTP-level proof that <c>AddContextualRequestPopulation</c> is actually wired into
///     <c>UseEndpointConfigs</c> request dispatch (DRK-565): declared members are populated before the handler
///     runs for both JSON-body and <c>[AsParameters]</c>/query binding, a caller-supplied value can never forge
///     the resolved claim, the system-account fallback only applies within its documented boundary, and a
///     request with no declared members is entirely unaffected. Unit-level branch coverage of the population
///     service itself lives in <see cref="ContextualRequestPopulationTests" />.
/// </summary>
public class ContextualRequestPopulationEndToEndTests
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
        return builder;
    }

    private static void AddTestAuth(WebApplicationBuilder builder, Action<TestAuthSchemeOptions> configure)
    {
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<TestAuthSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, configure);
    }

    // --- Item 1 + 2: declared member populated before the handler runs, on a request with no base type at all ---

    [Fact]
    public async Task JsonBodyBoundCommand_ClaimPresent_HandlerObservesClaimValue()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "alice";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("alice");
        await app.StopAsync();
    }

    [Fact]
    public async Task QueryBoundCommand_ClaimPresent_HandlerObservesClaimValue()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "alice";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/probe/by-user-query");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("alice");
        await app.StopAsync();
    }

    // --- Item 3: non-forgeability — a caller-supplied value never survives, over either binding source -----------

    [Fact]
    public async Task JsonBodyBoundCommand_CallerSuppliesByUser_ResolvedClaimValueWins()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "alice";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/probe/by-user", new ByUserProbeCommand { ByUser = "forged-by-caller" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("alice"); // never "forged-by-caller"
        await app.StopAsync();
    }

    [Fact]
    public async Task QueryBoundCommand_CallerSuppliesByUser_ResolvedClaimValueWins()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "alice";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/probe/by-user-query?ByUser=forged-by-caller");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("alice"); // never "forged-by-caller"
        await app.StopAsync();
    }

    // --- Item 4: claim missing + caller supplies a value -> type default, never the caller's value ----------------

    [Fact]
    public async Task JsonBodyBoundCommand_CallerSuppliesByUserAndClaimIsMissing_ResultsInTypeDefault()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = null; // authenticated, but no ClaimTypes.Name claim on the principal
        });
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/probe/by-user", new ByUserProbeCommand { ByUser = "forged-by-caller" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("(null)"); // handler's stand-in for the property's default (null), not the caller's value
        await app.StopAsync();
    }

    // --- Item 6: a claim value that fails to convert to the member's type -> type default -------------------------

    [Fact]
    public async Task GuidDeclaredMember_ClaimValueIsNotAGuid_HandlerObservesGuidEmpty()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.Claims = [new Claim("tenant-id", "not-a-guid")];
        });
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe/guid-claim", new GuidClaimCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe(Guid.Empty.ToString());
        await app.StopAsync();
    }

    // --- Item 7 + 8: system-account fallback, and its auth-required boundary -----------------------------------

    [Fact]
    public async Task RequireAuthorizationFalse_NoClaimResolvable_FallbackConfigured_HandlerObservesFallback()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation(o => o.SystemAccountFallback = "system-account");
        var app = builder.Build();
        app.UseEndpointConfigs(
            o =>
            {
                o.EnableVersioning = false;
                o.RequireAuthorization = false;
            },
            typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("system-account");
        await app.StopAsync();
    }

    [Fact]
    public async Task RequireAuthorizationTrue_AuthenticatedWithoutTheClaim_FallbackNeverLeaks()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation(o => o.SystemAccountFallback = "system-account");
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = null; // authenticated, but carries no ClaimTypes.Name claim
        });
        var app = builder.Build();
        var groups = app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        groups.ShouldNotBeEmpty(); // default RequireAuthorization stays true (not overridden above)
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("(null)"); // fallback never leaks across the RequireAuthorization=true boundary
        await app.StopAsync();
    }

    // --- Item 9: multiple declared members, each from its own claim -------------------------------------------

    [Fact]
    public async Task MultipleDeclaredMembers_EachResolvedFromItsOwnClaim()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "alice";
            o.Claims = [new Claim("tenant-id", "acme")];
        });
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe/multi-claim", new MultiClaimCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("alice|acme");
        await app.StopAsync();
    }

    // --- DRK-565 review findings 3 + 4: a second, host-registered source kind, scoped end to end ----------------

    [Fact]
    public async Task SecondSourceKindWithScopedResolver_RequestDeclaringBothKinds_BothResolveUnderValidateScopes()
    {
        // Finding 4: a host registers its OWN IContextualSource/IContextualValueResolver pair alongside the
        // built-in claim one — no change to any type in DKNet.AspCore.Extensions is needed for it to work.
        // Finding 3: ScopedSecondSourceResolver depends on a scoped ISecondSourceProbe, and ValidateScopes = true
        // below means the whole pipeline (including the built-in ContextualRequestPopulationService, now scoped
        // rather than singleton) must resolve without a captive-dependency or scope-validation failure.
        var builder = CreateBuilder();
        builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);
        builder.Services.AddContextualRequestPopulation();
        builder.Services.AddScoped<ISecondSourceProbe, SecondSourceProbe>();
        builder.Services.AddScoped<IContextualValueResolver, ScopedSecondSourceResolver>();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "alice";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe/mixed-source", new MixedSourceCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("alice|second-source-value");
        await app.StopAsync();
    }

    // --- Item 10: a request with no declared members is completely unaffected, at real dispatch level -----------

    [Fact]
    public async Task RequestWithNoDeclaredMembers_PopulationRegistered_StillDispatchesUnchanged()
    {
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "alice";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/probe/validated", new ValidatedCommand { Name = "Acme" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("Acme"); // ValidatedCommand declares nothing -> population is a pure no-op
        await app.StopAsync();
    }

    #endregion
}
