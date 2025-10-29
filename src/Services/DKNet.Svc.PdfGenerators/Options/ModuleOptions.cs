namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
///     Options that decide from where to load additional modules.
/// </summary>
/// <remarks>
///     For the option <see cref="FromLocalPath(string)" /> the <i>npm</i> packages specified in the <i>README</i>
///     need to be installed.
/// </remarks>
public record ModuleOptions
{
    #region Constructors

    /// <summary>
    ///     Creates a new instance of <see cref="ModuleOptions" />.
    /// </summary>
    /// <param name="moduleLocation">Location from where to load the modules.</param>
    protected internal ModuleOptions(ModuleLocation moduleLocation) => this.ModuleLocation = moduleLocation;

    #endregion

    #region Properties

    internal bool IsRemote => this.ModuleLocation == ModuleLocation.Remote;

    /// <summary>
    ///     Provides information from where to load modules.
    /// </summary>
    /// <summary>
    ///     Gets or sets ModuleLocation.
    /// </summary>
    public ModuleLocation ModuleLocation { get; }

    /// <summary>
    ///     Don't load any additional modules. With this only basic markdown features are enabled.
    /// </summary>
    public static ModuleOptions None => new(ModuleLocation.None);

    /// <summary>
    ///     Loads the <i>node_modules</i> over a CDN e.g. <see href="https://cdn.jsdelivr.net" />.
    /// </summary>
    /// <remarks>This option requires an internet connection.</remarks>
    public static ModuleOptions Remote => new(ModuleLocation.Remote);

    #endregion

    #region Methods

    /// <summary>
    ///     Loads the <i>node_modules</i> from the given (local) <i>npm</i> directory.
    /// </summary>
    /// <param name="modulePath">The path to the <i>node_module</i> directory.</param>
    /// <remarks>
    ///     For this to work, following <i>npm</i> packages need to be installed:
    ///     <code language="bash">
    /// npm i mathjax@3
    /// npm i mermaid@10
    /// npm i font-awesome
    /// npm i @highlightjs/cdn-assets@11
    /// npm i github-markdown-css
    /// npm i latex.css
    /// </code>
    /// </remarks>
    public static ModuleOptions FromLocalPath(string modulePath) => new NodeModuleOptions(modulePath);

    #endregion
}