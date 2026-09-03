using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides PdfGeneratorSetup functionality.
/// </summary>
public static class PdfGeneratorSetup
{
    #region Methods

    /// <summary>
    ///     AddPdfGenerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddPdfGenerator(
        this IServiceCollection services,
        PdfGeneratorOptions? options = null)
    {
        // Registered via factory (rather than a pre-built instance) so the container tracks and disposes
        // the singleton - including the browser it holds - when the container shuts down.
        services.TryAddSingleton<IPdfGenerator>(_ => new PdfGenerator(options));
        return services;
    }

    #endregion
}