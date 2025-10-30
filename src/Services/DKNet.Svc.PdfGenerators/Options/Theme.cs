namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
///     The theme to use for styling the document.
/// </summary>
public abstract record Theme
{
    #region Properties

    /// <summary>
    ///     Githubs markdown theme.
    /// </summary>
    /// <remarks>
    ///     If the option <see cref="ModuleOptions.FromLocalPath(string)" /> is being used,
    ///     the <i>npm</i>-package <c>github-markdown-css</c> needs to be installed in the corresponding location.
    /// </remarks>
    /// <summary>
    ///     Github field.
    /// </summary>
    public static Theme Github => new PredefinedTheme(ThemeType.Github);

    /// <summary>
    ///     Latex like document styling.
    /// </summary>
    /// <remarks>
    ///     If the option <see cref="ModuleOptions.FromLocalPath(string)" /> is being used,
    ///     the <i>npm</i>-package <c>latex.css</c> needs to be installed in the corresponding location.
    /// </remarks>
    public static Theme Latex => new PredefinedTheme(ThemeType.Latex);

    /// <summary>
    ///     Don't apply any theme to the document.
    /// </summary>
    public static Theme None => new PredefinedTheme(ThemeType.None);

    #endregion

    #region Methods

    /// <summary>
    ///     Define your own theme.
    /// </summary>
    /// <param name="cssPath">Path to the css containing the styles.</param>
    /// <returns>The generated PredefinedTheme</returns>
    /// <summary>
    ///     Custom operation.
    /// </summary>
    public static Theme Custom(string cssPath) => new CustomTheme(cssPath);

    #endregion
}