using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using DKNet.Svc.PdfGenerators.Services;

namespace Svc.PdfGenerators.Tests;

public class ModuleServiceTests
{
    [Fact]
    public async Task AddModulesToTemplateAsync_AddsModulePathsToModel()
    {
        var templateModel = new Dictionary<string, string>();
        var args = new TemplateModelEventArgs(templateModel);
        var events = new TestConversionEvents();
        var options = new ModuleOptions(ModuleLocation.Custom);
        var service = new ModuleService(options, events);
        // Directly call the handler instead of invoking the event
        await service.AddModulesToTemplateAsync(service, args);
        Assert.Contains("mathjax", templateModel.Keys);
        Assert.Contains("mermaid", templateModel.Keys);
        Assert.Contains("highlightjs", templateModel.Keys);
        Assert.Contains("fontawesome", templateModel.Keys);
    }

    private class TestConversionEvents : IConversionEvents
    {
        public string? OutputFileName => "output.pdf";
        public event EventHandler<MarkdownEventArgs>? BeforeHtmlConversion;
        public event Func<object, TemplateModelEventArgs, Task>? OnTemplateModelCreatingAsync;
        public event EventHandler<PdfEventArgs>? OnTempPdfCreatedEvent;
    }
}