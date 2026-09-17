using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Routing;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>
///     Declares TWO independently-named <see cref="FromRequestHeaderAttribute" /> members on the same request
///     type — supplementary coverage (not part of the frozen DRK-1524 §5 acceptance tests) proving
///     <c>ContextualSourceOperationTransformer</c> advertises every declared header parameter rather than only
///     the first, i.e. it never resets <c>operation.Parameters</c> between additions.
/// </summary>
public record TwoHeaderProbeCommand : Fluents.Requests.IWitResponse<WidgetResult>
{
    [FromRequestHeader("X-First")]
    public string? First { get; set; }

    [FromRequestHeader("X-Second")]
    public string? Second { get; set; }
}

internal sealed class TwoHeaderProbeHandler : Fluents.Requests.IHandler<TwoHeaderProbeCommand, WidgetResult>
{
    #region Methods

    public Task<IResult<WidgetResult>> OnHandle(TwoHeaderProbeCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(
            Result.Ok(new WidgetResult { Name = $"{request.First ?? "(null)"}|{request.Second ?? "(null)"}" }));

    #endregion
}

/// <summary>Own <see cref="IEndpointConfig" /> so it does not collide with <c>ProbeEndpointConfig</c>'s routes.</summary>
public sealed class MultiHeaderProbeEndpointConfig : IEndpointConfig
{
    #region Properties

    public string GroupEndpoint => "/probe-multi-header";

    public int Version => 1;

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group) => group.MapPost<TwoHeaderProbeCommand, WidgetResult>("/two-headers");

    #endregion
}
