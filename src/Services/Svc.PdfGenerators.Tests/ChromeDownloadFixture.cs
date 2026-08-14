using DKNet.Svc.PdfGenerators;

namespace Svc.PdfGenerators.Tests;

/// <summary>
///     Collection definition that ensures Chrome is downloaded once before any PDF generation tests run.
///     Tests in this collection run sequentially to avoid concurrent Chrome download race conditions.
/// </summary>
[CollectionDefinition("PdfGeneratorChrome")]
public class PdfGeneratorChromeCollection : ICollectionFixture<ChromeDownloadFixture>;

/// <summary>
///     Fixture that downloads Chrome (via PuppeteerSharp) once for all PDF generation tests.
///     The download goes through <see cref="PdfGenerator" />'s serialized <c>EnsureChromeAsync</c> path
///     (DRK-363), so it shares the process-wide lock with conversions running in parallel collections
///     instead of racing them on the download file.
/// </summary>
public class ChromeDownloadFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var warmupPdfPath = Path.Combine(Path.GetTempPath(), $"chrome-warmup-{Guid.NewGuid():N}.pdf");
        try
        {
            await new PdfGenerator().ConvertHtmlAsync("<h1>warmup</h1>", warmupPdfPath);
        }
        finally
        {
            if (File.Exists(warmupPdfPath)) File.Delete(warmupPdfPath);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
