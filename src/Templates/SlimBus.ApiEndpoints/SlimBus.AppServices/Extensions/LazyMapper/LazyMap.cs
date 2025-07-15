namespace SlimBus.AppServices.Extensions.LazyMapper;

public interface ILazyMap<out TResult>
{
    TResult? ValueOrDefault { get; }
    TResult Value { get; }
}

internal class LazyMap<TResult>(object? originalValue, IMapper mapper) : ILazyMap<TResult>
{
    private readonly IMapper _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    private TResult? _value;

    public TResult ValueOrDefault
    {
        get => GetValue()!;
    }

    public TResult Value
    {
        get => ValueOrDefault ?? throw new InvalidOperationException(nameof(ValueOrDefault));
    }

    private TResult? GetValue()
    {
        if (originalValue is null) return default;
        if (_value is not null) return _value;
        if (originalValue is TResult o) _value = o;
        else _value = _mapper.Map<TResult>(originalValue);
        return _value;
    }
}