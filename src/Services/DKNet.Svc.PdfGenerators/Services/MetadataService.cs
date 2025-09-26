using DKNet.Svc.PdfGenerators.Options;

namespace DKNet.Svc.PdfGenerators.Services;

internal class MetadataService
{
    private readonly PdfGeneratorOptions _generatorOptions;
    private readonly IConversionEvents _events;

    public MetadataService(PdfGeneratorOptions generatorOptions, IConversionEvents events)
    {
        events.OnTemplateModelCreatingAsync += AddTitleToTemplateAsync;
        _generatorOptions = generatorOptions;
        _events = events;
    }

    internal async Task AddTitleToTemplateAsync(object _, TemplateModelEventArgs e)
    {
        var title = _generatorOptions.MetadataTitle ?? _generatorOptions.DocumentTitle ?? _events.OutputFileName!;
        e.TemplateModel.Add("title", title);
        await Task.CompletedTask;
    }
}