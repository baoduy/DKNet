using DKNet.Svc.PdfGenerators.Options;
using Markdig;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using MarginOptions = PuppeteerSharp.Media.MarginOptions;

namespace DKNet.Svc.PdfGenerators;

/// <summary>
/// Provides functionality to generate PDF files from Markdown or HTML sources.
/// </summary>
public class PdfGenerator
{
    /// <summary>
    /// Options for PDF generation.
    /// </summary>
    public PdfGeneratorOptions Options { get; }

    /// <summary>
    /// Template for HTML generation from markdown.
    /// </summary>
    public string ContentTemplate { get; set; }

    /// <summary>
    /// The PDF file name without extension.
    /// </summary>
    public string? OutputFileName { get; private set; }

    /// <summary>
    /// The Markdig pipeline used for markdown to HTML conversion.
    /// </summary>
    public MarkdownPipelineBuilder PipelineBuilder { get; } = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .UseEmojiAndSmiley();

    /// <summary>
    /// Initializes a new instance of <see cref="PdfGenerator"/>.
    /// </summary>
    /// <param name="options">Options for PDF generation.</param>
    public PdfGenerator(PdfGeneratorOptions? options = null)
    {
        Options = options ?? new PdfGeneratorOptions();
        ContentTemplate = "<html><body>{{body}}</body></html>";
    }

    /// <summary>
    /// Converts a markdown file to PDF.
    /// </summary>
    /// <param name="markdownFile">The markdown file to convert.</param>
    /// <returns>The generated PDF file info.</returns>
    public async Task<FileInfo> ConvertMarkdownFileAsync(FileInfo markdownFile) =>
        new(await ConvertMarkdownFileAsync(markdownFile.FullName));

    /// <summary>
    /// Converts a markdown file to PDF.
    /// </summary>
    /// <param name="markdownFilePath">Path to the markdown file.</param>
    /// <param name="outputFilePath">Optional output PDF file path. If not provided, uses the markdown file name with .pdf extension.</param>
    /// <returns>Path to the generated PDF file.</returns>
    public async Task<string> ConvertMarkdownFileAsync(string markdownFilePath, string? outputFilePath = null)
    {
        markdownFilePath = Path.GetFullPath(markdownFilePath);
        outputFilePath = outputFilePath != null
            ? Path.GetFullPath(outputFilePath)
            : Path.ChangeExtension(markdownFilePath, ".pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

        var markdownContent = await File.ReadAllTextAsync(markdownFilePath);
        var html = Markdown.ToHtml(markdownContent, PipelineBuilder.Build());
        await GeneratePdfFromHtmlAsync(html, outputFilePath);
        return outputFilePath;
    }

    /// <summary>
    /// Converts multiple markdown files to a single PDF.
    /// </summary>
    /// <param name="markdownFilePaths">Array of markdown file paths.</param>
    /// <param name="outputFilePath">Output PDF file path.</param>
    /// <returns>Path to the generated PDF file.</returns>
    public async Task<string> ConvertMultipleMarkdownFilesAsync(string[] markdownFilePaths, string outputFilePath)
    {
        var markdownContents = await Task.WhenAll(markdownFilePaths.Select(path => File.ReadAllTextAsync(path)));
        var markdownContent = string.Join(Environment.NewLine, markdownContents);
        var html = Markdown.ToHtml(markdownContent, PipelineBuilder.Build());
        await GeneratePdfFromHtmlAsync(html, outputFilePath);
        return outputFilePath;
    }

    /// <summary>
    /// Converts HTML content to PDF.
    /// </summary>
    /// <param name="htmlContent">HTML content as a string.</param>
    /// <param name="outputPath">Optional output PDF file path. If not provided, uses "output_from_html.pdf" in the current directory.</param>
    /// <returns>Path to the generated PDF file.</returns>
    public async Task<string> ConvertHtmlAsync(string htmlContent, string? outputPath = null)
    {
        var pdfFileName = outputPath ?? Path.Combine(Directory.GetCurrentDirectory(), "output_from_html.pdf");
        await GeneratePdfFromHtmlAsync(htmlContent, pdfFileName);
        return pdfFileName;
    }

    /// <summary>
    /// Converts an HTML file to PDF.
    /// </summary>
    /// <param name="htmlFilePath">Path to the HTML file.</param>
    /// <param name="outputPath">Optional output PDF file path. If not provided, uses "output_from_html.pdf" in the current directory.</param>
    /// <returns>Path to the generated PDF file.</returns>
    public async Task<string> ConvertHtmlFileAsync(string htmlFilePath, string? outputPath = null)
    {
        if (!File.Exists(htmlFilePath))
            throw new FileNotFoundException($"HTML file not found: {htmlFilePath}");
        var htmlContent = await File.ReadAllTextAsync(htmlFilePath);
        return await ConvertHtmlAsync(htmlContent, outputPath);
    }

    /// <summary>
    /// Generates a PDF from HTML content.
    /// </summary>
    /// <param name="htmlContent">HTML content as a string.</param>
    /// <param name="outputFilePath">Output PDF file path.</param>
    private async Task GeneratePdfFromHtmlAsync(string htmlContent, string outputFilePath)
    {
        await new BrowserFetcher().DownloadAsync();
        var launchOptions = new LaunchOptions 
        { 
            Headless = true,
            ExecutablePath = Options.ChromePath
        };
        
        // Add no-sandbox args for CI/container environments
        if (Environment.GetEnvironmentVariable("CI") == "true" || 
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            launchOptions.Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" };
        }
        
        await using var browser = await Puppeteer.LaunchAsync(launchOptions);
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(htmlContent);
        var pdfOptions = new PdfOptions
        {
            Format = Options.Format,
            Landscape = Options.IsLandscape,
            PrintBackground = true,
            MarginOptions = Options.MarginOptions != null
                ? new MarginOptions
                {
                    Top = Options.MarginOptions.Top,
                    Bottom = Options.MarginOptions.Bottom,
                    Left = Options.MarginOptions.Left,
                    Right = Options.MarginOptions.Right
                }
                : new MarginOptions(),
            Scale = Options.Scale,
            DisplayHeaderFooter = Options.HeaderHtml != null || Options.FooterHtml != null,
            HeaderTemplate = Options.HeaderHtml,
            FooterTemplate = Options.FooterHtml
        };
        await page.EmulateMediaTypeAsync(MediaType.Screen);
        await page.PdfAsync(outputFilePath, pdfOptions);
    }
}