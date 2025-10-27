using System.Reflection;
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using DKNet.Svc.PdfGenerators.Services;

namespace Svc.PdfGenerators.Tests;

public class TableOfContentsCreatorTests
{
    #region Methods

    [Fact]
    public async Task InternalAddStylesToTemplateAsync_AddsStyles()
    {
        var options = new TableOfContentsOptions
            { ListStyle = ListStyle.Decimals, MinDepthLevel = 1, MaxDepthLevel = 6 };
        var events = new TestConversionEvents();
        var resourceService = new EmbeddedResourceService();
        var tocCreator = new TableOfContentsCreator(options, events, resourceService);
        var templateModel = new Dictionary<string, string>();
        var args = new TemplateModelEventArgs(templateModel);
        // Directly call the handler instead of invoking the event
        await tocCreator.InternalAddStylesToTemplateAsync(tocCreator, args);
        Assert.Contains("tocStyle", templateModel.Keys);
    }

    [Fact]
    public void InternalToHtml_GeneratesTocHtml()
    {
        var options = new TableOfContentsOptions
            { MinDepthLevel = 1, MaxDepthLevel = 6, ListStyle = ListStyle.OrderedDefault };
        var events = new TestConversionEvents();
        var resourceService = new EmbeddedResourceService();
        var tocCreator = new TableOfContentsCreator(options, events, resourceService);
        var markdown = "# Title\n## Subtitle\nContent";
        var tocHtml = typeof(TableOfContentsCreator)
            .GetMethod("InternalToHtml",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(tocCreator, [markdown]) as string;
        Assert.Contains("table-of-contents", tocHtml);
        Assert.Contains("Title", tocHtml);
        Assert.Contains("Subtitle", tocHtml);
    }

    #endregion

    private class TestConversionEvents : IConversionEvents
    {
        #region Properties

        public string? OutputFileName => "output.pdf";

        #endregion

        public event EventHandler<MarkdownEventArgs>? HtmlConverting;
        public event AsyncConversionEventHandler<TemplateModelEventArgs>? TemplateModelCreating;
        public event EventHandler<PdfEventArgs>? TempPdfCreated;
    }
}