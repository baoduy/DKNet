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
        // PdfGenerator's constructor is cheap (stores options, builds a MarkdownPipelineBuilder) - no I/O -
        // so constructing it unconditionally here is fine even when TryAddSingleton discards it as a duplicate.
        services.TryAddSingleton<IPdfGenerator>(new PdfGenerator(options));
        return services;
    }

    #endregion
}