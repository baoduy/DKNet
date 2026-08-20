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
///     Carries both <see cref="RequestBase.ByUser" /> and a validated <see cref="Name" /> — used to prove that a
///     host-restored attribution filter (via <see cref="EndpointRegistrationOptions.ConfigureGroup" />) runs before
///     a host-restored validation filter runs on the same request. <see cref="AttributedValidatedCommandValidator" />
///     rejects a request whose <see cref="RequestBase.ByUser" /> is still <see langword="null" /> at validation
///     time, so this command only reaches its handler when attribution really did happen first.
/// </summary>
public record AttributedValidatedCommand : RequestBase, Fluents.Requests.IWitResponse<WidgetResult>
{
    #region Properties

    public string Name { get; init; } = string.Empty;

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
///     Carries <see cref="RequestBase.ByUser" /> so tests can observe, via the response body, what
///     <c>EndpointConfigExtensions</c>' request filter stamped it to before dispatch.
/// </summary>
public record ByUserProbeCommand : RequestBase, Fluents.Requests.IWitResponse<WidgetResult>;

internal sealed class ByUserProbeHandler : Fluents.Requests.IHandler<ByUserProbeCommand, WidgetResult>
{
    #region Methods

    public Task<IResult<WidgetResult>> OnHandle(ByUserProbeCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(Result.Ok(new WidgetResult { Name = request.ByUser ?? "(null)" }));

    #endregion
}

/// <summary>
///     Same probe as <see cref="ByUserProbeCommand" /> but mapped with <c>[AsParameters]</c> binding (via
///     <c>MapGet&lt;TCommand,TResponse&gt;</c>) rather than JSON body binding — <c>[JsonIgnore]</c> on
///     <see cref="RequestBase.ByUser" /> has no effect on this binding source, so a caller can put
///     <c>?ByUser=...</c> straight on the querystring unless the host stamps over it unconditionally.
/// </summary>
public record ByUserQueryProbe : RequestBase, Fluents.Queries.IWitResponse<WidgetResult>;

internal sealed class ByUserQueryProbeHandler : Fluents.Queries.IHandler<ByUserQueryProbe, WidgetResult>
{
    #region Methods

    public Task<WidgetResult?> OnHandle(ByUserQueryProbe request, CancellationToken cancellationToken) =>
        Task.FromResult<WidgetResult?>(new WidgetResult { Name = request.ByUser ?? "(null)" });

    #endregion
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
