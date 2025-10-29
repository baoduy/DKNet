namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
///     The theme to use for styling the Markdown code blocks.
/// </summary>
/// <remarks>
///     To view the css of all the themes visit
///     <see href="https://github.com/highlightjs/highlight.js/tree/main/src/styles">highlight.js/src/styles</see>.
/// </remarks>
public sealed record CodeHighlightTheme
{
    #region Fields

    private readonly string _sheetName = string.Empty;

    #endregion

    #region Constructors

    private CodeHighlightTheme(string theme) => this._sheetName = theme;

    private CodeHighlightTheme()
    {
    }

    #endregion

    #region Properties

    /// <summary>
    ///     A11yDark field.
    /// </summary>
    public static CodeHighlightTheme A11yDark => new("a11y-dark.css");

    /// <summary>
    ///     A11yLight field.
    /// </summary>
    public static CodeHighlightTheme A11yLight => new("a11y-light.css");

    /// <summary>
    ///     Agate field.
    /// </summary>
    public static CodeHighlightTheme Agate => new("agate.css");

    public static CodeHighlightTheme AndroidStudio => new("androidstudio.css");

    /// <summary>
    ///     AnOldHope field.
    /// </summary>
    public static CodeHighlightTheme AnOldHope => new("an-old-hope.css");

    /// <summary>
    ///     ArduinoLight field.
    /// </summary>
    public static CodeHighlightTheme ArduinoLight => new("arduino-light.css");

    /// <summary>
    ///     Arta field.
    /// </summary>
    public static CodeHighlightTheme Arta => new("arta.css");

    public static CodeHighlightTheme Ascetic => new("ascetic.css");

    /// <summary>
    ///     AtomOneDark field.
    /// </summary>
    public static CodeHighlightTheme AtomOneDark => new("atom-one-dark.css");

    /// <summary>
    ///     AtomOneDarkReasonable field.
    /// </summary>
    public static CodeHighlightTheme AtomOneDarkReasonable => new("atom-one-dark-reasonable.css");

    /// <summary>
    ///     AtomOneLight field.
    /// </summary>
    public static CodeHighlightTheme AtomOneLight => new("atom-one-light.css");

    public static CodeHighlightTheme BrownPaper => new("brown-paper.css");

    /// <summary>
    ///     BrownPaperSqPng field.
    /// </summary>
    public static CodeHighlightTheme BrownPaperSqPng => new("brown-papersq.png");

    /// <summary>
    ///     CodepenEmbed field.
    /// </summary>
    public static CodeHighlightTheme CodepenEmbed => new("codepen-embed.css");

    /// <summary>
    ///     ColorBrewer field.
    /// </summary>
    public static CodeHighlightTheme ColorBrewer => new("color-brewer.css");

    public static CodeHighlightTheme Dark => new("dark.css");

    /// <summary>
    ///     Default field.
    /// </summary>
    public static CodeHighlightTheme Default => new("default.css");

    /// <summary>
    ///     Devibeans field.
    /// </summary>
    public static CodeHighlightTheme Devibeans => new("devibeans.css");

    /// <summary>
    ///     Docco field.
    /// </summary>
    public static CodeHighlightTheme Docco => new("docco.css");

    public static CodeHighlightTheme Far => new("far.css");

    /// <summary>
    ///     Felipec field.
    /// </summary>
    public static CodeHighlightTheme Felipec => new("felipec.css");

    /// <summary>
    ///     Foundation field.
    /// </summary>
    public static CodeHighlightTheme Foundation => new("foundation.css");

    /// <summary>
    ///     Github field.
    /// </summary>
    public static CodeHighlightTheme Github => new("github.css");

    public static CodeHighlightTheme GithubDark => new("github-dark.css");

    /// <summary>
    ///     GithubDarkDimmed field.
    /// </summary>
    public static CodeHighlightTheme GithubDarkDimmed => new("github-dark-dimmed.css");

    /// <summary>
    ///     Gml field.
    /// </summary>
    public static CodeHighlightTheme Gml => new("gml.css");

    /// <summary>
    ///     Googlecode field.
    /// </summary>
    public static CodeHighlightTheme Googlecode => new("googlecode.css");

    public static CodeHighlightTheme GradientDark => new("gradient-dark.css");

    /// <summary>
    ///     GradientLight field.
    /// </summary>
    public static CodeHighlightTheme GradientLight => new("gradient-light.css");

    /// <summary>
    ///     Grayscale field.
    /// </summary>
    public static CodeHighlightTheme Grayscale => new("grayscale.css");

    /// <summary>
    ///     Hybrid field.
    /// </summary>
    public static CodeHighlightTheme Hybrid => new("hybrid.css");

    public static CodeHighlightTheme Idea => new("idea.css");

    /// <summary>
    ///     IntellijLight field.
    /// </summary>
    public static CodeHighlightTheme IntellijLight => new("intellij-light.css");

    /// <summary>
    ///     IrBlack field.
    /// </summary>
    public static CodeHighlightTheme IrBlack => new("ir-black.css");

    /// <summary>
    ///     IsblEditorDark field.
    /// </summary>
    public static CodeHighlightTheme IsblEditorDark => new("isbl-editor-dark.css");

    public static CodeHighlightTheme IsblEditorLight => new("isbl-editor-light.css");

    /// <summary>
    ///     KimbieDark field.
    /// </summary>
    public static CodeHighlightTheme KimbieDark => new("kimbie-dark.css");

    /// <summary>
    ///     KimbieLight field.
    /// </summary>
    public static CodeHighlightTheme KimbieLight => new("kimbie-light.css");

    /// <summary>
    ///     Lightfair field.
    /// </summary>
    public static CodeHighlightTheme Lightfair => new("lightfair.css");

    public static CodeHighlightTheme Lioshi => new("lioshi.css");

    /// <summary>
    ///     Magula field.
    /// </summary>
    public static CodeHighlightTheme Magula => new("magula.css");

    /// <summary>
    ///     MonoBlue field.
    /// </summary>
    public static CodeHighlightTheme MonoBlue => new("mono-blue.css");

    /// <summary>
    ///     Monokai field.
    /// </summary>
    public static CodeHighlightTheme Monokai => new("monokai.css");

    public static CodeHighlightTheme MonokaiSublime => new("monokai-sublime.css");

    /// <summary>
    ///     NightOwl field.
    /// </summary>
    public static CodeHighlightTheme NightOwl => new("night-owl.css");

    /// <summary>
    ///     NnfxDark field.
    /// </summary>
    public static CodeHighlightTheme NnfxDark => new("nnfx-dark.css");

    /// <summary>
    ///     NnfxLight field.
    /// </summary>
    public static CodeHighlightTheme NnfxLight => new("nnfx-light.css");

    /// <summary>
    ///     Apply no theme.
    /// </summary>
    public static CodeHighlightTheme None => new();

    /// <summary>
    ///     Nord field.
    /// </summary>
    public static CodeHighlightTheme Nord => new("nord.css");

    /// <summary>
    ///     Obsidian field.
    /// </summary>
    public static CodeHighlightTheme Obsidian => new("obsidian.css");

    public static CodeHighlightTheme OneCLight => new("1c-light.css");

    /// <summary>
    ///     PandaSyntaxDark field.
    /// </summary>
    public static CodeHighlightTheme PandaSyntaxDark => new("panda-syntax-dark.css");

    /// <summary>
    ///     PandaSyntaxLight field.
    /// </summary>
    public static CodeHighlightTheme PandaSyntaxLight => new("panda-syntax-light.css");

    /// <summary>
    ///     ParaisoDark field.
    /// </summary>
    public static CodeHighlightTheme ParaisoDark => new("paraiso-dark.css");

    public static CodeHighlightTheme ParaisoLight => new("paraiso-light.css");

    /// <summary>
    ///     Pojoaque field.
    /// </summary>
    public static CodeHighlightTheme Pojoaque => new("pojoaque.css");

    /// <summary>
    ///     Purebasic field.
    /// </summary>
    public static CodeHighlightTheme Purebasic => new("purebasic.css");

    /// <summary>
    ///     QtcreatorDark field.
    /// </summary>
    public static CodeHighlightTheme QtcreatorDark => new("qtcreator-dark.css");

    public static CodeHighlightTheme QtcreatorLight => new("qtcreator-light.css");

    /// <summary>
    ///     Rainbow field.
    /// </summary>
    public static CodeHighlightTheme Rainbow => new("rainbow.css");

    /// <summary>
    ///     Routeros field.
    /// </summary>
    public static CodeHighlightTheme Routeros => new("routeros.css");

    /// <summary>
    ///     SchoolBook field.
    /// </summary>
    public static CodeHighlightTheme SchoolBook => new("school-book.css");

    public static CodeHighlightTheme ShadesOfPurple => new("shades-of-purple.css");

    /// <summary>
    ///     Srcery field.
    /// </summary>
    public static CodeHighlightTheme Srcery => new("srcery.css");

    /// <summary>
    ///     StackoverflowDark field.
    /// </summary>
    public static CodeHighlightTheme StackoverflowDark => new("stackoverflow-dark.css");

    /// <summary>
    ///     StackoverflowLight field.
    /// </summary>
    public static CodeHighlightTheme StackoverflowLight => new("stackoverflow-light.css");

    public static CodeHighlightTheme Sunburst => new("sunburst.css");

    /// <summary>
    ///     TokyoNightDark field.
    /// </summary>
    public static CodeHighlightTheme TokyoNightDark => new("tokyo-night-dark.css");

    /// <summary>
    ///     TokyoNightLight field.
    /// </summary>
    public static CodeHighlightTheme TokyoNightLight => new("tokyo-night-light.css");

    /// <summary>
    ///     TomorrowNightBlue field.
    /// </summary>
    public static CodeHighlightTheme TomorrowNightBlue => new("tomorrow-night-blue.css");

    public static CodeHighlightTheme TomorrowNightBright => new("tomorrow-night-bright.css");

    /// <summary>
    ///     Vs field.
    /// </summary>
    public static CodeHighlightTheme Vs => new("vs.css");

    /// <summary>
    ///     Vs2015 field.
    /// </summary>
    public static CodeHighlightTheme Vs2015 => new("vs2015.css");

    /// <summary>
    ///     Xcode field.
    /// </summary>
    public static CodeHighlightTheme Xcode => new("xcode.css");

    public static CodeHighlightTheme Xt256 => new("xt256.css");

    #endregion

    #region Methods

    /// <summary>
    ///     Returns the css file name of the theme.
    /// </summary>
    /// <summary>
    ///     ToString operation.
    /// </summary>
    public override string ToString() => this._sheetName;

    #endregion
}