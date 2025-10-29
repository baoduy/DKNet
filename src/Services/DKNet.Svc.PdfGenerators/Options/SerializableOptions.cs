using DKNet.Svc.PdfGenerators.Services;
using PuppeteerSharp.Media;

namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
///     The <see cref="PdfGeneratorOptions" /> in a serializable format.
/// </summary>
/// <summary>
///     Provides SerializableOptions functionality.
/// </summary>
public class SerializableOptions
{
    #region Properties

    /// <inheritdoc cref="PdfGeneratorOptions.EnableAutoLanguageDetection" />
    public bool? EnableAutoLanguageDetection { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.IsLandscape" />
    public bool? IsLandscape { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.KeepHtml" />
    public bool? KeepHtml { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.Scale" />
    public decimal? Scale { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.MarginOptions" />
    public MarginOptions? MarginOptions { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.ChromePath" />
    public string? ChromePath { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.CodeHighlightTheme" />
    public string? CodeHighlightTheme { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.CustomHeadContent" />
    public string? CustomHeadContent { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.DocumentTitle" />
    public string? DocumentTitle { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.FooterHtml" />
    public string? FooterHtml { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.Format" />
    public string? Format { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.HeaderHtml" />
    public string? HeaderHtml { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.MetadataTitle" />
    public string? MetadataTitle { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.ModuleOptions" />
    public string? ModuleOptions { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.Theme" />
    public string? Theme { get; set; }

    /// <inheritdoc cref="PdfGeneratorOptions.TableOfContents" />
    public TableOfContentsOptions? TableOfContents { get; set; }

    #endregion

    #region Methods

    /// <summary>
    ///     Converts this serializable options into proper <see cref="PdfGeneratorOptions" />.
    /// </summary>
    /// <returns>The deserialized <see cref="PdfGeneratorOptions" />.</returns>
    /// <summary>
    ///     ToPdfGeneratorOptions operation.
    /// </summary>
    public PdfGeneratorOptions ToPdfGeneratorOptions()
    {
        var options = new PdfGeneratorOptions();

        if (this.ModuleOptions != null)
        {
            options.ModuleOptions =
                PropertyService.TryGetPropertyValue<ModuleOptions, ModuleOptions>(
                    this.ModuleOptions,
                    out var moduleOptions)
                    ? moduleOptions
                    : Options.ModuleOptions.FromLocalPath(this.ModuleOptions);
        }

        if (this.Theme != null)
        {
            options.Theme = PropertyService.TryGetPropertyValue<Theme, Theme>(this.Theme, out var theme)
                ? theme
                : Options.Theme.Custom(this.Theme);
        }

        if (this.CodeHighlightTheme != null
            && PropertyService.TryGetPropertyValue<CodeHighlightTheme, CodeHighlightTheme>(
                this.CodeHighlightTheme,
                out var codeHighlightTheme))
        {
            options.CodeHighlightTheme = codeHighlightTheme;
        }

        if (this.EnableAutoLanguageDetection != null)
        {
            options.EnableAutoLanguageDetection = this.EnableAutoLanguageDetection.Value;
        }

        if (this.HeaderHtml != null)
        {
            options.HeaderHtml = this.HeaderHtml;
        }

        if (this.FooterHtml != null)
        {
            options.FooterHtml = this.FooterHtml;
        }

        if (this.DocumentTitle != null)
        {
            options.DocumentTitle = this.DocumentTitle;
        }

        if (this.MetadataTitle != null)
        {
            options.MetadataTitle = this.MetadataTitle;
        }

        if (this.CustomHeadContent != null)
        {
            options.CustomHeadContent = this.CustomHeadContent;
        }

        if (this.ChromePath != null)
        {
            options.ChromePath = this.ChromePath;
        }

        if (this.KeepHtml != null)
        {
            options.KeepHtml = this.KeepHtml.Value;
        }

        if (this.MarginOptions != null)
        {
            options.MarginOptions = this.MarginOptions;
        }

        if (this.IsLandscape != null)
        {
            options.IsLandscape = this.IsLandscape.Value;
        }

        if (this.Format != null &&
            PropertyService.TryGetPropertyValue<PaperFormat, PaperFormat>(this.Format, out var format))
        {
            options.Format = format;
        }

        if (this.Scale != null)
        {
            options.Scale = this.Scale.Value;
        }

        if (this.TableOfContents != null)
        {
            options.TableOfContents = this.TableOfContents;
        }

        return options;
    }

    #endregion
}