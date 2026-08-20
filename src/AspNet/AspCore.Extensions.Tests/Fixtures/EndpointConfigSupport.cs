using System.Security.Claims;
using DKNet.AspCore.Extensions;
using DKNet.SlimBus.Extensions;
using FluentResults;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>Command validated by <see cref="ValidatedCommandValidator" /> — proves the validation-gating option.</summary>
public record ValidatedCommand : Fluents.Requests.IWitResponse<WidgetResult>
{
    #region Properties

    public string Name { get; init; } = string.Empty;

    #endregion
}

public sealed class ValidatedCommandValidator : AbstractValidator<ValidatedCommand>
{
    #region Constructors

    public ValidatedCommandValidator() => RuleFor(x => x.Name).NotEmpty();

    #endregion
}

internal sealed class ValidatedCommandHandler : Fluents.Requests.IHandler<ValidatedCommand, WidgetResult>
{
    #region Methods

    public Task<IResult<WidgetResult>> OnHandle(ValidatedCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(Result.Ok(new WidgetResult { Name = request.Name }));

    #endregion
}

/// <summary>
///     Carries both its own <see cref="ByUser" /> and a validated <see cref="Name" /> — used to prove that a
///     host-restored attribution filter (via <see cref="EndpointRegistrationOptions.ConfigureGroup" />) runs before
///     a host-restored validation filter runs on the same request. <see cref="AttributedValidatedCommandValidator" />
///     rejects a request whose <see cref="ByUser" /> is still <see langword="null" /> at validation time, so this
///     command only reaches its handler when attribution really did happen first.
/// </summary>
public record AttributedValidatedCommand : Fluents.Requests.IWitResponse<WidgetResult>
{
    #region Properties

    public string Name { get; init; } = string.Empty;

    /// <summary>Declared via <see cref="FromClaimAttribute" /> — own property, no longer via <c>RequestBase</c> (DRK-565).</summary>
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    #endregion
}

public sealed class AttributedValidatedCommandValidator : AbstractValidator<AttributedValidatedCommand>
{
    #region Constructors

    public AttributedValidatedCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.ByUser).NotNull();
    }

    #endregion
}

internal sealed class AttributedValidatedCommandHandler
    : Fluents.Requests.IHandler<AttributedValidatedCommand, WidgetResult>
{
    #region Methods

    public Task<IResult<WidgetResult>> OnHandle(
        AttributedValidatedCommand request,
        CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(Result.Ok(new WidgetResult { Name = request.ByUser ?? "(null)" }));

    #endregion
}

/// <summary>
///     Carries its own <see cref="ByUser" /> so tests can observe, via the response body, what
///     <c>EndpointConfigExtensions</c>' request filter stamped it to before dispatch.
/// </summary>
public record ByUserProbeCommand : Fluents.Requests.IWitResponse<WidgetResult>
{
    /// <summary>Declared via <see cref="FromClaimAttribute" /> — own property, no longer via <c>RequestBase</c> (DRK-565).</summary>
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }
}

internal sealed class ByUserProbeHandler : Fluents.Requests.IHandler<ByUserProbeCommand, WidgetResult>
{
    #region Methods

    public Task<IResult<WidgetResult>> OnHandle(ByUserProbeCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(Result.Ok(new WidgetResult { Name = request.ByUser ?? "(null)" }));

    #endregion
}

/// <summary>
///     Same probe as <see cref="ByUserProbeCommand" /> but mapped with <c>[AsParameters]</c> binding (via
///     <c>MapGet&lt;TCommand,TResponse&gt;</c>) rather than JSON body binding — a caller can put
///     <c>?ByUser=...</c> straight on the querystring unless the host overwrites it unconditionally.
/// </summary>
public record ByUserQueryProbe : Fluents.Queries.IWitResponse<WidgetResult>
{
    /// <summary>Declared via <see cref="FromClaimAttribute" /> — own property, no longer via <c>RequestBase</c> (DRK-565).</summary>
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }
}

internal sealed class ByUserQueryProbeHandler : Fluents.Queries.IHandler<ByUserQueryProbe, WidgetResult>
{
    #region Methods

    public Task<WidgetResult?> OnHandle(ByUserQueryProbe request, CancellationToken cancellationToken) =>
        Task.FromResult<WidgetResult?>(new WidgetResult { Name = request.ByUser ?? "(null)" });

    #endregion
}

/// <summary>
///     Declares a non-string-typed member — proves a claim value that fails to convert to the property's own
///     type (e.g. a non-Guid string) leaves it at its type's default rather than throwing or rejecting the
///     request (DRK-565 population is never validation).
/// </summary>
public record GuidClaimCommand : Fluents.Requests.IWitResponse<WidgetResult>
{
    [FromClaim("tenant-id")]
    public Guid TenantId { get; set; }
}

internal sealed class GuidClaimCommandHandler : Fluents.Requests.IHandler<GuidClaimCommand, WidgetResult>
{
    public Task<IResult<WidgetResult>> OnHandle(GuidClaimCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(Result.Ok(new WidgetResult { Name = request.TenantId.ToString() }));
}

/// <summary>Two declared members, each sourced from its OWN claim type — proves both are populated independently.</summary>
public record MultiClaimCommand : Fluents.Requests.IWitResponse<WidgetResult>
{
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    [FromClaim("tenant-id")]
    public string? TenantId { get; set; }
}

internal sealed class MultiClaimCommandHandler : Fluents.Requests.IHandler<MultiClaimCommand, WidgetResult>
{
    public Task<IResult<WidgetResult>> OnHandle(MultiClaimCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(
            Result.Ok(new WidgetResult { Name = $"{request.ByUser ?? "(null)"}|{request.TenantId ?? "(null)"}" }));
}

/// <summary>
///     A second, host-defined <see cref="IContextualSource" /> kind (DRK-565 review finding 4) — proves that a
///     new declaration kind needs only its own attribute plus its own <see cref="IContextualValueResolver" />,
///     with no change to the mechanism itself. Resolved by <see cref="ScopedSecondSourceResolver" />, which is
///     registered as scoped by the tests that use it — doubling as end-to-end coverage that a host-registered
///     scoped resolver resolves on a real request (finding 3).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FromSecondSourceAttribute : Attribute, IContextualSource;

/// <summary>Depended on by <see cref="ScopedSecondSourceResolver" /> — a scoped service, the archetypal second-source shape (e.g. a tenant resolver over a <c>DbContext</c>).</summary>
public interface ISecondSourceProbe
{
    string Value { get; }
}

public sealed class SecondSourceProbe : ISecondSourceProbe
{
    public string Value => "second-source-value";
}

/// <summary>
///     Resolves <see cref="FromSecondSourceAttribute" /> via a scoped dependency — registered scoped by the
///     tests that use it, never singleton, so it fails under <c>ValidateScopes = true</c> if
///     <c>ContextualRequestPopulationService</c> were ever singleton again (finding 3's regression guard).
/// </summary>
public sealed class ScopedSecondSourceResolver(ISecondSourceProbe probe) : IContextualValueResolver
{
    public bool CanResolve(IContextualSource source) => source is FromSecondSourceAttribute;

    public string? Resolve(IContextualSource source, HttpContext httpContext) => probe.Value;
}

/// <summary>Declares one member per source kind — [FromClaim] (built-in) and [FromSecondSource] (host-defined) — proving both resolve independently on the same request.</summary>
public record MixedSourceCommand : Fluents.Requests.IWitResponse<WidgetResult>
{
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    [FromSecondSource]
    public string? Secondary { get; set; }
}

internal sealed class MixedSourceCommandHandler : Fluents.Requests.IHandler<MixedSourceCommand, WidgetResult>
{
    public Task<IResult<WidgetResult>> OnHandle(MixedSourceCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(
            Result.Ok(new WidgetResult { Name = $"{request.ByUser ?? "(null)"}|{request.Secondary ?? "(null)"}" }));
}

/// <summary>
///     Query-bound probe carrying a declared <see cref="ByUser" /> ALONGSIDE a non-declared sibling
///     <see cref="Name" /> — used to prove the OpenAPI operation-parameter transformer removes only the declared
///     one (<see cref="ByUserQueryProbe" /> has no non-declared sibling to make that distinction with).
/// </summary>
public record ByUserQueryProbeWithName : Fluents.Queries.IWitResponse<WidgetResult>
{
    public string Name { get; init; } = string.Empty;

    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }
}

internal sealed class ByUserQueryProbeWithNameHandler : Fluents.Queries.IHandler<ByUserQueryProbeWithName, WidgetResult>
{
    public Task<WidgetResult?> OnHandle(ByUserQueryProbeWithName request, CancellationToken cancellationToken) =>
        Task.FromResult<WidgetResult?>(new WidgetResult { Name = $"{request.Name}|{request.ByUser ?? "(null)"}" });
}

#pragma warning disable CS0618 // RequestBase is [Obsolete] (DRK-565) — this fixture intentionally exercises the
                               // pre-existing manual-stamping pattern to prove it still works after the attribute
                               // was added, not the new mechanism.
/// <summary>
///     Carries <see cref="RequestBase.ByUser" /> from the OLD pre-DRK-565 pattern — a host's own
///     <see cref="EndpointRegistrationOptions.ConfigureGroup" /> filter stamps it manually, the way every
///     <c>RequestBase</c> consumer did before <see cref="FromClaimAttribute" /> existed. Proves <c>[Obsolete]</c>
///     on <see cref="RequestBase" /> is advisory only — the old manual pattern still compiles and still works.
/// </summary>
public record LegacyByUserCommand : RequestBase, Fluents.Requests.IWitResponse<WidgetResult>
{
    public string Name { get; init; } = string.Empty;
}
#pragma warning restore CS0618

public sealed class LegacyByUserCommandValidator : AbstractValidator<LegacyByUserCommand>
{
    public LegacyByUserCommandValidator() => RuleFor(x => x.Name).NotEmpty();
}

internal sealed class LegacyByUserCommandHandler : Fluents.Requests.IHandler<LegacyByUserCommand, WidgetResult>
{
    public Task<IResult<WidgetResult>> OnHandle(LegacyByUserCommand request, CancellationToken cancellationToken) =>
#pragma warning disable CS0618
        Task.FromResult<IResult<WidgetResult>>(Result.Ok(new WidgetResult { Name = request.ByUser ?? "(null)" }));
#pragma warning restore CS0618
}

/// <summary>The single <see cref="IEndpointConfig" /> discovered by default (no explicit assemblies) in this test assembly.</summary>
public sealed class ProbeEndpointConfig : IEndpointConfig
{
    #region Properties

    public string GroupEndpoint => "/probe";

    public int Version => 1;

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        group.MapPost<ValidatedCommand, WidgetResult>("/validated");
        group.MapPost<ByUserProbeCommand, WidgetResult>("/by-user");
        group.MapGet<ByUserQueryProbe, WidgetResult>("/by-user-query");
        group.MapPost<AttributedValidatedCommand, WidgetResult>("/attributed-validated");
        group.MapPost<GuidClaimCommand, WidgetResult>("/guid-claim");
        group.MapPost<MultiClaimCommand, WidgetResult>("/multi-claim");
        group.MapPost<MixedSourceCommand, WidgetResult>("/mixed-source");
        group.MapGet<ByUserQueryProbeWithName, WidgetResult>("/by-user-query-with-name");
        group.MapPost<LegacyByUserCommand, WidgetResult>("/legacy-by-user");
    }

    #endregion
}

/// <summary>
///     Declares <see cref="Version" /> 2 — proves that a group declaring a version other than 1 still routes and
///     reports its version correctly once versioning is enabled (§5 "Versioned API keeps its versioned routes
///     after upgrading"). Uses its own <see cref="GroupEndpoint" /> rather than <see cref="ProbeEndpointConfig" />'s
///     so it does not collide with routes when a DIFFERENT test in this shared fixture assembly disables
///     versioning altogether (versioning off drops the version discriminator, and two groups sharing one route
///     text would then ambiguously match).
/// </summary>
public sealed class ProbeV2EndpointConfig : IEndpointConfig
{
    #region Properties

    public string GroupEndpoint => "/probe-versioned";

    public int Version => 2;

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group) => group.MapPost<ByUserProbeCommand, WidgetResult>("/by-user");

    #endregion
}

/// <summary>
///     States no <see cref="Version" /> override at all, relying on <see cref="IEndpointConfig" />'s default
///     interface member — proves a group declaring no version is treated as version 1 (§5).
/// </summary>
public sealed class UnversionedProbeEndpointConfig : IEndpointConfig
{
    #region Properties

    public string GroupEndpoint => "/unversioned-probe";

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group) => group.MapPost<ByUserProbeCommand, WidgetResult>("/by-user");

    #endregion
}

/// <summary>A second config with a custom <see cref="AuthPolicy" />, used by the per-policy authorization tests.</summary>
public sealed class PolicyGuardedEndpointConfig : IEndpointConfig
{
    public const string PolicyName = "CanConfigure";

    #region Properties

    public string? AuthPolicy => PolicyName;

    public string GroupEndpoint => "/guarded";

    public int Version => 1;

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group) => group.MapPost<ByUserProbeCommand, WidgetResult>("/by-user");

    #endregion
}

/// <summary>
///     A third config that explicitly overrides <see cref="Tag" /> to empty — exercising the
///     <see cref="EndpointRegistrationOptions.DefaultTag" /> fallback branch in
///     <c>EndpointConfigExtensions.MapEndpointConfig</c> (<see cref="ProbeEndpointConfig" /> and
///     <see cref="PolicyGuardedEndpointConfig" /> both resolve a non-empty tag from their default
///     <see cref="IEndpointConfig.Tag" /> implementation, so neither exercises this branch).
/// </summary>
public sealed class EmptyTagEndpointConfig : IEndpointConfig
{
    #region Properties

    public string GroupEndpoint => "/empty-tag";

    public string Tag => "";

    public int Version => 1;

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group) => group.MapPost<ByUserProbeCommand, WidgetResult>("/by-user");

    #endregion
}

/// <summary>Options for <see cref="TestAuthHandler" /> — set per-test, no shared/static state.</summary>
public sealed class TestAuthSchemeOptions : AuthenticationSchemeOptions
{
    #region Properties

    public bool Authenticated { get; set; }

    /// <summary>
    ///     Value of the authenticated identity's <see cref="System.Security.Claims.ClaimTypes.Name" /> claim.
    ///     <see langword="null" /> omits the claim entirely — simulating an authenticated principal that carries
    ///     no name (e.g. a client-credentials / machine-to-machine token), so <c>Identity.Name</c> is
    ///     <see langword="null" /> even though <c>Identity.IsAuthenticated</c> is <see langword="true" />.
    /// </summary>
    public string? UserName { get; set; } = "alice";

    public IEnumerable<System.Security.Claims.Claim> Claims { get; set; } = [];

    #endregion
}

/// <summary>Authentication handler whose outcome is fully controlled by <see cref="TestAuthSchemeOptions" /> — no real credential check.</summary>
public sealed class TestAuthHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<TestAuthSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<TestAuthSchemeOptions>(options, logger, encoder)
{
    #region Fields

    public const string SchemeName = "Test";

    #endregion

    #region Methods

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Options.Authenticated) return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<System.Security.Claims.Claim>();
        if (Options.UserName is not null)
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, Options.UserName));
        claims.AddRange(Options.Claims);
        var identity = new System.Security.Claims.ClaimsIdentity(claims, SchemeName);
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    #endregion
}
