using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AspCore.Extensions.Tests.Fixtures;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests;

/// <summary>
///     Exercises <c>WebApplication.UseEndpointConfigs</c> — discovery (default AppDomain scan vs. explicit
///     assemblies), <see cref="EndpointRegistrationOptions" /> overrides, the request filter that stamps
///     <see cref="DKNet.SlimBus.Extensions.RequestBase.ByUser" />, and authorization-on-by-default — through real
///     HTTP dispatch on a fresh TestServer per test (registration is a startup-time concern, so each test needs its
///     own host rather than sharing <see cref="Fixtures.EndpointTestHost" />).
/// </summary>
public class EndpointConfigExtensionsTests
{
    #region Methods

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(EndpointConfigExtensionsTests).Assembly)
            .AddChildBus(
                "Memory",
                mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(EndpointConfigExtensionsTests).Assembly)));
        builder.Services.AddAuthorization();
        builder.Services.AddApiVersioning();
        return builder;
    }

    private static void AddTestAuth(WebApplicationBuilder builder, Action<TestAuthSchemeOptions> configure)
    {
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<TestAuthSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, configure);
    }

    // --- Discovery -------------------------------------------------------------------------------------------

    [Fact]
    public async Task UseEndpointConfigs_NoAssembliesGiven_DiscoversConfigsFromCurrentAppDomain()
    {
        var builder = CreateBuilder();
        var app = builder.Build();

        var groups = app.UseEndpointConfigs();

        groups.ShouldNotBeEmpty();
        // Both this test assembly's IEndpointConfig implementations must be found via the AppDomain-wide default
        // scan — this is what lets a CONSUMING application's own IEndpointConfig declarations be picked up
        // without the package needing to know about them.
        groups.Count.ShouldBeGreaterThanOrEqualTo(2);
        await app.DisposeAsync();
    }

    [Fact]
    public async Task UseEndpointConfigs_ExplicitAssemblies_ScopesDiscoveryToThoseAssemblies()
    {
        var builder = CreateBuilder();
        var app = builder.Build();

        // The package's own assembly declares the IEndpointConfig interface but no implementations of it.
        var groups = app.UseEndpointConfigs(assemblies: typeof(IEndpointConfig).Assembly);

        groups.ShouldBeEmpty();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task UseEndpointConfigs_NoConfigsDiscovered_ReturnsEmptyList()
    {
        var builder = CreateBuilder();
        var app = builder.Build();

        var groups = app.UseEndpointConfigs(assemblies: typeof(string).Assembly);

        groups.ShouldNotBeNull();
        groups.ShouldBeEmpty();
        await app.DisposeAsync();
    }

    // --- Options: route template + tag override --------------------------------------------------------------

    [Fact]
    public async Task UseEndpointConfigs_DefaultOptions_UsesVersionedRouteTemplateAndAuthenticatedByUser()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "carol";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        // Default RouteTemplate is "/v{version:apiVersion}{GroupEndpoint}" -> "/v1/probe/by-user".
        var response = await client.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("carol");

        await app.StopAsync();
    }

    [Fact]
    public async Task UseEndpointConfigs_RouteTemplateOverride_MapsGroupsUnderCustomPrefix()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o => o.Authenticated = true);
        var app = builder.Build();
        app.UseEndpointConfigs(
            o => o.RouteTemplate = config => $"/custom{config.GroupEndpoint}",
            typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        // The override drops the "{version:apiVersion}" URL segment, so the api-version reader falls back to its
        // other sources (query string here) to resolve the version the group's HasApiVersion(1) still requires.
        var response = await client.PostAsJsonAsync(
            "/custom/probe/by-user?api-version=1.0", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await app.StopAsync();
    }

    // --- Options: validation gating -----------------------------------------------------------------------

    [Fact]
    public async Task UseEndpointConfigs_ValidationEnabled_RejectsInvalidRequestBody()
    {
        var builder = CreateBuilder();
        builder.Services.AddValidatorsFromAssemblyContaining<ValidatedCommandValidator>();
        builder.Services.AddFluentValidationAutoValidation();
        AddTestAuth(builder, o => o.Authenticated = true);
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableRequestValidation = true, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/probe/validated", new ValidatedCommand { Name = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await app.StopAsync();
    }

    [Fact]
    public async Task UseEndpointConfigs_ValidationDisabled_LetsInvalidRequestBodyThrough()
    {
        var builder = CreateBuilder();
        builder.Services.AddValidatorsFromAssemblyContaining<ValidatedCommandValidator>();
        builder.Services.AddFluentValidationAutoValidation();
        AddTestAuth(builder, o => o.Authenticated = true);
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableRequestValidation = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/probe/validated", new ValidatedCommand { Name = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await app.StopAsync();
    }

    // --- Request filter: ByUser stamping -------------------------------------------------------------------

    [Fact]
    public async Task RequestFilter_AuthenticatedUser_StampsByUserFromPrincipal()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "dave";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());

        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("dave");
        await app.StopAsync();
    }

    [Fact]
    public async Task RequestFilter_AuthorizationDisabled_StampsByUserFromSystemAccountName()
    {
        var builder = CreateBuilder();
        var app = builder.Build();
        app.UseEndpointConfigs(
            o =>
            {
                o.RequireAuthorization = false;
                o.SystemAccountName = "svc-account";
            },
            typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("svc-account");
        await app.StopAsync();
    }

    // --- Authorization: on by default, per-policy ---------------------------------------------------------

    [Fact]
    public async Task Authorization_DefaultOn_UnauthenticatedRequest_Returns401()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o => o.Authenticated = false);
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await app.StopAsync();
    }

    [Fact]
    public async Task Authorization_RequireAuthorizationFalse_UnauthenticatedRequest_Returns200()
    {
        var builder = CreateBuilder();
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.RequireAuthorization = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await app.StopAsync();
    }

    [Fact]
    public async Task Authorization_PerConfigAuthPolicy_AuthenticatedWithoutRequiredClaim_Returns403()
    {
        var builder = CreateBuilder();
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(PolicyGuardedEndpointConfig.PolicyName, p => p.RequireClaim("can-configure"));
        AddTestAuth(builder, o => o.Authenticated = true); // authenticated, but lacks the "can-configure" claim
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(PolicyGuardedEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/guarded/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await app.StopAsync();
    }

    [Fact]
    public async Task Authorization_PerConfigAuthPolicy_AuthenticatedWithRequiredClaim_Returns200()
    {
        var builder = CreateBuilder();
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(PolicyGuardedEndpointConfig.PolicyName, p => p.RequireClaim("can-configure"));
        AddTestAuth(
            builder,
            o =>
            {
                o.Authenticated = true;
                o.Claims = [new Claim("can-configure", "true")];
            });
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(PolicyGuardedEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/guarded/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await app.StopAsync();
    }

    #endregion
}
