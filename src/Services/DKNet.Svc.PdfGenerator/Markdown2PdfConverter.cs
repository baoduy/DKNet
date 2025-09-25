using System.Xml.Linq;
using System.Xml.XPath;
using DKNet.Svc.PdfGenerator.Options;
using DKNet.Svc.PdfGenerator.Services;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;
using PuppeteerSharp.Media;

namespace DKNet.Svc.PdfGenerator;

/// <summary>
/// The main <see langword="class"/> for converting markdown to PDF.<br/>
/// </summary>
/// <example>
/// The following code example shows how to convert a file "README.md" in the current directory to PDF.
/// The output will be saved as "README.pdf" in the same directory.
/// <code>
/// var converter = new Markdown2PdfConverter();
/// var resultPath = await converter.Convert("README.md");
/// </code>
/// To further specify the conversion process, <see cref="Markdown2PdfOptions"/> can be passed to the converter.
/// <code>
/// var options = new Markdown2PdfOptions {
///   HeaderHtml = File.ReadAllText("header.html"),
///   FooterHtml = File.ReadAllText("footer.html"),
///   DocumentTitle = "Example PDF",
/// };
/// var converter = new Markdown2PdfConverter(options);
/// </code>
/// </example>
public class Markdown2PdfConverter : IConvertionEvents
{
    /// <summary>
    /// Contains all options this converter uses for generating the PDF.
    /// </summary>
    /// <remarks>Can be set with the constructor <see cref="Markdown2PdfConverter(Markdown2PdfOptions)"/>.</remarks>
    public Markdown2PdfOptions Options { get; }

    /// <summary>
    /// The template used for generating the HTML which then gets converted into PDF.
    /// </summary>
    /// <remarks>Modify this to get more control over the HTML generation (e.g. to add your own JS-Scripts).</remarks>
    public string ContentTemplate { get; set; }

    /// <summary>
    /// The PDF file name without extension.
    /// </summary>
    public string? OutputFileName { get; private set; }

    /// <summary>
    /// The <see href="https://github.com/xoofx/markdig">Markdig</see> <see cref="MarkdownPipelineBuilder"/> used for the markdown to HTML conversion.
    /// </summary>
    /// <remarks>
    /// This <see cref="MarkdownPipelineBuilder"/> has the following extensions enabled by default:
    /// <br> * </br><see cref="MarkdownExtensions.UseAdvancedExtensions"/>
    /// <br> * </br><see cref="MarkdownExtensions.UseYamlFrontMatter"/>
    /// <br> * </br><see cref="MarkdownExtensions.UseEmojiAndSmiley(MarkdownPipelineBuilder, bool)"/>
    /// <br> * </br><see cref="MarkdownExtensions.UseAutoIdentifiers"/> with <see cref="AutoIdentifierOptions.AutoLink"/>
    /// </remarks>
    public MarkdownPipelineBuilder PipelineBuilder { get; } = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .UseEmojiAndSmiley();

    /// <inheritdoc cref="IConvertionEvents.BeforeHtmlConversion"/>
    public event EventHandler<MarkdownArgs>? BeforeHtmlConversion;

    event EventHandler<MarkdownArgs> IConvertionEvents.BeforeHtmlConversion
    {
        add => BeforeHtmlConversion += value;
        remove => BeforeHtmlConversion -= value;
    }

    /// <inheritdoc cref="IConvertionEvents.OnTemplateModelCreating"/>
    public event EventHandler<TemplateModelArgs>? OnTemplateModelCreating;

    event EventHandler<TemplateModelArgs>? IConvertionEvents.OnTemplateModelCreating
    {
        add => OnTemplateModelCreating += value;
        remove => OnTemplateModelCreating -= value;
    }

    /// <inheritdoc cref="IConvertionEvents.OnTempPdfCreatedEvent"/>
    public event EventHandler<PdfArgs>? OnTempPdfCreatedEvent;

    event EventHandler<PdfArgs>? IConvertionEvents.OnTempPdfCreatedEvent
    {
        add => OnTempPdfCreatedEvent += value;
        remove => OnTempPdfCreatedEvent -= value;
    }

    private readonly EmbeddedResourceService _embeddedResourceService = new();

    private const string CustomHeadKey = "customHeadContent";
    private const string BodyKey = "body";
    private const string CodeHighlightThemeNameKey = "highlightjs_theme_name";
    private const string DisableAutoLanguageDetectionKey = "disableAutoLanguageDetection";
    private const string DisableAutoLanguageDetectionValue = "hljs.configure({ languages: [] });";

    private const string DocumentTitleClass = "document-title";
    private const string TemplateWithScriptsFileName = "ContentTemplate.html";
    private const string TemplateNoScriptsFileName = "ContentTemplate_NoScripts.html";
    private const string HeaderFooterStylesFileName = "Header-Footer-Styles.html";

    /// <summary>
    /// Instantiates a new <see cref="Markdown2PdfConverter"/>.
    /// </summary>
    /// <param name="options">Optional options to specify how to convert the markdown.</param>
    public Markdown2PdfConverter(Markdown2PdfOptions? options = null)
    {
        Options = options ?? new Markdown2PdfOptions();

        // Switch to AutoLink Option to allow non-ASCII characters
        PipelineBuilder.Extensions.Remove(PipelineBuilder.Extensions.Find<AutoIdentifierExtension>()!);
        PipelineBuilder.UseAutoIdentifiers(AutoIdentifierOptions.AutoLink);

        var moduleOptions = Options.ModuleOptions;

        var templateName = Options.ModuleOptions == ModuleOptions.None
            ? TemplateNoScriptsFileName
            : TemplateWithScriptsFileName;
        ContentTemplate = _embeddedResourceService.GetResourceContent(templateName);

        // Services can be discarded because they stay alive through event attaching.
        if (Options.TableOfContents != null)
            _ = new TableOfContentsCreator(Options.TableOfContents, this, _embeddedResourceService);

        _ = new ThemeService(Options.Theme, moduleOptions, this);
        _ = new ModuleService(Options.ModuleOptions, this);
        _ = new MetadataService(Options, this);
    }

    /// <summary>
    /// Instantiates a new <see cref="Markdown2PdfConverter"/>.
    /// The <see cref="Markdown2PdfOptions"/> are loaded from a <i>YAML front matter block</i>
    /// at the start of the given markdown document.
    /// </summary>
    /// <param name="markdownFilePath">Path to the markdown file containing the <i>YAML front matter</i>.</param>
    /// <returns>The new <see cref="Markdown2PdfConverter"/>.</returns>
    /// <example>
    /// Use this at the beginning of the markdown file:
    /// <code language="markdown">
    /// ---
    /// document-title: myDocumentTitle
    /// metadata-title: myMetadataTitle
    /// module-options: Remote # or None or path to node_module directory
    /// theme: Github # or Latex or None or path to css file
    /// code-highlight-theme: Github
    /// enable-auto-language-detection: true
    /// header-html: "&lt;div class='document-title' style='background-color: #5eafed; width: 100%; padding: 5px'&gt;&lt;/div&gt;"
    /// # footer-html: "&lt;div&gt;hello world&lt;/div&gt;"
    /// # custom-head-content: "&lt;style&gt;h2 { page-break-before: always; }&lt;/style&gt;"
    /// # chrome-path: "C:\Program Files\Google\Chrome\Application\chrome.exe"
    /// keep-html: false
    /// margin-options:
    ///   top: 80px
    ///   bottom: 50px
    ///   left: 50px
    ///   right: 50px
    /// is-landscape: false
    /// format: A4
    /// scale: 1
    /// table-of-contents:
    ///   list-style: decimal
    ///   min-depth-level: 2
    ///   max-depth-level: 6
    ///   page-number-options:
    ///     tab-leader: dots
    /// ---
    ///
    /// # Here the normal markdown content starts
    /// </code>
    /// </example>
    /// <remarks>
    /// Instead of three dashes (---) an HTML comment (&lt;!-- --&gt;) can also be used to wrap the YAML.
    /// </remarks>
    public static Markdown2PdfConverter CreateWithInlineOptionsFromFile(string markdownFilePath)
    {
        var options = InlineOptionsParser.ParseYamlFrontMatter(markdownFilePath);
        return new Markdown2PdfConverter(options);
    }

    /// <inheritdoc cref="CreateWithInlineOptionsFromFile(string)"/>
    /// <param name="markdownFile">Markdown file containing the <i>YAML front matter</i>.</param>
    public static Markdown2PdfConverter CreateWithInlineOptionsFromFile(FileInfo markdownFile)
        => CreateWithInlineOptionsFromFile(markdownFile.FullName);

    /// <inheritdoc>
    ///     <cref>ConvertMarkdown</cref>
    /// </inheritdoc>
    /// <remarks>The PDF will be saved in the same location as the markdown file with the naming convention "markdownFileName.pdf".</remarks>
    /// <returns>The newly created PDF file.</returns>
    public async Task<FileInfo> ConvertMarkdown(FileInfo markdownFile) =>
        new(await ConvertMarkdown(markdownFile.FullName));

    /// <summary>
    /// Converts the given markdown-file to PDF.
    /// </summary>
    /// <param name="markdownFile"><see cref="FileInfo"/> containing the markdown.</param>
    /// <param name="outputFile"><see cref="FileInfo"/> for saving the generated PDF.</param>
    public async Task ConvertMarkdown(FileInfo markdownFile, FileInfo outputFile) =>
        await ConvertMarkdown(markdownFile.FullName, outputFile.FullName);

    /// <inheritdoc cref="ConvertMarkdown(string,string)"/>
    /// <remarks>The PDF will be saved in the same location as the markdown file with the naming convention "markdownFileName.pdf".</remarks>
    public async Task<string> ConvertMarkdown(string markdownFilePath)
    {
        var markdownDir = Path.GetDirectoryName(Path.GetFullPath(markdownFilePath));
        var outputFileName = Path.GetFileNameWithoutExtension(markdownFilePath) + ".pdf";
        var outputFilePath = Path.Combine(markdownDir!, outputFileName);
        await ConvertMarkdown(markdownFilePath, outputFilePath);

        return outputFilePath;
    }

    /// <summary>
    /// Converts the given markdown file to PDF.
    /// </summary>
    /// <param name="markdownFilePath">Path to the markdown file.</param>
    /// <param name="outputFilePath">File path for saving the PDF to.</param>
    /// <remarks>The PDF will be saved at the path specified in <paramref name="outputFilePath"/>.</remarks>
    /// <returns>Filepath to the generated pdf.</returns>
    public async Task<string> ConvertMarkdown(string markdownFilePath, string outputFilePath)
    {
        markdownFilePath = Path.GetFullPath(markdownFilePath);
        outputFilePath = Path.GetFullPath(outputFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

        var markdownContent = await File.ReadAllTextAsync(markdownFilePath);

        await InternalConvert(outputFilePath, markdownContent, markdownFilePath);

        return outputFilePath;
    }

    /// <inheritdoc>
    ///     <cref>Convert(IEnumerable{string}, string)</cref>
    /// </inheritdoc>
    /// <remarks>The PDF will be saved in the same location of the first markdown file with the naming convention "markdownFileName.pdf".</remarks>
    public async Task<string> ConvertMarkdown(string[] markdownFilePaths)
    {
        var first = markdownFilePaths.First();
        var markdownDir = Path.GetDirectoryName(first);
        var outputFileName = Path.GetFileNameWithoutExtension(first) + ".pdf";
        var outputFilePath = Path.Combine(markdownDir!, outputFileName);
        await ConvertMarkdown(markdownFilePaths, outputFilePath);

        return outputFilePath;
    }

    /// <summary>
    /// Converts the given enumerable of markdown files to PDF.
    /// </summary>
    /// <param name="markdownFilePaths">Enumerable with paths of the markdown files.</param>
    /// <param name="outputFilePath">File path for saving the PDF to.</param>
    public async Task<string> ConvertMarkdown(string[] markdownFilePaths, string outputFilePath)
    {
        var markdownContent = string.Join(Environment.NewLine, markdownFilePaths.Select(File.ReadAllText));

        var markdownFilePath = Path.GetFullPath(markdownFilePaths.First());
        outputFilePath = Path.GetFullPath(outputFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

        await InternalConvert(outputFilePath, markdownContent, markdownFilePath);

        return outputFilePath;
    }

    /// <summary>
    /// Converts the given list of markdown files to PDF.
    /// </summary>
    /// <param name="outputFilePath">File path for saving the PDF to.</param>
    /// <param name="markdownContent">String holding all markdown data.</param>
    /// <param name="markdownFilePath">Path of the first markdown file.</param>
    private async Task InternalConvert(string outputFilePath, string markdownContent, string markdownFilePath)
    {
        // Rerun logic
        await InternalConvertMarkdown(outputFilePath, markdownContent, markdownFilePath);
        var args = new PdfArgs(outputFilePath);
        if (Options.TableOfContents?.PageNumberOptions != null)
        {
            // If PageNumbers enabled, PDF needs to be generated twice
            var tempPath = InternalCreateTempFilePath(outputFilePath);
            await InternalConvertMarkdown(tempPath, markdownContent, markdownFilePath);
            OnTempPdfCreatedEvent?.Invoke(this, new PdfArgs(tempPath)); // TODO: trigger at right time
            File.Delete(tempPath);
        }

        await InternalConvertMarkdown(outputFilePath, markdownContent, markdownFilePath);
    }

    private static string InternalCreateTempFilePath(string outputFilePath)
    {
        var outputDir = Path.GetDirectoryName(outputFilePath)!;
        return Path.Combine(outputDir, $"~temp{DateTime.UtcNow:yyyyMMddHHmmssfff}.TEMPPDF");
    }

    private Task InternalConvertMarkdown(string outputFilePath, string markdownContent, string markdownFilePath)
    {
        // generate html
        var html = GenerateHtml(markdownContent);
        return InternalConvertHtml(outputFilePath, html, markdownFilePath);
    }

    private async Task InternalConvertHtml(string outputFilePath, string htmlContent, string inputFilePath)
    {
        OutputFileName = Path.GetFileNameWithoutExtension(outputFilePath);

        var markdownDir = Path.GetDirectoryName(inputFilePath);
        var htmlFileName = Path.GetFileNameWithoutExtension(inputFilePath) + ".html";
        var htmlPath = Path.Combine(markdownDir!, htmlFileName);
        await File.WriteAllTextAsync(htmlPath, htmlContent);

        // generate pdf
        await InternalGeneratePdfAsync(htmlPath, outputFilePath);

        if (!Options.KeepHtml)
            File.Delete(htmlPath);
    }

    private string GenerateHtml(string markdownContent)
    {
        var markdownArgs = new MarkdownArgs(markdownContent);
        BeforeHtmlConversion?.Invoke(this, markdownArgs);
        markdownContent = markdownArgs.MarkdownContent;

        var pipeline = PipelineBuilder.Build();

        var htmlContent = Markdown.ToHtml(markdownContent, pipeline);

        var templateModel = InternalCreateTemplateModel(htmlContent);

        return TemplateFiller.FillTemplate(ContentTemplate, templateModel);
    }

    private Dictionary<string, string> InternalCreateTemplateModel(string htmlContent)
    {
        var templateModel = new Dictionary<string, string>();

        var languageDetectionValue = Options.EnableAutoLanguageDetection
            ? string.Empty
            : DisableAutoLanguageDetectionValue;
        templateModel.Add(DisableAutoLanguageDetectionKey, languageDetectionValue);
        templateModel.Add(CodeHighlightThemeNameKey, Options.CodeHighlightTheme.ToString());
        templateModel.Add(CustomHeadKey, Options.CustomHeadContent ?? string.Empty);
        templateModel.Add(BodyKey, htmlContent);

        OnTemplateModelCreating?.Invoke(this, new TemplateModelArgs(templateModel));

        return templateModel;
    }

    private async Task InternalGeneratePdfAsync(string htmlFilePath, string outputFilePath)
    {
        await using var browser = await InternalCreateBrowserAsync();
        var page = await browser.NewPageAsync();
        var margins = Options.MarginOptions;

        _ = await page.GoToAsync("file:///" + htmlFilePath, WaitUntilNavigation.Networkidle2);

        var puppeteerMargins = margins != null
            ? new PuppeteerSharp.Media.MarginOptions
            {
                Top = margins.Top,
                Bottom = margins.Bottom,
                Left = margins.Left,
                Right = margins.Right
            }
            : new PuppeteerSharp.Media.MarginOptions();

        var pdfOptions = new PdfOptions
        {
            Format = Options.Format,
            Landscape = Options.IsLandscape,
            PrintBackground = true, // TODO: background doesnt work for margins
            MarginOptions = puppeteerMargins,
            Scale = Options.Scale
        };

        var hasHeaderFooterStylesAdded = false;

        // TODO: default header is super small
        if (Options.HeaderHtml != null)
        {
            pdfOptions.DisplayHeaderFooter = true;
            var html = InternalFillHeaderFooterDocumentTitleClass(Options.HeaderHtml);
            pdfOptions.HeaderTemplate = InternalAddHeaderFooterStylesToHtml(html);
            hasHeaderFooterStylesAdded = true;
        }

        if (Options.FooterHtml != null)
        {
            pdfOptions.DisplayHeaderFooter = true;
            var html = InternalFillHeaderFooterDocumentTitleClass(Options.FooterHtml);
            pdfOptions.FooterTemplate = !hasHeaderFooterStylesAdded ? InternalAddHeaderFooterStylesToHtml(html) : html;
        }

        await page.EmulateMediaTypeAsync(MediaType.Screen);
        await page.PdfAsync(outputFilePath, pdfOptions);
    }

    /// <summary>
    /// Applies extra styles to the given header / footer html because the default ones don't look good on the pdf.
    /// </summary>
    /// <param name="html">The header / footer html to add the styles to.</param>
    /// <returns>The html with added styles.</returns>
    private string InternalAddHeaderFooterStylesToHtml(string html)
        => _embeddedResourceService.GetResourceContent(HeaderFooterStylesFileName) + html;

    /// <summary>
    /// Inserts the document title into all elements containing the document-title class.
    /// </summary>
    /// <param name="html">Template html.</param>
    /// <returns>The html with inserted document-title.</returns>
    private string InternalFillHeaderFooterDocumentTitleClass(string html)
    {
        if (Options.DocumentTitle == null)
            return html;

        // need to wrap bc html could have multiple roots
        var htmlWrapped = $"<root>{html}</root>";
        var xDocument = XDocument.Parse(htmlWrapped);
        var titleElements = xDocument.XPathSelectElements($"//*[contains(@class, '{DocumentTitleClass}')]");

        foreach (var titleElement in titleElements)
            titleElement.Value = Options.DocumentTitle;

        var resultHtml = xDocument.ToString();

        // remove helper wrap
        var lines = resultHtml.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        resultHtml = string.Join(Environment.NewLine, lines.Take(lines.Length - 1).Skip(1));

        return resultHtml;
    }

    private async Task<IBrowser> InternalCreateBrowserAsync()
    {
        var launchOptions = new LaunchOptions
        {
            Headless = true,
            LogProcess = true,
            Args =
            [
                "--no-sandbox", "--disable-gpu", "--disable-setuid-sandbox", "--disable-dev-shm-usage"
            ] // needed for running inside docker
        };

        if (Options.ChromePath != null)
        {
            launchOptions.ExecutablePath = Options.ChromePath;
            return await Puppeteer.LaunchAsync(launchOptions);
        }

        var browserFetcher = new BrowserFetcher();
        var installed = browserFetcher.GetInstalledBrowsers().ToList();
        var hasDefaultRevisionInstalled = installed.Exists(installedBrowser =>
            string.Equals(installedBrowser.BuildId, Chrome.DefaultBuildId, StringComparison.Ordinal));

        if (!hasDefaultRevisionInstalled)
        {
            // Uninstall old revisions
            foreach (var oldBrowser in installed)
            {
                Console.WriteLine(
                    $"Uninstalling old Chrome version {oldBrowser.BuildId} from {browserFetcher.CacheDir}...");
                browserFetcher.Uninstall(oldBrowser.BuildId);
            }

            Console.WriteLine(
                $"Path to Chrome was not specified & default build is not installed. Downloading Chrome version {Chrome.DefaultBuildId} to {browserFetcher.CacheDir}...");
            _ = await browserFetcher.DownloadAsync(Chrome.DefaultBuildId);
        }

        return await Puppeteer.LaunchAsync(launchOptions);
    }

    /// <summary>
    /// Converts HTML content to a PDF file.
    /// </summary>
    /// <param name="htmlContent">The HTML content as a string.</param>
    /// <param name="outputPath">Optional output PDF file path. If not provided, uses default naming.</param>
    /// <returns>The path to the generated PDF file.</returns>
    public static async Task<string> ConvertHtml(string htmlContent, string? outputPath = null)
    {
        // Use PuppeteerSharp to generate PDF from HTML
        var pdfFileName = outputPath ?? Path.Combine(Directory.GetCurrentDirectory(), "output_from_html.pdf");
        await new BrowserFetcher().DownloadAsync();
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(htmlContent);
        await page.PdfAsync(pdfFileName, new PdfOptions
        {
            Format = PaperFormat.A4,
            DisplayHeaderFooter = false,
            PrintBackground = true
        });
        return pdfFileName;
    }

    /// <summary>
    /// Converts an HTML file to a PDF file.
    /// </summary>
    /// <param name="htmlFilePath">The path to the HTML file.</param>
    /// <param name="outputPath">Optional output PDF file path. If not provided, uses default naming.</param>
    /// <returns>The path to the generated PDF file.</returns>
    public static async Task<string> ConvertHtmlFile(string htmlFilePath, string? outputPath = null)
    {
        if (!File.Exists(htmlFilePath))
            throw new FileNotFoundException($"HTML file not found: {htmlFilePath}");
        var htmlContent = await File.ReadAllTextAsync(htmlFilePath);
        return await ConvertHtml(htmlContent, outputPath);
    }
}