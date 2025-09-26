using DKNet.Svc.PdfGenerators.Options;

namespace DKNet.Svc.PdfGenerators.Services;

internal class MetadataService
{
    private readonly PdfGeneratorOptions _generatorOptions;
    private readonly IConvertionEvents _events;

    public MetadataService(PdfGeneratorOptions generatorOptions, IConvertionEvents events)
    {
        events.OnTemplateModelCreating += _AddTitleToTemplate;
        _generatorOptions = generatorOptions;
        _events = events;
    }

    private void _AddTitleToTemplate(object _, TemplateModelArgs e)
    {
        var title = _generatorOptions.MetadataTitle ?? _generatorOptions.DocumentTitle ?? _events.OutputFileName!;
        e.TemplateModel.Add("title", title);
    }
}