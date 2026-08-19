using DKNet.Svc.Transformation;

// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides TransformSetup functionality.
/// </summary>
public static class TransformSetup
{
    #region Methods

    /// <summary>
    ///     AddTransformerService operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddTransformerService(
        this IServiceCollection services,
        Action<TransformOptions>? optionFactory = null)
    {
        if (services.Any(s => s.ServiceType == typeof(ITransformerService)))
            return services;

        var op = new TransformOptions();
        optionFactory?.Invoke(op);

        services.AddSingleton(Options.Options.Create(op));
        return services.AddTransient<ITransformerService, TransformerService>();
    }

    #endregion
}