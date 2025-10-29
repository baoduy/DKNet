// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: CodeHighlightTheme.cs
// Description: Represents a syntax highlighting theme (highlight.js stylesheet) for Markdown code blocks.

namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
///     Represents a syntax highlighting theme (highlight.js stylesheet) for Markdown code blocks.
/// </summary>
/// <remarks>
///     To view the CSS of all the themes visit
///     <see href="https://github.com/highlightjs/highlight.js/tree/main/src/styles">highlight.js/src/styles</see>.
/// </remarks>
public sealed record CodeHighlightTheme
{
    #region Fields

    private readonly string _sheetName = string.Empty;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new theme instance for the given stylesheet file name.
    /// </summary>
    /// <param name="theme">The highlight.js stylesheet file name (e.g. <c>"github.css"</c>).</param>
    private CodeHighlightTheme(string theme) => _sheetName = theme;

    /// <summary>
    ///     Initializes the <see cref="CodeHighlightTheme" /> representing no stylesheet (plain code blocks).
    /// </summary>
    private CodeHighlightTheme()
    {
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the accessible dark theme (a11y-dark.css).
    /// </summary>
    public static CodeHighlightTheme A11YDark => new("a11y-dark.css");

    /// <summary>
    ///     Gets the accessible light theme (a11y-light.css).
    /// </summary>
    public static CodeHighlightTheme A11YLight => new("a11y-light.css");

    /// <summary>
    ///     Gets the Agate theme (agate.css).
    /// </summary>
    public static CodeHighlightTheme Agate => new("agate.css");

    /// <summary>
    ///     Gets the Android Studio theme (androidstudio.css).
    /// </summary>
    public static CodeHighlightTheme AndroidStudio => new("androidstudio.css");

    /// <summary>
    ///     Gets the "An Old Hope" theme (an-old-hope.css).
    /// </summary>
    public static CodeHighlightTheme AnOldHope => new("an-old-hope.css");

    /// <summary>
    ///     Gets the Arduino Light theme (arduino-light.css).
    /// </summary>
    public static CodeHighlightTheme ArduinoLight => new("arduino-light.css");

    /// <summary>
    ///     Gets the Arta theme (arta.css).
    /// </summary>
    public static CodeHighlightTheme Arta => new("arta.css");

    /// <summary>
    ///     Gets the Ascetic theme (ascetic.css).
    /// </summary>
    public static CodeHighlightTheme Ascetic => new("ascetic.css");

    /// <summary>
    ///     Gets the Atom One Dark theme (atom-one-dark.css).
    /// </summary>
    public static CodeHighlightTheme AtomOneDark => new("atom-one-dark.css");

    /// <summary>
    ///     Gets the Atom One Dark Reasonable theme (atom-one-dark-reasonable.css).
    /// </summary>
    public static CodeHighlightTheme AtomOneDarkReasonable => new("atom-one-dark-reasonable.css");

    /// <summary>
    ///     Gets the Atom One Light theme (atom-one-light.css).
    /// </summary>
    public static CodeHighlightTheme AtomOneLight => new("atom-one-light.css");

    /// <summary>
    ///     Gets the Brown Paper theme (brown-paper.css).
    /// </summary>
    public static CodeHighlightTheme BrownPaper => new("brown-paper.css");

    /// <summary>
    ///     Gets the Brown Paper square PNG background theme (brown-papersq.png).
    /// </summary>
    public static CodeHighlightTheme BrownPaperSqPng => new("brown-papersq.png");

    /// <summary>
    ///     Gets the CodePen Embed theme (codepen-embed.css).
    /// </summary>
    public static CodeHighlightTheme CodepenEmbed => new("codepen-embed.css");

    /// <summary>
    ///     Gets the Color Brewer theme (color-brewer.css).
    /// </summary>
    public static CodeHighlightTheme ColorBrewer => new("color-brewer.css");

    /// <summary>
    ///     Gets the Dark theme (dark.css).
    /// </summary>
    public static CodeHighlightTheme Dark => new("dark.css");

    /// <summary>
    ///     Gets the Default theme (default.css).
    /// </summary>
    public static CodeHighlightTheme Default => new("default.css");

    /// <summary>
    ///     Gets the Devibeans theme (devibeans.css).
    /// </summary>
    public static CodeHighlightTheme Devibeans => new("devibeans.css");

    /// <summary>
    ///     Gets the Docco theme (docco.css).
    /// </summary>
    public static CodeHighlightTheme Docco => new("docco.css");

    /// <summary>
    ///     Gets the Far theme (far.css).
    /// </summary>
    public static CodeHighlightTheme Far => new("far.css");

    /// <summary>
    ///     Gets the Felipec theme (felipec.css).
    /// </summary>
    public static CodeHighlightTheme Felipec => new("felipec.css");

    /// <summary>
    ///     Gets the Foundation theme (foundation.css).
    /// </summary>
    public static CodeHighlightTheme Foundation => new("foundation.css");

    /// <summary>
    ///     Gets the GitHub light theme (github.css).
    /// </summary>
    public static CodeHighlightTheme Github => new("github.css");

    /// <summary>
    ///     Gets the GitHub dark theme (github-dark.css).
    /// </summary>
    public static CodeHighlightTheme GithubDark => new("github-dark.css");

    /// <summary>
    ///     Gets the GitHub dark dimmed theme (github-dark-dimmed.css).
    /// </summary>
    public static CodeHighlightTheme GithubDarkDimmed => new("github-dark-dimmed.css");

    /// <summary>
    ///     Gets the GML theme (gml.css).
    /// </summary>
    public static CodeHighlightTheme Gml => new("gml.css");

    /// <summary>
    ///     Gets the Googlecode theme (googlecode.css).
    /// </summary>
    public static CodeHighlightTheme Googlecode => new("googlecode.css");

    /// <summary>
    ///     Gets the Gradient Dark theme (gradient-dark.css).
    /// </summary>
    public static CodeHighlightTheme GradientDark => new("gradient-dark.css");

    /// <summary>
    ///     Gets the Gradient Light theme (gradient-light.css).
    /// </summary>
    public static CodeHighlightTheme GradientLight => new("gradient-light.css");

    /// <summary>
    ///     Gets the Grayscale theme (grayscale.css).
    /// </summary>
    public static CodeHighlightTheme Grayscale => new("grayscale.css");

    /// <summary>
    ///     Gets the Hybrid theme (hybrid.css).
    /// </summary>
    public static CodeHighlightTheme Hybrid => new("hybrid.css");

    /// <summary>
    ///     Gets the IntelliJ IDEA theme (idea.css).
    /// </summary>
    public static CodeHighlightTheme Idea => new("idea.css");

    /// <summary>
    ///     Gets the IntelliJ Light theme (intellij-light.css).
    /// </summary>
    public static CodeHighlightTheme IntellijLight => new("intellij-light.css");

    /// <summary>
    ///     Gets the IR Black theme (ir-black.css).
    /// </summary>
    public static CodeHighlightTheme IrBlack => new("ir-black.css");

    /// <summary>
    ///     Gets the ISBL Editor Dark theme (isbl-editor-dark.css).
    /// </summary>
    public static CodeHighlightTheme IsblEditorDark => new("isbl-editor-dark.css");

    /// <summary>
    ///     Gets the ISBL Editor Light theme (isbl-editor-light.css).
    /// </summary>
    public static CodeHighlightTheme IsblEditorLight => new("isbl-editor-light.css");

    /// <summary>
    ///     Gets the Kimbie Dark theme (kimbie-dark.css).
    /// </summary>
    public static CodeHighlightTheme KimbieDark => new("kimbie-dark.css");

    /// <summary>
    ///     Gets the Kimbie Light theme (kimbie-light.css).
    /// </summary>
    public static CodeHighlightTheme KimbieLight => new("kimbie-light.css");

    /// <summary>
    ///     Gets the Lightfair theme (lightfair.css).
    /// </summary>
    public static CodeHighlightTheme Lightfair => new("lightfair.css");

    /// <summary>
    ///     Gets the Lioshi theme (lioshi.css).
    /// </summary>
    public static CodeHighlightTheme Lioshi => new("lioshi.css");

    /// <summary>
    ///     Gets the Magula theme (magula.css).
    /// </summary>
    public static CodeHighlightTheme Magula => new("magula.css");

    /// <summary>
    ///     Gets the Mono Blue theme (mono-blue.css).
    /// </summary>
    public static CodeHighlightTheme MonoBlue => new("mono-blue.css");

    /// <summary>
    ///     Gets the Monokai theme (monokai.css).
    /// </summary>
    public static CodeHighlightTheme Monokai => new("monokai.css");

    /// <summary>
    ///     Gets the Monokai Sublime theme (monokai-sublime.css).
    /// </summary>
    public static CodeHighlightTheme MonokaiSublime => new("monokai-sublime.css");

    /// <summary>
    ///     Gets the Night Owl theme (night-owl.css).
    /// </summary>
    public static CodeHighlightTheme NightOwl => new("night-owl.css");

    /// <summary>
    ///     Gets the NNFX Dark theme (nnfx-dark.css).
    /// </summary>
    public static CodeHighlightTheme NnfxDark => new("nnfx-dark.css");

    /// <summary>
    ///     Gets the NNFX Light theme (nnfx-light.css).
    /// </summary>
    public static CodeHighlightTheme NnfxLight => new("nnfx-light.css");

    /// <summary>
    ///     Gets the theme representing no syntax stylesheet.
    /// </summary>
    public static CodeHighlightTheme None => new();

    /// <summary>
    ///     Gets the Nord theme (nord.css).
    /// </summary>
    public static CodeHighlightTheme Nord => new("nord.css");

    /// <summary>
    ///     Gets the Obsidian theme (obsidian.css).
    /// </summary>
    public static CodeHighlightTheme Obsidian => new("obsidian.css");

    /// <summary>
    ///     Gets the One C Light theme (1c-light.css).
    /// </summary>
    public static CodeHighlightTheme OneCLight => new("1c-light.css");

    /// <summary>
    ///     Gets the Panda Syntax Dark theme (panda-syntax-dark.css).
    /// </summary>
    public static CodeHighlightTheme PandaSyntaxDark => new("panda-syntax-dark.css");

    /// <summary>
    ///     Gets the Panda Syntax Light theme (panda-syntax-light.css).
    /// </summary>
    public static CodeHighlightTheme PandaSyntaxLight => new("panda-syntax-light.css");

    /// <summary>
    ///     Gets the Paraiso Dark theme (paraiso-dark.css).
    /// </summary>
    public static CodeHighlightTheme ParaisoDark => new("paraiso-dark.css");

    /// <summary>
    ///     Gets the Paraiso Light theme (paraiso-light.css).
    /// </summary>
    public static CodeHighlightTheme ParaisoLight => new("paraiso-light.css");

    /// <summary>
    ///     Gets the Pojoaque theme (pojoaque.css).
    /// </summary>
    public static CodeHighlightTheme Pojoaque => new("pojoaque.css");

    /// <summary>
    ///     Gets the Purebasic theme (purebasic.css).
    /// </summary>
    public static CodeHighlightTheme Purebasic => new("purebasic.css");

    /// <summary>
    ///     Gets the QtCreator Dark theme (qtcreator-dark.css).
    /// </summary>
    public static CodeHighlightTheme QtcreatorDark => new("qtcreator-dark.css");

    /// <summary>
    ///     Gets the QtCreator Light theme (qtcreator-light.css).
    /// </summary>
    public static CodeHighlightTheme QtcreatorLight => new("qtcreator-light.css");

    /// <summary>
    ///     Gets the Rainbow theme (rainbow.css).
    /// </summary>
    public static CodeHighlightTheme Rainbow => new("rainbow.css");

    /// <summary>
    ///     Gets the RouterOS theme (routeros.css).
    /// </summary>
    public static CodeHighlightTheme Routeros => new("routeros.css");

    /// <summary>
    ///     Gets the School Book theme (school-book.css).
    /// </summary>
    public static CodeHighlightTheme SchoolBook => new("school-book.css");

    /// <summary>
    ///     Gets the Shades of Purple theme (shades-of-purple.css).
    /// </summary>
    public static CodeHighlightTheme ShadesOfPurple => new("shades-of-purple.css");

    /// <summary>
    ///     Gets the Srcery theme (srcery.css).
    /// </summary>
    public static CodeHighlightTheme Srcery => new("srcery.css");

    /// <summary>
    ///     Gets the StackOverflow Dark theme (stackoverflow-dark.css).
    /// </summary>
    public static CodeHighlightTheme StackoverflowDark => new("stackoverflow-dark.css");

    /// <summary>
    ///     Gets the StackOverflow Light theme (stackoverflow-light.css).
    /// </summary>
    public static CodeHighlightTheme StackoverflowLight => new("stackoverflow-light.css");

    /// <summary>
    ///     Gets the Sunburst theme (sunburst.css).
    /// </summary>
    public static CodeHighlightTheme Sunburst => new("sunburst.css");

    /// <summary>
    ///     Gets the Tokyo Night Dark theme (tokyo-night-dark.css).
    /// </summary>
    public static CodeHighlightTheme TokyoNightDark => new("tokyo-night-dark.css");

    /// <summary>
    ///     Gets the Tokyo Night Light theme (tokyo-night-light.css).
    /// </summary>
    public static CodeHighlightTheme TokyoNightLight => new("tokyo-night-light.css");

    /// <summary>
    ///     Gets the Tomorrow Night Blue theme (tomorrow-night-blue.css).
    /// </summary>
    public static CodeHighlightTheme TomorrowNightBlue => new("tomorrow-night-blue.css");

    /// <summary>
    ///     Gets the Tomorrow Night Bright theme (tomorrow-night-bright.css).
    /// </summary>
    public static CodeHighlightTheme TomorrowNightBright => new("tomorrow-night-bright.css");

    /// <summary>
    ///     Gets the Visual Studio classic theme (vs.css).
    /// </summary>
    public static CodeHighlightTheme Vs => new("vs.css");

    /// <summary>
    ///     Gets the Visual Studio 2015 theme (vs2015.css).
    /// </summary>
    public static CodeHighlightTheme Vs2015 => new("vs2015.css");

    /// <summary>
    ///     Gets the Xcode theme (xcode.css).
    /// </summary>
    public static CodeHighlightTheme Xcode => new("xcode.css");

    /// <summary>
    ///     Gets the XT256 theme (xt256.css).
    /// </summary>
    public static CodeHighlightTheme Xt256 => new("xt256.css");

    #endregion

    #region Methods

    /// <summary>
    ///     Returns the underlying highlight.js stylesheet file name for this theme instance.
    /// </summary>
    /// <returns>The stylesheet file name, or an empty string when no theme is applied.</returns>
    public override string ToString() => _sheetName;

    #endregion
}