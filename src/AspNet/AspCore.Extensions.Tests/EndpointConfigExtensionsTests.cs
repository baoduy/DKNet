using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.SlimBus.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests;

/// <summary>
///     Exercises <c>WebApplication.UseEndpointConfigs</c> — discovery (default AppDomain scan vs. explicit
///     assemblies), <see cref="EndpointRegistrationOptions" /> overrides, the host-supplied
///     <see cref="EndpointRegistrationOptions.ConfigureGroup" /> per-group setup callback, the
///     <see cref="EndpointRegistrationOptions.EnableVersioning" /> switch, and authorization-on-by-default —
///     through real HTTP dispatch on a fresh TestServer per test (registration is a startup-time concern, so each
///     test needs its own host rather than sharing <see cref="Fixtures.EndpointTestHost" />).
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
        // UseEndpointConfigs discovers the whole test assembly, which always includes ProbeEndpointConfig's
        // [FromClaim]-declared ByUserProbeCommand — so every test built from this helper needs population
        // registered regardless of what that individual test is actually exercising (DRK-565 review finding 2's
        // fail-fast at endpoint-build time). Tests that specifically exercise the "never registered" case build
        // their own host instead of calling this helper.
        builder.Services.AddContextualRequestPopulation();
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
    public async Task UseEndpointConfigs_DefaultOptions_UsesVersionedRouteTemplate()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o => o.Authenticated = true);
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        // Default RouteTemplate is "/v{version:apiVersion}{GroupEndpoint}" -> "/v1/probe/by-user".
        var response = await client.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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

    [Fact]
    public async Task UseEndpointConfigs_RouteTemplateOverride_EnableVersioningFalse_HonoursCustomTemplateWithoutApiVersion()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o => o.Authenticated = true);
        var app = builder.Build();
        app.UseEndpointConfigs(
            o =>
            {
                o.RouteTemplate = config => $"/custom{config.GroupEndpoint}";
                o.EnableVersioning = false;
            },
            typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        // With versioning off, the host-supplied template is honoured verbatim and no api-version is required
        // in any form — mirrors the versioning-on override test above, but for the EnableVersioning = false path.
        var response = await client.PostAsJsonAsync("/custom/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("api-supported-versions").ShouldBeFalse();
        await app.StopAsync();
    }

    // --- Validation is no longer performed by the package ------------------------------------------------
    //     (attribution IS performed automatically for [FromClaim]-declared members once
    //     AddContextualRequestPopulation is registered — see ContextualRequestPopulationEndToEndTests
    //     .JsonBodyBoundCommand_ClaimPresent_HandlerObservesClaimValue — no ConfigureGroup wiring needed, DRK-565.)

    [Fact]
    public async Task NoConfigureGroup_AuthorizationOff_CarriesNoStandInAttribution()
    {
        var builder = CreateBuilder();
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.RequireAuthorization = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("(null)"); // stand-in account name setting is gone; no attribution is stamped at all
        await app.StopAsync();
    }

    [Fact]
    public async Task NoConfigureGroup_InvalidRequestBody_IsNotRejectedByThePackage()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o => o.Authenticated = true); // authorization stays on; authenticate so validation would be reached
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/v1/probe/validated", new ValidatedCommand { Name = "" });

        // No auto-validation is applied by the package any more; the empty-name body reaches the handler untouched.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("");
        await app.StopAsync();
    }

    // --- ConfigureGroup: host-supplied per-group setup -----------------------------------------------------

    [Fact]
    public async Task ConfigureGroup_InvokedForEveryDiscoveredGroup()
    {
        var builder = CreateBuilder();
        var app = builder.Build();
        var capturedTags = new List<string>();
        var groups = app.UseEndpointConfigs(
            o => o.ConfigureGroup = (_, config) => capturedTags.Add(config.Tag),
            typeof(ProbeEndpointConfig).Assembly);

        capturedTags.Count.ShouldBe(groups.Count); // callback fired exactly once per group, none skipped
        capturedTags.ShouldContain("probe");
        capturedTags.ShouldContain("guarded");
        await app.DisposeAsync();
    }

    [Fact]
    public async Task ConfigureGroup_AddedFilter_RunsBeforeHandler_RejectsFlaggedRequestsAllowsOthers()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o => o.Authenticated = true);
        var app = builder.Build();
        app.UseEndpointConfigs(
            o => o.ConfigureGroup = (group, _) => group.AddEndpointFilter(async (context, next) =>
                context.HttpContext.Request.Headers["X-Merchant"] == "blocked"
                    ? Results.StatusCode(StatusCodes.Status403Forbidden)
                    : await next(context)),
            typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var blockedRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/probe/by-user")
        {
            Content = JsonContent.Create(new ByUserProbeCommand())
        };
        blockedRequest.Headers.Add("X-Merchant", "blocked");
        var blockedResponse = await client.SendAsync(blockedRequest);

        var allowedRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/probe/by-user")
        {
            Content = JsonContent.Create(new ByUserProbeCommand())
        };
        allowedRequest.Headers.Add("X-Merchant", "trusted");
        var allowedResponse = await client.SendAsync(allowedRequest);

        // ConfigureGroup's filter ran ahead of the handler for both requests: the flagged one never reached it,
        // the trusted one did.
        blockedResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        allowedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        await app.StopAsync();
    }

    [Fact]
    public async Task ConfigureGroup_ContextualPopulationRegistered_PopulatesDeclaredMemberBeforeValidationRuns()
    {
        // AttributedValidatedCommand.ByUser now carries [FromClaim(ClaimTypes.Name)] (DRK-565) — no manual
        // stamping filter is registered here at all, only AddFluentValidationAutoValidation(). If population
        // did NOT run before validation, AttributedValidatedCommandValidator's "ByUser must not be null" rule
        // would reject every request, since nothing else ever sets ByUser.
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        builder.Services.AddValidatorsFromAssemblyContaining<AttributedValidatedCommandValidator>();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "jane.tan@acme.com";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(
            o => o.ConfigureGroup = (group, _) => group.AddFluentValidationAutoValidation(),
            typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/v1/probe/attributed-validated", new AttributedValidatedCommand { Name = "Acme Retail" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("jane.tan@acme.com");
        await app.StopAsync();
    }

    [Fact]
    public async Task ConfigureGroup_ContextualPopulationRegistered_ClaimMissing_ValidationRejectsRequest()
    {
        // Same wiring as above, but the authenticated caller carries no ClaimTypes.Name claim: population still
        // runs (leaving ByUser at its default, null) and the request is rejected by VALIDATION — proving
        // population never bypasses or substitutes for the application's own validation.
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        builder.Services.AddValidatorsFromAssemblyContaining<AttributedValidatedCommandValidator>();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = null;
        });
        var app = builder.Build();
        app.UseEndpointConfigs(
            o => o.ConfigureGroup = (group, _) => group.AddFluentValidationAutoValidation(),
            typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/v1/probe/attributed-validated", new AttributedValidatedCommand { Name = "Acme Retail" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await app.StopAsync();
    }

    [Fact]
    public async Task ConfigureGroup_ManualByUserStamping_RequestBaseBasedCommand_StampedByUserReachesHandler()
    {
        // Proves the OLD pre-DRK-565 pattern still works: RequestBase.ByUser stamped manually by a host's own
        // ConfigureGroup filter, ahead of a host-added validation filter — exactly as ConfigureGroup's docs
        // describe. [Obsolete] on RequestBase is advisory only; it does not break this existing consumer.
        var builder = CreateBuilder();
        builder.Services.AddValidatorsFromAssemblyContaining<LegacyByUserCommandValidator>();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "legacy.user@acme.com";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(
            o => o.ConfigureGroup = (group, _) =>
            {
                group.AddEndpointFilter(async (context, next) =>
                {
                    var identity = context.HttpContext.User.Identity;
                    var userName = identity is { IsAuthenticated: true } ? identity.Name : null;
                    foreach (var argument in context.Arguments)
#pragma warning disable CS0618 // RequestBase is [Obsolete] (DRK-565) — exercising the old manual pattern on purpose.
                        if (argument is LegacyByUserCommand legacyByUserCommand)
                            legacyByUserCommand.ByUser = userName;
#pragma warning restore CS0618
                    return await next(context);
                });
                group.AddFluentValidationAutoValidation();
            },
            typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/v1/probe/legacy-by-user", new LegacyByUserCommand { Name = "Acme Retail" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("legacy.user@acme.com");
        await app.StopAsync();
    }

    [Fact]
    public async Task AsParametersBinding_DeclaredByUser_CallerSuppliedQueryValueIsOverwrittenByResolvedClaim()
    {
        // ByUserQueryProbe.ByUser now carries [FromClaim(ClaimTypes.Name)] (DRK-565): once AddContextualRequestPopulation
        // is registered, a caller-supplied ?ByUser=... on the querystring is no longer trustworthy input — it is
        // always overwritten by the resolved claim, unlike the pre-DRK-565 pass-through behaviour this test used
        // to assert.
        var builder = CreateBuilder();
        builder.Services.AddContextualRequestPopulation();
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = "jane.tan@acme.com";
        });
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/v1/probe/by-user-query?ByUser=whatever-the-caller-sends");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("jane.tan@acme.com"); // never "whatever-the-caller-sends"
        await app.StopAsync();
    }

    // --- Contextual population fail-fast (DRK-565 review finding 2) ------------------------------------------

    [Fact]
    public async Task DeclaredMemberMapped_AddContextualRequestPopulationNeverCalled_StartAsyncThrowsNamingType()
    {
        // ByUserProbeCommand.ByUser carries [FromClaim]; the host below never calls AddContextualRequestPopulation
        // (deliberately not using CreateBuilder(), which registers it for every other test in this file).
        // Without the fail-fast, the caller-supplied value would reach the handler untouched — exactly the
        // exposure [FromClaim]'s own doc comment implies is guarded.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(EndpointConfigExtensionsTests).Assembly)
            .AddChildBus(
                "Memory",
                mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(EndpointConfigExtensionsTests).Assembly)));
        builder.Services.AddAuthorization();
        AddTestAuth(builder, o => o.Authenticated = true);
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync());

        exception.Message.ShouldContain(nameof(ByUserProbeCommand));
        exception.Message.ShouldContain(
            nameof(ContextualRequestPopulationServiceCollectionExtensions.AddContextualRequestPopulation));
        await app.StopAsync();
    }

    // R3 ("a host with no declared members anywhere still starts normally") is covered at the scanner level by
    // ContextualRequestPopulationTests.GetDeclaredMembers_TypeWithNoDeclaredMembers_ReturnsEmptyArray: the guard
    // above is `members.Length > 0 && ...`, so a type that scans to zero members short-circuits before ever
    // touching IContextualRequestPopulationService. This test fixture's assembly-wide discovery always has some
    // other IEndpointConfig mapping a declared-member command, so a same-host end-to-end negative case would
    // exercise ProbeEndpointConfig's own registration rather than isolate the no-declaration case.

    // --- Versioning switch ----------------------------------------------------------------------------------

    [Fact]
    public async Task EnableVersioning_On_NoApiVersioningServicesRegistered_ThrowsWithDiagnostic()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthorization();
        // Deliberately no AddApiVersioning() call.
        var app = builder.Build();

        var exception = Should.Throw<InvalidOperationException>(
            () => app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly));

        exception.Message.ShouldContain("AddApiVersioning()");
        exception.Message.ShouldContain(nameof(EndpointRegistrationOptions.EnableVersioning));
        await app.DisposeAsync();
    }

    [Fact]
    public async Task EnableVersioning_Off_RoutesCarryNoVersionSegmentAndReportNoApiVersion()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o => o.Authenticated = true); // authorization stays on by default, unaffected by the versioning switch
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.EnableVersioning = false, typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        // Default RouteTemplate with versioning off is "{GroupEndpoint}" -> no "/v{version}" segment.
        var response = await client.PostAsJsonAsync("/probe/by-user", new ByUserProbeCommand());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("api-supported-versions").ShouldBeFalse();
        await app.StopAsync();
    }

    [Fact]
    public async Task EnableVersioning_On_GroupDeclaringVersion2_RoutesUnderV2AndReportsSupportedVersions()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o => o.Authenticated = true);
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly); // discovers Probe (v1) and ProbeV2 (v2)
        await app.StartAsync();
        using var client = app.GetTestClient();

        var v1Response = await client.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());
        var v2Response = await client.PostAsJsonAsync("/v2/probe-versioned/by-user", new ByUserProbeCommand());

        v1Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        v2Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        v1Response.Headers.Contains("api-supported-versions").ShouldBeTrue();
        await app.StopAsync();
    }

    [Fact]
    public async Task Version_NotDeclared_RoutesAndDisplaysIdenticallyToExplicitVersion1()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o => o.Authenticated = true);
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(UnversionedProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        // Same version prefix ("/v1/...") as an explicitly-declared version 1 group -> the default interface
        // member fell back to 1, exactly as an explicit `Version => 1` would have routed.
        var response = await client.PostAsJsonAsync("/v1/unversioned-probe/by-user", new ByUserProbeCommand());
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var endpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.EndsWith("/unversioned-probe/by-user", StringComparison.Ordinal));
        // WithDisplayName($"v{config.Version}{config.GroupEndpoint}") -> identical shape to an explicit Version=1 config.
        endpoint.DisplayName.ShouldBe("v1/unversioned-probe");
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

    // --- Two hosts in one process configure independently -----------------------------------------------------

    [Fact]
    public async Task UseEndpointConfigs_TwoHostsInSameProcess_StrictHostStartedFirst_EachKeepsItsOwnAuthorizationSetting()
    {
        var strictBuilder = CreateBuilder();
        AddTestAuth(strictBuilder, o => o.Authenticated = false);
        var strictApp = strictBuilder.Build();
        strictApp.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly); // default: RequireAuthorization = true
        await strictApp.StartAsync();
        using var strictClient = strictApp.GetTestClient();

        var permissiveBuilder = CreateBuilder();
        var permissiveApp = permissiveBuilder.Build();
        permissiveApp.UseEndpointConfigs(o => o.RequireAuthorization = false, typeof(ProbeEndpointConfig).Assembly);
        await permissiveApp.StartAsync();
        using var permissiveClient = permissiveApp.GetTestClient();

        var strictResponse = await strictClient.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());
        var permissiveResponse = await permissiveClient.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());

        strictResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        permissiveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await strictApp.StopAsync();
        await permissiveApp.StopAsync();
    }

    [Fact]
    public async Task UseEndpointConfigs_TwoHostsInSameProcess_PermissiveHostStartedFirst_EachKeepsItsOwnAuthorizationSetting()
    {
        // Same assertion as the previous test with construction/start order reversed — proves the two hosts'
        // options are independent registration-time state, not shared statics that only happen to work in one order.
        var permissiveBuilder = CreateBuilder();
        var permissiveApp = permissiveBuilder.Build();
        permissiveApp.UseEndpointConfigs(o => o.RequireAuthorization = false, typeof(ProbeEndpointConfig).Assembly);
        await permissiveApp.StartAsync();
        using var permissiveClient = permissiveApp.GetTestClient();

        var strictBuilder = CreateBuilder();
        AddTestAuth(strictBuilder, o => o.Authenticated = false);
        var strictApp = strictBuilder.Build();
        strictApp.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        await strictApp.StartAsync();
        using var strictClient = strictApp.GetTestClient();

        var permissiveResponse = await permissiveClient.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());
        var strictResponse = await strictClient.PostAsJsonAsync("/v1/probe/by-user", new ByUserProbeCommand());

        permissiveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        strictResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await permissiveApp.StopAsync();
        await strictApp.StopAsync();
    }

    // --- Startup diagnostics reach the host log, not the console ------------------------------------------------

    [Fact]
    public async Task UseEndpointConfigs_DiscoveryDiagnostic_LogsToHostLoggerAndNeverToConsole()
    {
        var builder = CreateBuilder();
        builder.Logging.ClearProviders();
        var capturedLogs = new List<string>();
        builder.Logging.AddProvider(new CapturingLoggerProvider(capturedLogs));
        var app = builder.Build();

        var originalConsoleOut = Console.Out;
        var capturedConsole = new StringWriter();
        Console.SetOut(capturedConsole);
        try
        {
            app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        }
        finally
        {
            Console.SetOut(originalConsoleOut);
        }

        capturedLogs.ShouldContain(m => m.Contains("discovered", StringComparison.OrdinalIgnoreCase) &&
                                         m.Contains("endpoint configuration", StringComparison.OrdinalIgnoreCase));
        capturedConsole.ToString().ShouldBeEmpty();
        await app.DisposeAsync();
    }

    private sealed class CapturingLoggerProvider(List<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                messages.Add(formatter(state, exception));
        }
    }

    // --- A consumer's own request filter runs before her handler -----------------------------------------------

    [Fact]
    public async Task ConsumerRegisteredFilter_OnReturnedGroup_RunsBeforeHandler_RejectsFlaggedRequestsAllowsOthers()
    {
        var builder = CreateBuilder();
        AddTestAuth(builder, o => o.Authenticated = true);
        var app = builder.Build();
        var groups = app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        // A consumer wires her own filter onto the group builder UseEndpointConfigs handed back — it must run
        // before ByUserProbeHandler ever executes, so a rejected request never reaches the handler at all.
        foreach (var group in groups)
            group.AddEndpointFilter(async (context, next) =>
                context.HttpContext.Request.Headers["X-Merchant"] == "blocked"
                    ? Results.StatusCode(StatusCodes.Status403Forbidden)
                    : await next(context));
        await app.StartAsync();
        using var client = app.GetTestClient();

        var blockedRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/probe/by-user")
        {
            Content = JsonContent.Create(new ByUserProbeCommand())
        };
        blockedRequest.Headers.Add("X-Merchant", "blocked");
        var blockedResponse = await client.SendAsync(blockedRequest);

        var allowedRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/probe/by-user")
        {
            Content = JsonContent.Create(new ByUserProbeCommand())
        };
        allowedRequest.Headers.Add("X-Merchant", "trusted");
        var allowedResponse = await client.SendAsync(allowedRequest);

        blockedResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        allowedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var allowedBody = await allowedResponse.Content.ReadFromJsonAsync<WidgetResult>();
        allowedBody.ShouldNotBeNull();
        allowedBody.Name.ShouldBe("alice"); // proves dispatch reached the real handler for the non-rejected request

        await app.StopAsync();
    }

    // --- Resolved grouping tag, including the DefaultTag fallback -----------------------------------------------

    [Fact]
    public async Task UseEndpointConfigs_ConfigWithNonEmptyTag_ResolvesItsOwnTagNotDefaultTag()
    {
        var builder = CreateBuilder();
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.DefaultTag = "Fallback", typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();

        var tags = GetTags(app, "/probe/by-user");

        await app.StopAsync();
        tags.ShouldContain("probe"); // ProbeEndpointConfig's default Tag impl: GroupEndpoint "/probe" -> "probe"
        tags.ShouldNotContain("Fallback");
    }

    [Fact]
    public async Task UseEndpointConfigs_ConfigWithEmptyTag_FallsBackToDefaultTagOption()
    {
        var builder = CreateBuilder();
        var app = builder.Build();
        app.UseEndpointConfigs(o => o.DefaultTag = "Fallback", typeof(EmptyTagEndpointConfig).Assembly);
        await app.StartAsync();

        var tags = GetTags(app, "/empty-tag/by-user");

        await app.StopAsync();
        tags.ShouldContain("Fallback");
    }

    /// <summary>
    ///     Finds the mapped endpoint whose route ends with <paramref name="routeSuffix" /> (e.g. <c>/probe/by-user</c>)
    ///     and returns its resolved OpenAPI tags. Matches by suffix rather than the full raw pattern because
    ///     <see cref="Microsoft.AspNetCore.Routing.Patterns.RoutePattern.RawText" /> preserves the unresolved
    ///     <c>{version:apiVersion}</c> placeholder literally, not the version value substituted at request time.
    /// </summary>
    private static HashSet<string> GetTags(WebApplication app, string routeSuffix)
    {
        var endpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.EndsWith(routeSuffix, StringComparison.Ordinal));
        return [.. endpoint.Metadata.OfType<ITagsMetadata>().SelectMany(m => m.Tags!)];
    }

    #endregion
}
