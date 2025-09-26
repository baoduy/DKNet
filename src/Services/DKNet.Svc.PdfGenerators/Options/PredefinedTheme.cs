namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
/// Use a predefined theme.
/// </summary>
/// <param name="Type">The theme type to use.</param>
internal record PredefinedTheme(ThemeType Type) : Theme;

/// <summary>
/// All predefined themes.
/// </summary>
public enum ThemeType
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    None,
    Github,
    Latex,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}