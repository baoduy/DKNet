using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Xunit.Abstractions;

namespace Svc.PdfGenerators.Tests;

public class Markdown2PdfConverterTests(ITestOutputHelper outputHelper)
{
    #region Methods

    [Fact]
    public async Task Convert_SampleMarkdownFile_GeneratesPdf()
    {
        var converter = new PdfGenerator();
        var markdownPath = Path.GetFullPath("Sample.md");
        var expectedPdfPath = Path.Combine(Path.GetDirectoryName(markdownPath)!, "Sample.pdf");

        if (File.Exists(expectedPdfPath))
            File.Delete(expectedPdfPath);

        var resultPath = await converter.ConvertMarkdownFileAsync(markdownPath);
        OutputResultPath(resultPath);

        Assert.True(File.Exists(resultPath), $"PDF file was not created at {resultPath}");
        var fileInfo = new FileInfo(resultPath);
        Assert.True(fileInfo.Length > 0, "Generated PDF file is empty");

        // Clean up
        //File.Delete(resultPath);
    }

    [Fact]
    public async Task ConvertHtml_SampleHtmlString_GeneratesPdf()
    {
        var converter = new PdfGenerator();
        var htmlContent = "<html><body><h1>Test PDF</h1><p>Generated from HTML string.</p></body></html>";
        var expectedPdfPath = Path.Combine(Directory.GetCurrentDirectory(), "HtmlString.pdf");

        if (File.Exists(expectedPdfPath))
            File.Delete(expectedPdfPath);

        var resultPath = await converter.ConvertHtmlAsync(htmlContent, expectedPdfPath);
        OutputResultPath(resultPath);

        Assert.True(File.Exists(resultPath), $"PDF file was not created at {resultPath}");
        var fileInfo = new FileInfo(resultPath);
        Assert.True(fileInfo.Length > 0, "Generated PDF file is empty");

        //File.Delete(resultPath);
    }

    [Fact]
    public async Task ConvertHtmlAsync_WithCustomOptions_GeneratesPdfWithHeaderFooter()
    {
        var options = new PdfGeneratorOptions
        {
            HeaderHtml = "<div style='font-size:10px'>Header</div>",
            FooterHtml = "<div style='font-size:10px'>Footer</div>",
            MarginOptions = new MarginOptions { Top = "50px", Bottom = "50px" }
        };
        var converter = new PdfGenerator(options);
        var htmlContent = "<html><body><h2>Header/Footer Test</h2></body></html>";
        var outputPdf = Path.Combine(Directory.GetCurrentDirectory(), "HeaderFooter.pdf");
        if (File.Exists(outputPdf)) File.Delete(outputPdf);
        var resultPath = await converter.ConvertHtmlAsync(htmlContent, outputPdf);
        OutputResultPath(resultPath);
        Assert.True(File.Exists(resultPath));
        var fileInfo = new FileInfo(resultPath);
        Assert.True(fileInfo.Length > 0);
        File.Delete(resultPath);
    }

    [Fact]
    public async Task ConvertHtmlFile_SampleHtmlFile_GeneratesPdf()
    {
        var converter = new PdfGenerator();
        var htmlFilePath = Path.GetFullPath("Sample.html");
        var expectedPdfPath = Path.Combine(Path.GetDirectoryName(htmlFilePath)!, "HtmlFile.pdf");

        if (File.Exists(expectedPdfPath))
            File.Delete(expectedPdfPath);

        // Act
        var resultPath = await converter.ConvertHtmlFileAsync(htmlFilePath, expectedPdfPath);
        OutputResultPath(resultPath);

        // Assert
        Assert.True(File.Exists(resultPath));
        var fileInfo = new FileInfo(resultPath);
        Assert.True(fileInfo.Length > 0);
        File.Delete(resultPath);
    }

    [Fact]
    public async Task ConvertHtmlFileAsync_FileNotFound_ThrowsException()
    {
        var converter = new PdfGenerator();
        var missingFile = "missing.html";
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await converter.ConvertHtmlFileAsync(missingFile));
        Assert.Contains("HTML file not found", ex.Message);
    }

    [Fact]
    public async Task ConvertMultipleMarkdownFilesAsync_MergesFiles_GeneratesSinglePdf()
    {
        var converter = new PdfGenerator();
        var file1 = "Sample1.md";
        var file2 = "Sample2.md";
        await File.WriteAllTextAsync(file1, "# Title 1\nContent 1");
        await File.WriteAllTextAsync(file2, "# Title 2\nContent 2");
        var outputPdf = Path.Combine(Directory.GetCurrentDirectory(), "Merged.pdf");
        if (File.Exists(outputPdf)) File.Delete(outputPdf);
        var resultPath = await converter.ConvertMultipleMarkdownFilesAsync([file1, file2], outputPdf);
        OutputResultPath(resultPath);
        Assert.True(File.Exists(resultPath));
        var fileInfo = new FileInfo(resultPath);
        Assert.True(fileInfo.Length > 0);
        File.Delete(file1);
        File.Delete(file2);
        File.Delete(resultPath);
    }

    private void OutputResultPath(string resultPath)
    {
        outputHelper.WriteLine($"Generated PDF file at: {resultPath}");
    }

    #endregion
}