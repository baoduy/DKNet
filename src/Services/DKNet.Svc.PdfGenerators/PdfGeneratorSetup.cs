using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;

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
    public static IServiceCollection AddPdfGenerator(
        this IServiceCollection services,
        PdfGeneratorOptions? options = null)
    {
        services.AddSingleton<IPdfGenerator>(new PdfGenerator(options));
        return services;
    }

    #endregion
}