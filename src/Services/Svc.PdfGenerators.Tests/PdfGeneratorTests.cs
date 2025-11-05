using DKNet.Svc.PdfGenerators;
using Xunit.Abstractions;

namespace Svc.PdfGenerators.Tests;

public class PdfGeneratorTests(ITestOutputHelper testOutputHelper)
{
    #region Methods

    [Fact]
    public async Task ConvertHtmlAsync_GeneratesPdfFile()
    {
        var generator = new PdfGenerator();
        var html = "<h1>Hello HTML</h1><p>Test paragraph.</p>";
        var pdfPath = Path.GetTempFileName() + ".pdf";
        var result = await generator.ConvertHtmlAsync(html, pdfPath);
        Assert.True(File.Exists(result));
        testOutputHelper.WriteLine($"PDF generated at: {result}");
        File.Delete(result);
    }

    [Fact]
    public async Task ConvertHtmlFileAsync_ThrowsIfFileNotFound()
    {
        var generator = new PdfGenerator();
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await generator.ConvertHtmlFileAsync("nonexistent.html"));
    }

    [Fact]
    public async Task ConvertMarkdownFileAsync_GeneratesPdfFile()
    {
        var generator = new PdfGenerator();
        var markdownPath = Path.GetTempFileName() + ".md";
        var pdfPath = Path.ChangeExtension(markdownPath, ".pdf");
        await File.WriteAllTextAsync(markdownPath, "# Hello World\nThis is a test.");
        var result = await generator.ConvertMarkdownFileAsync(markdownPath);
        Assert.True(File.Exists(result));
        testOutputHelper.WriteLine($"PDF generated at: {result}");
        File.Delete(markdownPath);
        File.Delete(result);
    }

    [Fact]
    public async Task ConvertMultipleMarkdownFilesAsync_GeneratesPdfFile()
    {
        var generator = new PdfGenerator();
        var md1 = Path.GetTempFileName() + ".md";
        var md2 = Path.GetTempFileName() + ".md";
        await File.WriteAllTextAsync(md1, "# First File\nContent");
        await File.WriteAllTextAsync(md2, "# Second File\nContent");
        var pdfPath = Path.GetTempFileName() + ".pdf";
        var result = await generator.ConvertMultipleMarkdownFilesAsync([md1, md2], pdfPath);
        Assert.True(File.Exists(result));
        testOutputHelper.WriteLine($"PDF generated at: {result}");
        File.Delete(md1);
        File.Delete(md2);
        File.Delete(result);
    }

    #endregion
}