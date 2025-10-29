namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
///     A theme from a CSS file.
/// </summary>
/// <param name="CssPath">Path to the CSS file to use as the theme.</param>
/// <summary>
///     CustomTheme operation.
/// </summary>
public record CustomTheme(string CssPath) : Theme;