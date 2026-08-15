using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Shouldly;
using Xunit.Abstractions;

namespace Svc.PdfGenerators.Tests;

/// <summary>
///     Verifies DRK-363: the Chrome download in <see cref="PdfGenerator" /> is serialized process-wide
///     (static semaphore), skipped entirely when <see cref="PdfGeneratorOptions.ChromePath" /> is set, and
///     reused across conversions instead of being re-downloaded.
/// </summary>
[Collection("PdfGeneratorChrome")]
public class PdfGeneratorChromeDownloadTests(ITestOutputHelper outputHelper)
{
    #region Methods

    [Fact]
    public async Task ConvertHtmlAsync_WithChromePathSet_GeneratesPdfWithProvidedChrome()
    {
        var chromePath = GetInstalledChromeExecutablePath();

        var options = new PdfGeneratorOptions { ChromePath = chromePath };
        var generator = new PdfGenerator(options);
        var pdfPath = Path.GetTempFileName() + ".pdf";

        var result = await generator.ConvertHtmlAsync("<h1>ChromePath</h1>", pdfPath);

        result.ShouldBe(pdfPath);
        var fileInfo = new FileInfo(result);
        fileInfo.Exists.ShouldBeTrue();
        fileInfo.Length.ShouldBeGreaterThan(0);
        outputHelper.WriteLine($"PDF generated with ChromePath: {result}");

        File.Delete(result);
    }

    [Fact]
    public async Task ConvertHtmlAsync_SequentialConversions_SecondConversionReusesDownloadedChrome()
    {
        var chromePath = GetInstalledChromeExecutablePath();
        var lastWriteBefore = File.GetLastWriteTimeUtc(chromePath);

        var first = new PdfGenerator();
        var second = new PdfGenerator();
        var pdf1 = Path.GetTempFileName() + ".pdf";
        var pdf2 = Path.GetTempFileName() + ".pdf";

        await first.ConvertHtmlAsync("<h1>First</h1>", pdf1);
        var result2 = await second.ConvertHtmlAsync("<h1>Second</h1>", pdf2);

        new FileInfo(result2).Length.ShouldBeGreaterThan(0);
        File.GetLastWriteTimeUtc(chromePath).ShouldBe(lastWriteBefore, "Chrome must not be re-downloaded for a second conversion");

        File.Delete(pdf1);
        File.Delete(pdf2);
    }

    [Fact]
    public async Task ConvertMarkdownFileAsync_WithFileInfo_GeneratesPdf()
    {
        var markdownPath = Path.GetTempFileName() + ".md";
        await File.WriteAllTextAsync(markdownPath, "# FileInfo overload\nBody text.");
        var generator = new PdfGenerator();

        var result = await generator.ConvertMarkdownFileAsync(new FileInfo(markdownPath));

        result.Exists.ShouldBeTrue();
        result.Length.ShouldBeGreaterThan(0);
        outputHelper.WriteLine($"PDF generated from FileInfo: {result.FullName}");

        File.Delete(markdownPath);
        result.Delete();
    }

    [Fact]
    public async Task ConvertHtmlAsync_WithFullOptions_GeneratesPdf()
    {
        var options = new PdfGeneratorOptions
        {
            IsLandscape = true,
            Scale = 1.25m,
            Format = PaperFormat.Letter,
            MarginOptions = new DKNet.Svc.PdfGenerators.Options.MarginOptions
            {
                Top = "40px",
                Bottom = "40px",
                Left = "20px",
                Right = "20px"
            },
            HeaderHtml = "<div>Header</div>",
            FooterHtml = "<div>Footer</div>"
        };
        var generator = new PdfGenerator(options);
        var pdfPath = Path.GetTempFileName() + ".pdf";

        var result = await generator.ConvertHtmlAsync("<h1>Options</h1>", pdfPath);

        var fileInfo = new FileInfo(result);
        fileInfo.Exists.ShouldBeTrue();
        fileInfo.Length.ShouldBeGreaterThan(0);
        outputHelper.WriteLine($"PDF generated with options: {result}");

        File.Delete(result);
    }

    private static string GetInstalledChromeExecutablePath()
    {
        var executablePath = new BrowserFetcher()
            .GetInstalledBrowsers()
            .FirstOrDefault(b => b.BuildId == PuppeteerSharp.BrowserData.Chrome.DefaultBuildId)
            ?.GetExecutablePath();
        executablePath.ShouldNotBeNull("Expected the collection fixture to have downloaded Chrome");
        File.Exists(executablePath).ShouldBeTrue($"Expected Chrome executable at {executablePath}");
        return executablePath;
    }

    #endregion
}

/// <summary>
///     Runs outside the shared Chrome collection so the concurrent conversions race the process-wide
///     Chrome download exactly like the pre-fix suite did (parallel collections colliding on the zip).
///     Serialization (DRK-363) must let every conversion succeed without an IOException.
/// </summary>
public class PdfGeneratorConcurrentDownloadTests(ITestOutputHelper outputHelper)
{
    #region Methods

    [Fact]
    public async Task ConvertHtmlAsync_ConcurrentDefaultOptionInstances_BothSucceedWithoutIOException()
    {
        var generatorCount = 6;
        var generators = Enumerable.Range(0, generatorCount).Select(_ => new PdfGenerator()).ToArray();
        var pdfPaths = Enumerable.Range(0, generatorCount)
            .Select(_ => Path.GetTempFileName() + ".pdf")
            .ToArray();
        var html = "<h1>Concurrent</h1><p>Shared Chrome download must be serialized.</p>";

        try
        {
            var tasks = generators.Zip(pdfPaths, (generator, pdfPath) => generator.ConvertHtmlAsync(html, pdfPath)).ToArray();
            var results = await Task.WhenAll(tasks);

            results.Length.ShouldBe(generatorCount);
            foreach (var result in results)
            {
                new FileInfo(result).Length.ShouldBeGreaterThan(0, $"PDF not generated at {result}");
                outputHelper.WriteLine($"Concurrent PDF generated: {result}");
            }
        }
        catch (AggregateException ex)
        {
            var ioExceptions = ex.Flatten().InnerExceptions.OfType<IOException>().ToList();
            ioExceptions.ShouldBeEmpty("Concurrent conversions must not race the Chrome download");
            throw;
        }
        finally
        {
            foreach (var pdfPath in pdfPaths)
            {
                if (File.Exists(pdfPath)) File.Delete(pdfPath);
            }
        }
    }

    #endregion
}
