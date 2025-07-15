using X.PagedList;

namespace SlimBus.Api.Configs.Endpoints;

internal sealed record PagedResult<TResult>
{
    public PagedResult() => Items = [];

    public PagedResult(IPagedList<TResult> list)
    {
        PageNumber = list.PageNumber;
        PageSize = list.PageSize;
        PageCount = list.PageCount;
        TotalItemCount = list.TotalItemCount;
        Items = [.. list];
    }

    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int PageCount { get; init; }
    public int TotalItemCount { get; init; }
    public IList<TResult> Items { get; init; }
}