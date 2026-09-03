using DKNet.Svc.PdfGenerators.Options;
using Markdig;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using MarginOptions = PuppeteerSharp.Media.MarginOptions;

namespace DKNet.Svc.PdfGenerators;

/// <summary>
///     Interface for PdfGenerator operations.
/// </summary>
public interface IPdfGenerator
{
    #region Methods

    /// <summary>
    ///     Converts HTML content to PDF.
    /// </summary>
    /// <param name="htmlContent">HTML content as a string.</param>
    /// <param name="outputPath">
    ///     Optional output PDF file path. If not provided, uses "output_from_html.pdf" in the current
    ///     directory.
    /// </param>
    /// <returns>Path to the generated PDF file.</returns>
    Task<string> ConvertHtmlAsync(string htmlContent, string? outputPath = null);

    /// <summary>
    ///     Converts an HTML file to PDF.
    /// </summary>
    /// <param name="htmlFilePath">Path to the HTML file.</param>
    /// <param name="outputPath">
    ///     Optional output PDF file path. If not provided, uses "output_from_html.pdf" in the current
    ///     directory.
    /// </param>
    /// <returns>Path to the generated PDF file.</returns>
    Task<string> ConvertHtmlFileAsync(string htmlFilePath, string? outputPath = null);

    /// <summary>
    ///     Converts a markdown file to PDF.
    /// </summary>
    /// <param name="markdownFile">The markdown file to convert.</param>
    /// <returns>The generated PDF file info.</returns>
    Task<FileInfo> ConvertMarkdownFileAsync(FileInfo markdownFile);

    /// <summary>
    ///     Converts a markdown file to PDF.
    /// </summary>
    /// <param name="markdownFilePath">Path to the markdown file.</param>
    /// <param name="outputFilePath">
    ///     Optional output PDF file path. If not provided, uses the markdown file name with .pdf
    ///     extension.
    /// </param>
    /// <returns>Path to the generated PDF file.</returns>
    Task<string> ConvertMarkdownFileAsync(string markdownFilePath, string? outputFilePath = null);

    /// <summary>
    ///     Converts multiple markdown files to a single PDF.
    /// </summary>
    /// <param name="markdownFilePaths">Array of markdown file paths.</param>
    /// <param name="outputFilePath">Output PDF file path.</param>
    /// <returns>Path to the generated PDF file.</returns>
    Task<string> ConvertMultipleMarkdownFilesAsync(string[] markdownFilePaths, string outputFilePath);

    #endregion
}

/// <summary>
///     Provides functionality to generate PDF files from Markdown or HTML sources.
/// </summary>
/// <remarks>
///     Initializes a new instance of <see cref="PdfGenerator" />.
/// </remarks>
/// <param name="options">Options for PDF generation.</param>
/// <summary>
///     Provides PdfGenerator functionality.
/// </summary>
/// <param>The null parameter.</param>
/// <returns>The result of the operation.</returns>
public class PdfGenerator(PdfGeneratorOptions? options = null) : IPdfGenerator, IAsyncDisposable
{
    #region Properties

    /// <summary>
    ///     Serializes the Chrome browser download so concurrent conversions never race on the same download.
    /// </summary>
    private static readonly SemaphoreSlim ChromeDownloadLock = new(1, 1);

    /// <summary>
    ///     Serializes access to <see cref="_browser" /> so concurrent conversions never launch more than one
    ///     browser process for the same <see cref="PdfGenerator" /> instance.
    /// </summary>
    private readonly SemaphoreSlim _browserLock = new(1, 1);

    /// <summary>
    ///     The browser shared by every conversion performed by this instance. Re-created automatically if it
    ///     is <see langword="null" />, closed, or disconnected (e.g. it crashed or was disposed).
    /// </summary>
    private IBrowser? _browser;

    /// <summary>
    ///     Options for PDF generation.
    /// </summary>
    private PdfGeneratorOptions Options { get; } = options ?? new PdfGeneratorOptions();

    /// <summary>
    ///     The Marking pipeline used for markdown to HTML conversion, built once and reused for every
    ///     conversion performed by this instance.
    /// </summary>
    private MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .UseEmojiAndSmiley()
        .Build();

    #endregion

    #region Methods

    /// <summary>
    ///     Converts HTML content to PDF.
    /// </summary>
    /// <param name="htmlContent">HTML content as a string.</param>
    /// <param name="outputPath">
    ///     Optional output PDF file path. If not provided, uses "output_from_html.pdf" in the current
    ///     directory.
    /// </param>
    /// <returns>Path to the generated PDF file.</returns>
    public async Task<string> ConvertHtmlAsync(string htmlContent, string? outputPath = null)
    {
        var pdfFileName = outputPath ?? Path.Combine(Directory.GetCurrentDirectory(), "output_from_html.pdf");
        await GeneratePdfFromHtmlAsync(htmlContent, pdfFileName);
        return pdfFileName;
    }

    /// <summary>
    ///     Converts an HTML file to PDF.
    /// </summary>
    /// <param name="htmlFilePath">Path to the HTML file.</param>
    /// <param name="outputPath">
    ///     Optional output PDF file path. If not provided, uses "output_from_html.pdf" in the current
    ///     directory.
    /// </param>
    /// <returns>Path to the generated PDF file.</returns>
    public async Task<string> ConvertHtmlFileAsync(string htmlFilePath, string? outputPath = null)
    {
        if (!File.Exists(htmlFilePath)) throw new FileNotFoundException($"HTML file not found: {htmlFilePath}");

        var htmlContent = await File.ReadAllTextAsync(htmlFilePath);
        return await ConvertHtmlAsync(htmlContent, outputPath);
    }

    /// <summary>
    ///     Converts a markdown file to PDF.
    /// </summary>
    /// <param name="markdownFile">The markdown file to convert.</param>
    /// <returns>The generated PDF file info.</returns>
    public async Task<FileInfo> ConvertMarkdownFileAsync(FileInfo markdownFile) =>
        new(await ConvertMarkdownFileAsync(markdownFile.FullName));

    /// <summary>
    ///     Converts a markdown file to PDF.
    /// </summary>
    /// <param name="markdownFilePath">Path to the markdown file.</param>
    /// <param name="outputFilePath">
    ///     Optional output PDF file path. If not provided, uses the markdown file name with .pdf
    ///     extension.
    /// </param>
    /// <returns>Path to the generated PDF file.</returns>
    public async Task<string> ConvertMarkdownFileAsync(string markdownFilePath, string? outputFilePath = null)
    {
        markdownFilePath = Path.GetFullPath(markdownFilePath);
        outputFilePath = outputFilePath != null
            ? Path.GetFullPath(outputFilePath)
            : Path.ChangeExtension(markdownFilePath, ".pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

        var markdownContent = await File.ReadAllTextAsync(markdownFilePath);
        var html = Markdown.ToHtml(markdownContent, Pipeline);
        await GeneratePdfFromHtmlAsync(html, outputFilePath);
        return outputFilePath;
    }

    /// <summary>
    ///     Converts multiple markdown files to a single PDF.
    /// </summary>
    /// <param name="markdownFilePaths">Array of markdown file paths.</param>
    /// <param name="outputFilePath">Output PDF file path.</param>
    /// <returns>Path to the generated PDF file.</returns>
    public async Task<string> ConvertMultipleMarkdownFilesAsync(string[] markdownFilePaths, string outputFilePath)
    {
        var markdownContents = await Task.WhenAll(markdownFilePaths.Select(path => File.ReadAllTextAsync(path)));
        var markdownContent = string.Join(Environment.NewLine, markdownContents);
        var html = Markdown.ToHtml(markdownContent, Pipeline);
        await GeneratePdfFromHtmlAsync(html, outputFilePath);
        return outputFilePath;
    }

    /// <summary>
    ///     Generates a PDF from HTML content.
    /// </summary>
    /// <param name="htmlContent">HTML content as a string.</param>
    /// <param name="outputFilePath">Output PDF file path.</param>
    private async Task GeneratePdfFromHtmlAsync(string htmlContent, string outputFilePath)
    {
        var browser = await GetBrowserAsync();
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

    /// <summary>
    ///     Ensures the Chrome browser used by Puppeteer is available, serializing the download so
    ///     concurrent conversions never race on the same download.
    /// </summary>
    private async Task EnsureChromeAsync()
    {
        if (Options.ChromePath != null) return;

        await ChromeDownloadLock.WaitAsync();
        try
        {
            await new BrowserFetcher().DownloadAsync();
        }
        finally
        {
            ChromeDownloadLock.Release();
        }
    }

    /// <summary>
    ///     Returns the browser shared by every conversion performed by this instance, launching it on first
    ///     use and relaunching it if the previous instance is closed or disconnected (e.g. it crashed).
    ///     Concurrent callers are serialized so at most one browser process is ever launched at a time.
    /// </summary>
    /// <returns>A connected <see cref="IBrowser" /> ready to open new pages.</returns>
    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is { IsConnected: true }) return _browser;

        await _browserLock.WaitAsync();
        try
        {
            if (_browser is { IsConnected: true }) return _browser;

            if (_browser is not null) await _browser.DisposeAsync();

            await EnsureChromeAsync();
            var launchOptions = new LaunchOptions
            {
                Headless = true,
                ExecutablePath = Options.ChromePath
            };

            // Add no-sandbox args for CI/container environments
            if (Environment.GetEnvironmentVariable("CI") == "true" ||
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
                launchOptions.Args = ["--no-sandbox", "--disable-setuid-sandbox"];

            _browser = await Puppeteer.LaunchAsync(launchOptions);
            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    /// <summary>
    ///     Closes and disposes the shared browser, if one was launched.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _browserLock.WaitAsync();
        try
        {
            if (_browser is not null)
            {
                await _browser.DisposeAsync();
                _browser = null;
            }
        }
        finally
        {
            _browserLock.Release();
        }

        _browserLock.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}