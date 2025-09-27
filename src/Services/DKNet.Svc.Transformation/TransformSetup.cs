using DKNet.Svc.Transformation;
using Microsoft.Extensions.Options;

// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global

namespace Microsoft.Extensions.DependencyInjection;

public static class TransformSetup
{
    public static IServiceCollection AddTransformerService(this IServiceCollection services,
        Action<TransformOptions>? optionFactory = null)
    {
        var op = new TransformOptions();
        optionFactory?.Invoke(op);

        services.AddSingleton(Options.Options.Create(op));
        return services.AddTransient<ITransformerService, TransformerService>();
    }
}