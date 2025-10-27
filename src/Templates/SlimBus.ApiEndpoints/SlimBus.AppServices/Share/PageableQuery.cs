namespace SlimBus.AppServices.Share;

public record PageableQuery
{
    #region Properties

    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 100;

    #endregion
}