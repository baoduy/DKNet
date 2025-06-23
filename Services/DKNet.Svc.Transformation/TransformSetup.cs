using DKNet.Svc.Transformation;

// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global

namespace Microsoft.Extensions.DependencyInjection;

public static class TransformSetup
{
    public static IServiceCollection AddTransformerService(this IServiceCollection services, Action<TransformOptions>? optionFactory = null)
        => services.AddTransient<ITransformerService>(p =>
        {
            var op = new TransformOptions();
            optionFactory?.Invoke(op);

            return new TransformerService(op);
        });
}