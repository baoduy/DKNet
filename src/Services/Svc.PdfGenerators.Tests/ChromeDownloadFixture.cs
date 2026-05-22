using PuppeteerSharp;

namespace Svc.PdfGenerators.Tests;

/// <summary>
///     Collection definition that ensures Chrome is downloaded once before any PDF generation tests run.
///     Tests in this collection run sequentially to avoid concurrent Chrome download race conditions.
/// </summary>
[CollectionDefinition("PdfGeneratorChrome")]
public class PdfGeneratorChromeCollection : ICollectionFixture<ChromeDownloadFixture>;

/// <summary>
///     Fixture that downloads Chrome (via PuppeteerSharp) once for all PDF generation tests.
///     By sharing this fixture across a collection, the download happens exactly once.
/// </summary>
public class ChromeDownloadFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await new BrowserFetcher().DownloadAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
