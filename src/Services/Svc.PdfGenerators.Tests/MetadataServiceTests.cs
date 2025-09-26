using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using DKNet.Svc.PdfGenerators.Services;

namespace Svc.PdfGenerators.Tests;

public class MetadataServiceTests
{
    [Fact]
    public async Task AddTitleToTemplateAsync_AddsTitleToModel()
    {
        var templateModel = new Dictionary<string, string>();
        var args = new TemplateModelEventArgs(templateModel);
        var events = new TestConversionEvents();
        var options = new PdfGeneratorOptions { MetadataTitle = "MetaTitle", DocumentTitle = "DocTitle" };
        var service = new MetadataService(options, events);
        // Directly call the handler instead of invoking the event
        await service.AddTitleToTemplateAsync(service, args);
        Assert.Equal("MetaTitle", templateModel["title"]);
    }

    private class TestConversionEvents : IConversionEvents
    {
        public string? OutputFileName => "output.pdf";
        public event EventHandler<MarkdownEventArgs>? BeforeHtmlConversion;
        public event Func<object, TemplateModelEventArgs, Task>? OnTemplateModelCreatingAsync;
        public event EventHandler<PdfEventArgs>? OnTempPdfCreatedEvent;
    }
}