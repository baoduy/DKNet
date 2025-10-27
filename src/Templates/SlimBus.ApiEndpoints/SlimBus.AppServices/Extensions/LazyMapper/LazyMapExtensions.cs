namespace SlimBus.AppServices.Extensions.LazyMapper;

public static class LazyMapExtensions
{
    #region Methods

    public static ILazyMap<TValue> LazyMap<TValue>(this IMapper mapper, object value) =>
        new LazyMap<TValue>(value, mapper);

    public static IResult<TValue> ResultOf<TValue>(this IMapper mapper, object value) =>
        new LazyResult<TValue>(value, mapper);

    #endregion
}