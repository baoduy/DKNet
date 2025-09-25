using DKNet.Svc.PdfGenerator.Models;
using DKNet.Svc.PdfGenerator.Options;

namespace DKNet.Svc.PdfGenerator.Services;

internal class ThemeService
{
    private const string _STYLE_KEY = "stylePath";

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

    public ThemeService(Theme theme, ModuleOptions options, IConvertionEvents events)
    {
        _theme = theme;
        _options = options;

        // adjust local dictionary paths
        if (options is NodeModuleOptions nodeModuleOptions)
        {
            var path = nodeModuleOptions.ModulePath;

            _themeSourceMapping = ModuleInformation.UpdateDic(_themeSourceMapping, path);
        }

        events.OnTemplateModelCreating += _AddThemeToTemplate;
    }

    private void _AddThemeToTemplate(object sender, TemplateModelArgs e)
    {
        switch (_theme)
        {
            case PredefinedTheme predefinedTheme when predefinedTheme.Type != ThemeType.None:
            {
                var value = _themeSourceMapping[predefinedTheme.Type];
                e.TemplateModel.Add(_STYLE_KEY, _options.IsRemote ? value.RemotePath : value.NodePath);
                break;
            }

            case CustomTheme customTheme:
                e.TemplateModel.Add(_STYLE_KEY, customTheme.CssPath);
                break;
        }
    }
}