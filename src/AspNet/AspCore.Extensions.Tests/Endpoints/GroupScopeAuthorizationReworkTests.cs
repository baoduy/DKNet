using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Rework round 1 for the DRK-1547 review gate on DRK-1545 (findings 1 and 2). Not part of the frozen DRK-1542
///     §5 acceptance tests — a regression test per review finding, added the same way any post-freeze defect is
///     covered. Duplicates <see cref="EndpointGroupScopeTests" />'s host-building convention rather than sharing
///     it: that class's <c>CreateBuilder</c> is private to it.
/// </summary>
public class GroupScopeAuthorizationReworkTests
{
    private static WebApplicationBuilder CreateBuilder(Action<TestAuthSchemeOptions> configureAuth)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(GroupScopeAuthorizationReworkTests).Assembly)
            .AddChildBus(
                "Memory",
                mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(GroupScopeAuthorizationReworkTests).Assembly)));
        builder.Services.AddContextualRequestPopulation();
        builder.Services.AddAuthorization();
        builder.Services.AddApiVersioning();
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<TestAuthSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, configureAuth);
        return builder;
    }

    // --- Finding 1: a route serving a method through a second stacked IHttpMethodMetadata entry -----------------
    // (DRK-1547 review, GroupScopeAuthorization.cs:55) must still be covered, or refused. Reading only the first
    // (or only the last) IHttpMethodMetadata entry can miss a served method entirely.
    //
    // StackDeleteMetadata is an AsyncLocal set (and reset) only around this test's own synchronous
    // UseEndpointConfigs call — Map runs synchronously inside it, before any await — so every other host built
    // from this shared assembly discovers the fixture as a plain, fully-covered GET route (mirrors
    // ConditionallyUncoveredEndpointConfig's own remarks in EndpointConfigSupport.cs).

    [Fact]
    public async Task MethodWithNoDeclaredScope_ServedOnlyViaSecondStackedHttpMethodMetadataEntry_StopsHostAtStartup()
    {
        var builder = CreateBuilder(o => o.Authenticated = true);
        var app = builder.Build();
        StackedMethodMetadataEndpointConfig.StackDeleteMetadata.Value = true;
        try
        {
            app.UseEndpointConfigs(assemblies: typeof(StackedMethodMetadataEndpointConfig).Assembly);
        }
        finally
        {
            StackedMethodMetadataEndpointConfig.StackDeleteMetadata.Value = false;
        }

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync());

        exception.Message.ShouldContain("gate-stacked-metadata/item");
        exception.Message.ShouldContain("DELETE");
        await app.StopAsync();
    }

    // --- Finding 2: a verb-less route (no IHttpMethodMetadata at all) can never be covered (R4) and must refuse,
    // naming the method as "*" (DRK-1547 review, GroupScopeAuthorization.cs:60-63 — correct but untested).
    //
    // ServeVerblessRoute is gated the same way as StackDeleteMetadata above, for the same reason.

    [Fact]
    public async Task VerblessRoute_ServedByDeclaringGroup_StopsHostAtStartup_NamingRouteAndStar()
    {
        var builder = CreateBuilder(o => o.Authenticated = true);
        var app = builder.Build();
        VerblessRouteEndpointConfig.ServeVerblessRoute.Value = true;
        try
        {
            app.UseEndpointConfigs(assemblies: typeof(VerblessRouteEndpointConfig).Assembly);
        }
        finally
        {
            VerblessRouteEndpointConfig.ServeVerblessRoute.Value = false;
        }

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync());

        exception.Message.ShouldContain("gate-probe-verbless/item");
        exception.Message.ShouldContain("*");
        await app.StopAsync();
    }
}

/// <summary>
///     Declares "accounts.read" for GET only; its one GET route optionally carries a second, stacked
///     <see cref="HttpMethodMetadata" /> entry naming DELETE — served only when
///     <see cref="StackDeleteMetadata" /> is set for the calling flow.
/// </summary>
[EndpointGroupScope("accounts.read", EndpointHttpMethods.Get)]
public sealed class StackedMethodMetadataEndpointConfig : IEndpointConfig
{
    /// <summary>Set (and reset) only by the test exercising the stacked-metadata coverage gap.</summary>
    public static readonly AsyncLocal<bool> StackDeleteMetadata = new();

    public string GroupEndpoint => "/gate-stacked-metadata";

    public void Map(RouteGroupBuilder group)
    {
        var route = group.MapGet("/item", () => Results.Ok());
        if (StackDeleteMetadata.Value) route.WithMetadata(new HttpMethodMetadata(["DELETE"]));
    }
}

/// <summary>
///     Declares "ops.manage" for GET; optionally also serves a verb-less route no declaration can ever cover (R4)
///     — served only when <see cref="ServeVerblessRoute" /> is set for the calling flow.
/// </summary>
[EndpointGroupScope("ops.manage", EndpointHttpMethods.Get)]
public sealed class VerblessRouteEndpointConfig : IEndpointConfig
{
    /// <summary>Set (and reset) only by the test exercising the verb-less-route startup refusal.</summary>
    public static readonly AsyncLocal<bool> ServeVerblessRoute = new();

    public string GroupEndpoint => "/gate-probe-verbless";

    public void Map(RouteGroupBuilder group)
    {
        group.MapGet("/other", () => Results.Ok());
        if (ServeVerblessRoute.Value) group.Map("/item", () => Results.Ok());
    }
}
