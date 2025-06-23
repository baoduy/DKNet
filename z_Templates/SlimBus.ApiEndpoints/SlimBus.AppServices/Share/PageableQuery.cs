namespace SlimBus.AppServices.Share;

public record PageableQuery
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 100;
}