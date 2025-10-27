using DKNet.Svc.PdfGenerators.Services;
using PuppeteerSharp.Media;

namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
///     The <see cref="PdfGeneratorOptions" /> in a serializable format.
/// </summary>
public class SerializableOptions
{
    #region Properties

    /// <inheritdoc cref="PdfGeneratorOptions.ChromePath" />
    public string? ChromePath { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.CodeHighlightTheme" />
    public string? CodeHighlightTheme { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.CustomHeadContent" />
    public string? CustomHeadContent { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.DocumentTitle" />
    public string? DocumentTitle { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.EnableAutoLanguageDetection" />
    public bool? EnableAutoLanguageDetection { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.FooterHtml" />
    public string? FooterHtml { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.Format" />
    public string? Format { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.HeaderHtml" />
    public string? HeaderHtml { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.IsLandscape" />
    public bool? IsLandscape { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.KeepHtml" />
    public bool? KeepHtml { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.MarginOptions" />
    public MarginOptions? MarginOptions { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.MetadataTitle" />
    public string? MetadataTitle { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.ModuleOptions" />
    public string? ModuleOptions { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.Scale" />
    public decimal? Scale { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.TableOfContents" />
    public TableOfContentsOptions? TableOfContents { get; set; } = null;

    /// <inheritdoc cref="PdfGeneratorOptions.Theme" />
    public string? Theme { get; set; }

    #endregion

    #region Methods

    /// <summary>
    ///     Converts this serializable options into proper <see cref="PdfGeneratorOptions" />.
    /// </summary>
    /// <returns>The deserialized <see cref="PdfGeneratorOptions" />.</returns>
    public PdfGeneratorOptions ToPdfGeneratorOptions()
    {
        var options = new PdfGeneratorOptions();

        if (ModuleOptions != null)
            options.ModuleOptions =
                PropertyService.TryGetPropertyValue<ModuleOptions, ModuleOptions>(ModuleOptions, out var moduleOptions)
                    ? moduleOptions
                    : Options.ModuleOptions.FromLocalPath(ModuleOptions);

        if (Theme != null)
            options.Theme = PropertyService.TryGetPropertyValue<Theme, Theme>(Theme, out var theme)
                ? theme
                : Options.Theme.Custom(Theme);

        if (CodeHighlightTheme != null
            && PropertyService.TryGetPropertyValue<CodeHighlightTheme, CodeHighlightTheme>(CodeHighlightTheme,
                out var codeHighlightTheme))
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

        if (Format != null && PropertyService.TryGetPropertyValue<PaperFormat, PaperFormat>(Format, out var format))
            options.Format = format;

        if (Scale != null)
            options.Scale = Scale.Value;

        if (TableOfContents != null)
            options.TableOfContents = TableOfContents;

        return options;
    }

    #endregion
}