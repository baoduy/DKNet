using DKNet.Svc.PdfGenerators.Models;
using DKNet.Svc.PdfGenerators.Options;

namespace DKNet.Svc.PdfGenerators.Services;

internal class ThemeService
{
    private const string StyleKey = "stylePath";

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

    private readonly Theme _theme;
    private readonly ModuleOptions _options;

    public ThemeService(Theme theme, ModuleOptions options, IConversionEvents events)
    {
        _theme = theme;
        _options = options;

        // adjust local dictionary paths
        if (options is NodeModuleOptions nodeModuleOptions)
        {
            var path = nodeModuleOptions.ModulePath;

            _themeSourceMapping = ModuleInformation.UpdateDic(_themeSourceMapping, path);
        }

        events.OnTemplateModelCreatingAsync += InternalAddThemeToTemplateAsync;
    }

    internal async Task InternalAddThemeToTemplateAsync(object sender, TemplateModelEventArgs e)
    {
        switch (_theme)
        {
            case PredefinedTheme predefinedTheme when predefinedTheme.Type != ThemeType.None:
            {
                var value = _themeSourceMapping[predefinedTheme.Type];
                e.TemplateModel.Add(StyleKey, _options.IsRemote ? value.RemotePath : value.NodePath);
                break;
            }

            case CustomTheme customTheme:
                e.TemplateModel.Add(StyleKey, customTheme.CssPath);
                break;
        }

        await Task.CompletedTask;
    }
}