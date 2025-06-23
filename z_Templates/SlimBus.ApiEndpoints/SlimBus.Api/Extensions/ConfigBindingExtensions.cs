
namespace SlimBus.Api.Extensions;

public static class ConfigBindingExtensions
{
    public static TConfig Bind<TConfig>(this IConfiguration configuration, string name) where TConfig : class,new()
    {
        var rs = new TConfig();
        configuration.GetSection(name).Bind(rs);
        return rs;
    }
}