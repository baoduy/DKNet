using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using AspCore.Extensions.Tests.Fixtures;
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

    // --- Request filter: caller-supplied ByUser on [AsParameters] binding must never survive ------------------

    [Fact]
    public async Task RequestFilter_AsParametersBinding_AuthenticatedWithNoNameClaim_CallerSuppliedByUserIsIgnored()
    {
        var builder = CreateBuilder();
        // Authenticated (IsAuthenticated == true), but with no Name claim — e.g. a client-credentials /
        // machine-to-machine token. Before the fix, the filter's `if (userName is not null)` guard skipped
        // stamping in this case, letting whatever [AsParameters] bound from the querystring survive untouched.
        AddTestAuth(builder, o =>
        {
            o.Authenticated = true;
            o.UserName = null;
        });
        var app = builder.Build();
        app.UseEndpointConfigs(assemblies: typeof(ProbeEndpointConfig).Assembly);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/v1/probe/by-user-query?ByUser=attacker");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("(null)"); // caller-supplied "attacker" must never reach the handler
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
