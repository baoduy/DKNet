namespace SlimBus.AppServices.Extensions.LazyMapper;

internal class LazyResult<TResult>(object? originalValue, IMapper mapper)
    : LazyMap<TResult>(originalValue, mapper), IResult<TResult>
{
    public bool IsFailed
    {
        get => Reasons.OfType<IError>().Any();
    }

    public bool IsSuccess
    {
        get => !IsFailed;
    }

    public List<IReason> Reasons { get; init; } = [];

    public IReadOnlyList<IError> Errors
    {
        get => [.. Reasons.OfType<IError>()];
    }

    public IReadOnlyList<ISuccess> Successes
    {
        get => [.. Reasons.OfType<ISuccess>()];
    }
}