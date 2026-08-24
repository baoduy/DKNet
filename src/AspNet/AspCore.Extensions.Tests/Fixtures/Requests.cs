using DKNet.SlimBus.Extensions;
using FluentResults;
using X.PagedList;

namespace AspCore.Extensions.Tests.Fixtures;

// Fluent request/query types + handlers used to exercise real SlimMessageBus dispatch through the endpoint
// mappers under test. Kept intentionally simple (name-only payload) — the point is proving the mapper wires
// HTTP verb/route -> bus.Send -> HTTP response correctly, not exercising business logic.

public record WidgetResult
{
    #region Properties

    public string Name { get; init; } = string.Empty;

    #endregion
}

/// <summary>Command whose type name contains "Create" — mapped POSTs must answer 201.</summary>
public record CreateWidgetCommand : Fluents.Requests.IWitResponse<WidgetResult>
{
    #region Properties

    public string Name { get; init; } = string.Empty;

    #endregion
}

/// <summary>Command whose type name does not contain "Create" — mapped POSTs must answer 200.</summary>
public record RenameWidgetCommand : Fluents.Requests.IWitResponse<WidgetResult>
{
    #region Properties

    public string Name { get; init; } = string.Empty;

    #endregion
}

/// <summary>Always fails, to exercise the shared ProblemDetails error path.</summary>
public record FailingWidgetCommand : Fluents.Requests.IWitResponse<WidgetResult>
{
    #region Properties

    public string Reason { get; init; } = "boom";

    #endregion
}

/// <summary>No-response command that always succeeds.</summary>
public record EchoNoResponseCommand : Fluents.Requests.INoResponse;

/// <summary>No-response command whose type name contains "Create" — mapped POSTs must answer 201.</summary>
public record CreateNoResponseCommand : Fluents.Requests.INoResponse;

/// <summary>No-response command that always fails.</summary>
public record FailingNoResponseCommand : Fluents.Requests.INoResponse;

/// <summary>Query returning null when <see cref="Found" /> is false, to exercise the 404 branch.</summary>
public record FindWidgetQuery : Fluents.Queries.IWitResponse<WidgetResult>
{
    #region Properties

    public bool Found { get; init; }

    public string Name { get; init; } = string.Empty;

    #endregion
}

/// <summary>Paged query returning a fixed, known page shape.</summary>
public record ListWidgetsPageQuery : Fluents.Queries.IWitPageResponse<WidgetResult>;

/// <summary>Command bound via MapPutById — <see cref="Id" /> is overwritten from the route, never the body.</summary>
public sealed record RenameThingRequest : Fluents.Requests.IWitResponse<string>, Fluents.Requests.IWithKey<Guid>
{
    #region Properties

    public Guid Id { get; set; }

    public string Name { get; init; } = string.Empty;

    #endregion
}

internal sealed class CreateWidgetHandler : Fluents.Requests.IHandler<CreateWidgetCommand, WidgetResult>
{
    #region Methods

    public Task<IResult<WidgetResult>> OnHandle(CreateWidgetCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(Result.Ok(new WidgetResult { Name = request.Name }));

    #endregion
}

internal sealed class RenameWidgetHandler : Fluents.Requests.IHandler<RenameWidgetCommand, WidgetResult>
{
    #region Methods

    public Task<IResult<WidgetResult>> OnHandle(RenameWidgetCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(Result.Ok(new WidgetResult { Name = request.Name }));

    #endregion
}

internal sealed class FailingWidgetHandler : Fluents.Requests.IHandler<FailingWidgetCommand, WidgetResult>
{
    #region Methods

    public Task<IResult<WidgetResult>> OnHandle(FailingWidgetCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<WidgetResult>>(Result.Fail<WidgetResult>(request.Reason));

    #endregion
}

internal sealed class EchoNoResponseHandler : Fluents.Requests.IHandler<EchoNoResponseCommand>
{
    #region Methods

    public Task<IResultBase> OnHandle(EchoNoResponseCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResultBase>(Result.Ok());

    #endregion
}

internal sealed class CreateNoResponseHandler : Fluents.Requests.IHandler<CreateNoResponseCommand>
{
    #region Methods

    public Task<IResultBase> OnHandle(CreateNoResponseCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResultBase>(Result.Ok());

    #endregion
}

internal sealed class FailingNoResponseHandler : Fluents.Requests.IHandler<FailingNoResponseCommand>
{
    #region Methods

    public Task<IResultBase> OnHandle(FailingNoResponseCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResultBase>(Result.Fail("no-response-boom"));

    #endregion
}

internal sealed class FindWidgetHandler : Fluents.Queries.IHandler<FindWidgetQuery, WidgetResult>
{
    #region Methods

    public Task<WidgetResult?> OnHandle(FindWidgetQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(request.Found ? new WidgetResult { Name = request.Name } : null);

    #endregion
}

internal sealed class ListWidgetsPageHandler : Fluents.Queries.IPageHandler<ListWidgetsPageQuery, WidgetResult>
{
    #region Methods

    public Task<IPagedList<WidgetResult>> OnHandle(ListWidgetsPageQuery request, CancellationToken cancellationToken)
    {
        IPagedList<WidgetResult> page = new StaticPagedList<WidgetResult>(
            [new WidgetResult { Name = "a" }, new WidgetResult { Name = "b" }],
            pageNumber: 1,
            pageSize: 2,
            totalItemCount: 5);
        return Task.FromResult(page);
    }

    #endregion
}

internal sealed class RenameThingHandler : Fluents.Requests.IHandler<RenameThingRequest, string>
{
    #region Methods

    public Task<IResult<string>> OnHandle(RenameThingRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<string>>(Result.Ok($"{request.Id}:{request.Name}"));

    #endregion
}
