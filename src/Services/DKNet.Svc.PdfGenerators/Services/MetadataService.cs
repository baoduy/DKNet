using DKNet.Svc.PdfGenerators.Options;

namespace DKNet.Svc.PdfGenerators.Services;

internal class MetadataService
{
    #region Fields

    private readonly IConversionEvents _events;
    private readonly PdfGeneratorOptions _generatorOptions;

    #endregion

    #region Constructors

    public MetadataService(PdfGeneratorOptions generatorOptions, IConversionEvents events)
    {
        events.OnTemplateModelCreatingAsync += AddTitleToTemplateAsync;
        _generatorOptions = generatorOptions;
        _events = events;
    }

    #endregion

    #region Methods

    internal async Task AddTitleToTemplateAsync(object _, TemplateModelEventArgs e)
    {
        var title = _generatorOptions.MetadataTitle ?? _generatorOptions.DocumentTitle ?? _events.OutputFileName!;
        e.TemplateModel.Add("title", title);
        await Task.CompletedTask;
    }

    #endregion
}