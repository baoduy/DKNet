namespace SlimBus.AppServices.Extensions.LazyMapper;

internal class LazyResult<TResult>(object? originalValue, IMapper mapper)
    : LazyMap<TResult>(originalValue, mapper), IResult<TResult>
{
    public bool IsFailed => Reasons.OfType<IError>().Any();
    public bool IsSuccess => !IsFailed;

    public List<IReason> Reasons { get; init; } = [];

    public List<IError> Errors => [.. Reasons.OfType<IError>()];

    public List<ISuccess> Successes => [.. Reasons.OfType<ISuccess>()];
}