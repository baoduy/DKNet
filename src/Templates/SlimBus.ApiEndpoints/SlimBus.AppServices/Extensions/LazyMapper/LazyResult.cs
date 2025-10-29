namespace SlimBus.AppServices.Extensions.LazyMapper;

internal class LazyResult<TResult>(object? originalValue, IMapper mapper)
    : LazyMap<TResult>(originalValue, mapper), IResult<TResult>
{
    #region Properties

    public bool IsFailed => this.Reasons.OfType<IError>().Any();

    public bool IsSuccess => !this.IsFailed;

    public IReadOnlyList<IError> Errors => [.. this.Reasons.OfType<IError>()];

    public IReadOnlyList<ISuccess> Successes => [.. this.Reasons.OfType<ISuccess>()];

    public List<IReason> Reasons { get; init; } = [];

    #endregion
}