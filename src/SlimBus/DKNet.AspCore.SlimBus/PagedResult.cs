using X.PagedList;

namespace DKNet.AspCore.SlimBus;

public sealed record PagedResult<TResult>
{
    #region Constructors

    public PagedResult() => this.Items = [];

    public PagedResult(IPagedList<TResult> list)
    {
        this.PageNumber = list.PageNumber;
        this.PageSize = list.PageSize;
        this.PageCount = list.PageCount;
        this.TotalItemCount = list.TotalItemCount;
        this.Items = [.. list];
    }

    #endregion

    #region Properties

    public IList<TResult> Items { get; init; }

    public int PageCount { get; init; }

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalItemCount { get; init; }

    #endregion
}