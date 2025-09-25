using DKNet.Svc.PdfGenerator.Options;

namespace DKNet.Svc.PdfGenerator.Services;

internal class MetadataService
{
    private readonly Markdown2PdfOptions _options;
    private readonly IConvertionEvents _events;

    public MetadataService(Markdown2PdfOptions options, IConvertionEvents events)
    {
        events.OnTemplateModelCreating += _AddTitleToTemplate;
        _options = options;
        _events = events;
    }

    private void _AddTitleToTemplate(object _, TemplateModelArgs e)
    {
        var title = _options.MetadataTitle ?? _options.DocumentTitle ?? _events.OutputFileName!;
        e.TemplateModel.Add("title", title);
    }
}