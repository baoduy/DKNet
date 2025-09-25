using DKNet.Svc.PdfGenerator.Models;
using DKNet.Svc.PdfGenerator.Options;

namespace DKNet.Svc.PdfGenerator.Services;

internal class ModuleService
{
    private readonly IReadOnlyDictionary<string, ModuleInformation> _packagelocationMapping =
        new Dictionary<string, ModuleInformation>
        {
            {
                "mathjax",
                new ModuleInformation("https://cdn.jsdelivr.net/npm/mathjax@3/es5/tex-mml-chtml.js",
                    "mathjax/es5/tex-mml-chtml.js")
            },
            {
                "mermaid",
                new ModuleInformation("https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js",
                    "mermaid/dist/mermaid.min.js")
            },
            {
                "highlightjs",
                new ModuleInformation("https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js",
                    "@highlightjs/cdn-assets/highlight.min.js")
            },
            {
                "highlightjs_style",
                new ModuleInformation("https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles",
                    "@highlightjs/cdn-assets/styles")
            },
            {
                "fontawesome",
                new ModuleInformation("https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.1/css/all.min.css",
                    "font-awesome/css/font-awesome.min.css")
            }
        };

    private readonly ModuleOptions _options;

    public ModuleService(ModuleOptions options, IConvertionEvents events)
    {
        _options = options;

        // adjust local dictionary paths
        if (options is NodeModuleOptions nodeModuleOptions)
        {
            var path = nodeModuleOptions.ModulePath;

            _packagelocationMapping = ModuleInformation.UpdateDic(_packagelocationMapping, path);
        }

        events.OnTemplateModelCreating += _AddModulesToTemplate;
    }

    private void _AddModulesToTemplate(object sender, TemplateModelArgs e)
    {
        // load correct module paths
        foreach (var kvp in _packagelocationMapping)
            e.TemplateModel.Add(kvp.Key, _options.IsRemote ? kvp.Value.RemotePath : kvp.Value.NodePath);
    }
}