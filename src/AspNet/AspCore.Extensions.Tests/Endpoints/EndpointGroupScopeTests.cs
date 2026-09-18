using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     DRK-1542 §5 — an endpoint group declares its required scopes above the group, one HTTP method at a time.
///     Acceptance tests for <see cref="DKNet.AspCore.Extensions.Endpoints.EndpointGroupScopeAttribute" />, driven
///     through <c>WebApplication.UseEndpointConfigs</c> and real HTTP dispatch on a fresh per-test host — mirrors
///     <see cref="EndpointConfigExtensionsTests" />'s own per-test-host convention (registration is a startup-time
///     concern), duplicated here rather than shared because that class's builder helpers are private to it.
/// </summary>
public class EndpointGroupScopeTests
{
    private static WebApplicationBuilder CreateBuilder(Action<TestAuthSchemeOptions> configureAuth)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        // UseEndpointConfigs discovers every IEndpointConfig in this test assembly, including the pre-existing
        // ProbeEndpointConfig group (its [FromClaim]-declared ByUserProbeCommand needs the bus + population
        // services below) — every host built from this helper needs the same baseline EndpointConfigExtensionsTests
        // .CreateBuilder() registers, or an unrelated fixture's registration fails this test's app.StartAsync().
        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(EndpointGroupScopeTests).Assembly)
            .AddChildBus(
                "Memory",
                mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(EndpointGroupScopeTests).Assembly)));
        builder.Services.AddContextualRequestPopulation();
        builder.Services.AddAuthorization();
        builder.Services.AddApiVersioning();
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<TestAuthSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, configureAuth);
        return builder;
    }

    // --- Scenario: The caller's scope decides each method of one group -----------------------------------------
    // NOTE: the GET example (below) is green from the start — a caller merely signed in already reaches a GET
    // route the group's default RequireAuthorization() lets through today; the outline's only red example is PUT.

    [Theory]
    [InlineData("GET", HttpStatusCode.OK)]
    [InlineData("PUT", HttpStatusCode.Forbidden)]
    public async Task Scenario1_CallersScopeDecidesEachMethod_TreasuryOpsHoldingOnlyAccountsRead(
        string method,
        HttpStatusCode expectedStatus)
    {
        var builder = CreateBuilder(o =>
        {
            o.Authenticated = true;
            o.UserName = "treasury-ops";
            o.Claims = [new Claim("scope", "accounts.read")];
        });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("accounts.read", p => p.RequireClaim("scope", "accounts.read"))
            .AddPolicy("accounts.write", p => p.RequireClaim("scope", "accounts.write"));
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ScopedReadWriteEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), "/v1/scoped-read-write/item"));

        response.StatusCode.ShouldBe(expectedStatus);
        await app.StopAsync();
    }

    // --- Scenario: One declaration covers several methods -------------------------------------------------------
    // NOTE: green from the start for all three examples — the spec's outline only exercises the caller who HOLDS
    // "accounts.write", and the group's blanket RequireAuthorization() already lets any signed-in caller through
    // today, so this scenario as written cannot yet distinguish "scope enforced" from "not enforced". It stays a
    // real regression guard (it must still pass once enforcement exists) but proves nothing new on its own.

    [Theory]
    [InlineData("POST", "/v1/scoped-multi-method/item")]
    [InlineData("PUT", "/v1/scoped-multi-method/item/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/v1/scoped-multi-method/item/11111111-1111-1111-1111-111111111111")]
    public async Task Scenario2_OneDeclarationCoversSeveralMethods_TreasuryOpsHoldingAccountsWriteSucceeds(
        string method,
        string url)
    {
        var builder = CreateBuilder(o =>
        {
            o.Authenticated = true;
            o.UserName = "treasury-ops";
            o.Claims = [new Claim("scope", "accounts.write")];
        });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("accounts.write", p => p.RequireClaim("scope", "accounts.write"));
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ScopedMultiMethodEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await app.StopAsync();
    }

    // --- Scenario: A route that requires its own scope keeps it -------------------------------------------------

    [Fact]
    public async Task Scenario4_RouteRequiringItsOwnScope_TreasuryOpsHoldingOnlyPostingsReadSucceeds()
    {
        var builder = CreateBuilder(o =>
        {
            o.Authenticated = true;
            o.UserName = "treasury-ops";
            o.Claims = [new Claim("scope", "postings.read")];
        });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("accounts.read", p => p.RequireClaim("scope", "accounts.read"))
            .AddPolicy("postings.read", p => p.RequireClaim("scope", "postings.read"));
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(RouteOwnScopeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/v1/scoped-route-own-scope/item");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await app.StopAsync();
    }

    // --- Scenario: A route opened to anonymous callers needs no token -------------------------------------------

    [Fact]
    public async Task Scenario5_RouteOpenedToAnonymousCallers_UnknownCallerSucceeds()
    {
        var builder = CreateBuilder(o => o.Authenticated = false);
        builder.Services.AddAuthorizationBuilder().AddPolicy("accounts.read", p => p.RequireClaim("scope", "accounts.read"));
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(AnonymousRouteEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/v1/scoped-anonymous/item");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await app.StopAsync();
    }

    // --- Scenario: A group that declares nothing is unchanged ----------------------------------------------------
    // NOTE: green from the start by design (R2) — this scenario guards that adding the feature never changes a
    // group with no EndpointGroupScopeAttribute, so it is expected to already pass, not evidence of a wrong AT.

    [Fact]
    public async Task Scenario9_GroupDeclaringNoScope_IsUnchanged_TreasuryOpsSignedInWithNoScopeSucceeds()
    {
        var builder = CreateBuilder(o =>
        {
            o.Authenticated = true;
            o.UserName = "treasury-ops";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await app.StopAsync();
    }

    // --- Scenario: A host with authorization switched off serves every route ------------------------------------
    // NOTE: green from the start by design (R3) — RequireAuthorization = false skips reading the attribute
    // entirely (§3 row 4), so this scenario is expected to already pass.

    [Fact]
    public async Task Scenario10_AuthorizationSwitchedOff_UnknownCallerOnDeclaringGroupSucceeds()
    {
        var builder = CreateBuilder(o => o.Authenticated = false);
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.RequireAuthorization = false, typeof(ScopedReadWriteEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/v1/scoped-read-write/item");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await app.StopAsync();
    }
}
