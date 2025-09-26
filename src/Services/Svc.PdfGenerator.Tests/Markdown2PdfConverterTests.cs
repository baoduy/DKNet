using DKNet.Svc.PdfGenerator;
using DKNet.Svc.PdfGenerator.Options;

namespace Svc.PdfGenerator.Tests;

public class Markdown2PdfConverterTests
{
    [Fact]
    public void Constructor_DefaultOptions_InitializesProperties()
    {
        var converter = new Markdown2PdfConverter();
        Assert.NotNull(converter.Options);
        Assert.NotNull(converter.ContentTemplate);
        Assert.Null(converter.OutputFileName);
    }

    [Fact]
    public void Constructor_WithOptions_SetsOptionsProperty()
    {
        var options = new PdfGeneratorOptions { DocumentTitle = "Test Title" };
        var converter = new Markdown2PdfConverter(options);
        Assert.Equal("Test Title", converter.Options.DocumentTitle);
    }

    [Fact]
    public void ContentTemplate_CanBeSetAndRetrieved()
    {
        var converter = new Markdown2PdfConverter();
        var template = "<html>{{body}}</html>";
        converter.ContentTemplate = template;
        Assert.Equal(template, converter.ContentTemplate);
    }

    [Fact]
    public void Events_CanBeSubscribedAndUnsubscribed()
    {
        var converter = new Markdown2PdfConverter();
        EventHandler<MarkdownArgs> handler = (sender, args) => { };
        converter.BeforeHtmlConversion += handler;
        converter.BeforeHtmlConversion -= handler;
        // If no exception is thrown, subscription works as expected
        Assert.True(true);
    }

    [Fact]
    public async Task Convert_SampleMarkdownFile_GeneratesPdf()
    {
        var converter = new Markdown2PdfConverter();
        var markdownPath = Path.GetFullPath("Sample.md");
        var expectedPdfPath = Path.Combine(Path.GetDirectoryName(markdownPath)!, "Sample.pdf");

        if (File.Exists(expectedPdfPath))
            File.Delete(expectedPdfPath);

        var resultPath = await converter.ConvertMarkdown(markdownPath);

        Assert.True(File.Exists(resultPath), $"PDF file was not created at {resultPath}");
        var fileInfo = new FileInfo(resultPath);
        Assert.True(fileInfo.Length > 0, "Generated PDF file is empty");

        // Clean up
        File.Delete(resultPath);
    }

    [Fact]
    public async Task ConvertHtml_SampleHtmlString_GeneratesPdf()
    {
        var converter = new PdfGeneratorOptions();
        var htmlContent = "<html><body><h1>Test PDF</h1><p>Generated from HTML string.</p></body></html>";
        var expectedPdfPath = Path.Combine(Directory.GetCurrentDirectory(), "HtmlString.pdf");

        if (File.Exists(expectedPdfPath))
            File.Delete(expectedPdfPath);

        var resultPath = await DKNet.Svc.PdfGenerator.PdfGenerator.Con(htmlContent, expectedPdfPath);

        Assert.True(File.Exists(resultPath), $"PDF file was not created at {resultPath}");
        var fileInfo = new FileInfo(resultPath);
        Assert.True(fileInfo.Length > 0, "Generated PDF file is empty");

        File.Delete(resultPath);
    }

    [Fact]
    public async Task ConvertHtmlFile_SampleHtmlFile_GeneratesPdf()
    {
        var converter = new Markdown2PdfConverter();
        var htmlFilePath = Path.GetFullPath("Sample.html");
        var expectedPdfPath = Path.Combine(Path.GetDirectoryName(htmlFilePath)!, "HtmlFile.pdf");

        if (File.Exists(expectedPdfPath))
            File.Delete(expectedPdfPath);

        var resultPath = await Markdown2PdfConverter.ConvertHtmlFile(htmlFilePath, expectedPdfPath);
    }
}