namespace SlimBus.AppServices.Extensions.LazyMapper;

public interface ILazyMap<out TResult>
{
    #region Properties

    TResult Value { get; }
    TResult? ValueOrDefault { get; }

    #endregion
}

internal class LazyMap<TResult>(object? originalValue, IMapper mapper) : ILazyMap<TResult>
{
    #region Fields

    private readonly IMapper _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    private TResult? _value;

    #endregion

    #region Properties

    public TResult Value => ValueOrDefault ?? throw new InvalidOperationException(nameof(ValueOrDefault));

    public TResult ValueOrDefault => GetValue()!;

    #endregion

    #region Methods

    private TResult? GetValue()
    {
        if (originalValue is null) return default;
        if (_value is not null) return _value;
        if (originalValue is TResult o) _value = o;
        else _value = _mapper.Map<TResult>(originalValue);
        return _value;
    }

    #endregion
}