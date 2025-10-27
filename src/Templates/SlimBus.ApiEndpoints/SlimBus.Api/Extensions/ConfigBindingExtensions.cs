namespace SlimBus.Api.Extensions;

internal static class ConfigBindingExtensions
{
    #region Methods

    public static TConfig Bind<TConfig>(this IConfiguration configuration, string name) where TConfig : class, new()
    {
        var rs = new TConfig();
        configuration.GetSection(name).Bind(rs);
        return rs;
    }

    #endregion
}