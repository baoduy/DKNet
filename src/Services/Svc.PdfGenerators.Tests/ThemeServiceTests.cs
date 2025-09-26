using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using DKNet.Svc.PdfGenerators.Services;

namespace Svc.PdfGenerators.Tests;

public class ThemeServiceTests
{
    [Fact]
    public async Task InternalAddThemeToTemplateAsync_AddsStylePath_ForPredefinedTheme()
    {
        var templateModel = new Dictionary<string, string>();
        var args = new TemplateModelEventArgs(templateModel);
        var events = new TestConversionEvents();
        var theme = new PredefinedTheme(ThemeType.Github);
        var options = new ModuleOptions(ModuleLocation.Custom);
        var service = new ThemeService(theme, options, events);
        // Directly call the handler instead of invoking the event
        await service.InternalAddThemeToTemplateAsync(service, args);
        Assert.True(templateModel.ContainsKey("stylePath"));
        Assert.Contains("github-markdown", templateModel["stylePath"]);
    }

    [Fact]
    public async Task InternalAddThemeToTemplateAsync_AddsStylePath_ForCustomTheme()
    {
        var templateModel = new Dictionary<string, string>();
        var args = new TemplateModelEventArgs(templateModel);
        var events = new TestConversionEvents();
        var theme = new CustomTheme("custom.css");
        var options = new ModuleOptions(ModuleLocation.Custom);
        var service = new ThemeService(theme, options, events);
        // Directly call the handler instead of invoking the event
        await service.InternalAddThemeToTemplateAsync(service, args);
        Assert.Equal("custom.css", templateModel["stylePath"]);
    }

    private class TestConversionEvents : IConversionEvents
    {
        public string? OutputFileName => "output.pdf";
        public event EventHandler<MarkdownEventArgs>? BeforeHtmlConversion;
        public event Func<object, TemplateModelEventArgs, Task>? OnTemplateModelCreatingAsync;
        public event EventHandler<PdfEventArgs>? OnTempPdfCreatedEvent;
    }
}