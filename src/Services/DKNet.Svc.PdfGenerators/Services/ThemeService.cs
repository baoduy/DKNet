using DKNet.Svc.PdfGenerators.Models;
using DKNet.Svc.PdfGenerators.Options;

namespace DKNet.Svc.PdfGenerators.Services;

internal class ThemeService
{
    #region Fields

    private readonly ModuleOptions _options;

    private readonly Theme _theme;

    private readonly IReadOnlyDictionary<ThemeType, ModuleInformation> _themeSourceMapping =
        new Dictionary<ThemeType, ModuleInformation>
        {
            {
                ThemeType.Github,
                new ModuleInformation(
                    "https://cdnjs.cloudflare.com/ajax/libs/github-markdown-css/5.5.1/github-markdown-light.min.css",
                    "github-markdown-css/github-markdown-light.css")
            },
            { ThemeType.Latex, new ModuleInformation("https://latex.now.sh/style.css", "latex.css/style.min.css") }
        };

    #endregion

    #region Constructors

    public ThemeService(Theme theme, ModuleOptions options, IConversionEvents events)
    {
        this._theme = theme;
        this._options = options;

        // adjust local dictionary paths
        if (options is NodeModuleOptions nodeModuleOptions)
        {
            var path = nodeModuleOptions.ModulePath;

            this._themeSourceMapping = ModuleInformation.UpdateDic(this._themeSourceMapping, path);
        }

        events.TemplateModelCreating += this.InternalAddThemeToTemplateAsync;
    }

    #endregion

    #region Methods

    internal async Task InternalAddThemeToTemplateAsync(object? sender, TemplateModelEventArgs e)
    {
        switch (this._theme)
        {
            case PredefinedTheme predefinedTheme when predefinedTheme.Type != ThemeType.None:
            {
                var value = this._themeSourceMapping[predefinedTheme.Type];
                e.TemplateModel.Add(StyleKey, this._options.IsRemote ? value.RemotePath : value.NodePath);
                break;
            }

            case CustomTheme customTheme:
                e.TemplateModel.Add(StyleKey, customTheme.CssPath);
                break;
        }

        await Task.CompletedTask;
    }

    #endregion

    private const string StyleKey = "stylePath";
}