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

    public TResult Value => this.ValueOrDefault ?? throw new InvalidOperationException(nameof(this.ValueOrDefault));

    public TResult ValueOrDefault => this.GetValue()!;

    #endregion

    #region Methods

    private TResult? GetValue()
    {
        if (originalValue is null)
        {
            return default;
        }

        if (this._value is not null)
        {
            return this._value;
        }

        if (originalValue is TResult o)
        {
            this._value = o;
        }
        else
        {
            this._value = this._mapper.Map<TResult>(originalValue);
        }

        return this._value;
    }

    #endregion
}