using DKNet.Svc.PdfGenerators.Options;
using PuppeteerSharp.Media;
using MarginOptions = DKNet.Svc.PdfGenerators.Options.MarginOptions;

namespace Svc.PdfGenerators.Tests;

public class PdfGeneratorOptionsTests
{
    #region Methods

    [Fact]
    public void ChromePath_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions();
        var chromePath = "/usr/bin/chromium";

        // Act
        options.ChromePath = chromePath;

        // Assert
        Assert.Equal(chromePath, options.ChromePath);
    }

    [Fact]
    public void CodeHighlightTheme_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions
        {
            // Act
            CodeHighlightTheme = CodeHighlightTheme.Monokai
        };

        // Assert
        Assert.Equal(CodeHighlightTheme.Monokai, options.CodeHighlightTheme);
    }

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Act
        var options = new PdfGeneratorOptions();

        // Assert
        Assert.Equal(ModuleOptions.Remote, options.ModuleOptions);
        Assert.Equal(Theme.Github, options.Theme);
        Assert.Equal(CodeHighlightTheme.Github, options.CodeHighlightTheme);
        Assert.False(options.EnableAutoLanguageDetection);
        Assert.Null(options.HeaderHtml);
        Assert.Null(options.FooterHtml);
        Assert.Null(options.DocumentTitle);
        Assert.Null(options.MetadataTitle);
        Assert.Null(options.CustomHeadContent);
        Assert.Null(options.ChromePath);
        Assert.False(options.KeepHtml);
        Assert.Null(options.MarginOptions);
        Assert.False(options.IsLandscape);
        Assert.Equal(PaperFormat.A4, options.Format);
        Assert.Equal(1m, options.Scale);
        Assert.Null(options.TableOfContents);
    }

    [Fact]
    public void CustomHeadContent_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions();
        var customHead = "<style>body { font-size: 12px; }</style>";

        // Act
        options.CustomHeadContent = customHead;

        // Assert
        Assert.Equal(customHead, options.CustomHeadContent);
    }

    [Fact]
    public void DocumentTitle_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions();
        var title = "Test Document Title";

        // Act
        options.DocumentTitle = title;

        // Assert
        Assert.Equal(title, options.DocumentTitle);
    }

    [Fact]
    public void EnableAutoLanguageDetection_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions
        {
            // Act
            EnableAutoLanguageDetection = true
        };

        // Assert
        Assert.True(options.EnableAutoLanguageDetection);
    }

    [Fact]
    public void FooterHtml_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions();
        var footerHtml = "<div class='footer'>Test Footer</div>";

        // Act
        options.FooterHtml = footerHtml;

        // Assert
        Assert.Equal(footerHtml, options.FooterHtml);
    }

    [Fact]
    public void Format_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions
        {
            // Act
            Format = PaperFormat.Letter
        };

        // Assert
        Assert.Equal(PaperFormat.Letter, options.Format);
    }

    [Fact]
    public void HeaderHtml_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions();
        var headerHtml = "<div class='header'>Test Header</div>";

        // Act
        options.HeaderHtml = headerHtml;

        // Assert
        Assert.Equal(headerHtml, options.HeaderHtml);
    }

    [Fact]
    public void IsLandscape_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions
        {
            // Act
            IsLandscape = true
        };

        // Assert
        Assert.True(options.IsLandscape);
    }

    [Fact]
    public void KeepHtml_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions
        {
            // Act
            KeepHtml = true
        };

        // Assert
        Assert.True(options.KeepHtml);
    }

    [Fact]
    public void MarginOptions_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions();
        var marginOptions = new MarginOptions
        {
            Top = "10px",
            Bottom = "10px",
            Left = "15px",
            Right = "15px"
        };

        // Act
        options.MarginOptions = marginOptions;

        // Assert
        Assert.NotNull(options.MarginOptions);
        Assert.Equal("10px", options.MarginOptions.Top);
        Assert.Equal("10px", options.MarginOptions.Bottom);
        Assert.Equal("15px", options.MarginOptions.Left);
        Assert.Equal("15px", options.MarginOptions.Right);
    }

    [Fact]
    public void MetadataTitle_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions();
        var title = "Test Metadata Title";

        // Act
        options.MetadataTitle = title;

        // Assert
        Assert.Equal(title, options.MetadataTitle);
    }

    [Fact]
    public void ModuleOptions_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions
        {
            // Act
            ModuleOptions = ModuleOptions.FromLocalPath("/custom/path")
        };

        // Assert
        Assert.NotNull(options.ModuleOptions);
        // Additional assertions would depend on ModuleOptions implementation
    }

    [Fact]
    public void Scale_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions
        {
            // Act
            Scale = 1.5m
        };

        // Assert
        Assert.Equal(1.5m, options.Scale);
    }

    [Fact]
    public void TableOfContents_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions();
        var tocOptions = new TableOfContentsOptions
        {
            MinDepthLevel = 2,
            MaxDepthLevel = 4,
            ListStyle = ListStyle.Decimals
        };

        // Act
        options.TableOfContents = tocOptions;

        // Assert
        Assert.NotNull(options.TableOfContents);
        Assert.Equal(2, options.TableOfContents.MinDepthLevel);
        Assert.Equal(4, options.TableOfContents.MaxDepthLevel);
        Assert.Equal(ListStyle.Decimals, options.TableOfContents.ListStyle);
    }

    [Fact]
    public void Theme_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new PdfGeneratorOptions
        {
            // Act
            Theme = Theme.Github
        };

        // Assert
        Assert.Equal(Theme.Github, options.Theme);
    }

    #endregion
}