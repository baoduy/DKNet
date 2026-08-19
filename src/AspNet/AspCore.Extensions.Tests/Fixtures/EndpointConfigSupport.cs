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
    }

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

/// <summary>Options for <see cref="TestAuthHandler" /> — set per-test, no shared/static state.</summary>
public sealed class TestAuthSchemeOptions : AuthenticationSchemeOptions
{
    #region Properties

    public bool Authenticated { get; set; }

    public string UserName { get; set; } = "alice";

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

        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.Name, Options.UserName)
        };
        claims.AddRange(Options.Claims);
        var identity = new System.Security.Claims.ClaimsIdentity(claims, SchemeName);
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    #endregion
}
