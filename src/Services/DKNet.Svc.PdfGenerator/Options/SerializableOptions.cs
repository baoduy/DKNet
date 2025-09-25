using DKNet.Svc.PdfGenerator.Services;
using PuppeteerSharp.Media;

namespace DKNet.Svc.PdfGenerator.Options;

/// <summary>
/// The <see cref="Markdown2PdfOptions"/> in a serializable format.
/// </summary>
public class SerializableOptions
{
    /// <inheritdoc cref="Markdown2PdfOptions.ModuleOptions"/>
    public string? ModuleOptions { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.Theme"/>
    public string? Theme { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.CodeHighlightTheme"/>
    public string? CodeHighlightTheme { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.EnableAutoLanguageDetection"/>
    public bool? EnableAutoLanguageDetection { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.HeaderHtml"/>
    public string? HeaderHtml { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.FooterHtml"/>
    public string? FooterHtml { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.DocumentTitle"/>
    public string? DocumentTitle { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.MetadataTitle"/>
    public string? MetadataTitle { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.CustomHeadContent"/>
    public string? CustomHeadContent { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.ChromePath"/>
    public string? ChromePath { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.KeepHtml"/>
    public bool? KeepHtml { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.MarginOptions"/>
    public MarginOptions? MarginOptions { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.IsLandscape"/>
    public bool? IsLandscape { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.Format"/>
    public string? Format { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.Scale"/>
    public decimal? Scale { get; set; }

    /// <inheritdoc cref="Markdown2PdfOptions.TableOfContents"/>
    public TableOfContentsOptions? TableOfContents { get; set; } = null;

    /// <summary>
    /// Converts this serializable options into proper <see cref="Markdown2PdfOptions"/>.
    /// </summary>
    /// <returns>The deserialized <see cref="Markdown2PdfOptions"/>.</returns>
    public Markdown2PdfOptions ToMarkdown2PdfOptions()
    {
        var options = new Markdown2PdfOptions();

        if (ModuleOptions != null)
            options.ModuleOptions =
                PropertyService.TryGetPropertyValue<ModuleOptions>(ModuleOptions, out var moduleOptions)
                    ? moduleOptions
                    : Options.ModuleOptions.FromLocalPath(ModuleOptions);

        if (Theme != null)
            options.Theme = PropertyService.TryGetPropertyValue<Theme>(Theme, out var theme)
                ? theme
                : Options.Theme.Custom(Theme);

        if (CodeHighlightTheme != null
            && PropertyService.TryGetPropertyValue<CodeHighlightTheme>(CodeHighlightTheme, out var codeHighlightTheme))
            options.CodeHighlightTheme = codeHighlightTheme;

        if (EnableAutoLanguageDetection != null)
            options.EnableAutoLanguageDetection = EnableAutoLanguageDetection.Value;

        if (HeaderHtml != null)
            options.HeaderHtml = HeaderHtml;

        if (FooterHtml != null)
            options.FooterHtml = FooterHtml;

        if (DocumentTitle != null)
            options.DocumentTitle = DocumentTitle;

        if (MetadataTitle != null)
            options.MetadataTitle = MetadataTitle;

        if (CustomHeadContent != null)
            options.CustomHeadContent = CustomHeadContent;

        if (ChromePath != null)
            options.ChromePath = ChromePath;

        if (KeepHtml != null)
            options.KeepHtml = KeepHtml.Value;

        if (MarginOptions != null)
            options.MarginOptions = MarginOptions;

        if (IsLandscape != null)
            options.IsLandscape = IsLandscape.Value;

        if (Format != null && PropertyService.TryGetPropertyValue<PaperFormat>(Format, out var format))
            options.Format = format;

        if (Scale != null)
            options.Scale = Scale.Value;

        if (TableOfContents != null)
            options.TableOfContents = TableOfContents;

        return options;
    }
}