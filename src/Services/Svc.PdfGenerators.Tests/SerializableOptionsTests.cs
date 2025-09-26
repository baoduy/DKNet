using DKNet.Svc.PdfGenerators.Options;
using PuppeteerSharp.Media;

namespace Svc.PdfGenerators.Tests;

public class SerializableOptionsTests
{
    [Fact]
    public void ToPdfGeneratorOptions_WithNullValues_ReturnsDefaultValues()
    {
        // Arrange
        var serializableOptions = new SerializableOptions();

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

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
    public void ToPdfGeneratorOptions_WithValidStringValues_SetsPropertiesCorrectly()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            DocumentTitle = "Test Document",
            MetadataTitle = "Test Metadata",
            HeaderHtml = "<div>Header</div>",
            FooterHtml = "<div>Footer</div>",
            CustomHeadContent = "<style>body{color:red;}</style>",
            ChromePath = "/usr/bin/chrome"
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.Equal("Test Document", options.DocumentTitle);
        Assert.Equal("Test Metadata", options.MetadataTitle);
        Assert.Equal("<div>Header</div>", options.HeaderHtml);
        Assert.Equal("<div>Footer</div>", options.FooterHtml);
        Assert.Equal("<style>body{color:red;}</style>", options.CustomHeadContent);
        Assert.Equal("/usr/bin/chrome", options.ChromePath);
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithBooleanValues_SetsPropertiesCorrectly()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            EnableAutoLanguageDetection = true,
            KeepHtml = true,
            IsLandscape = true
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.True(options.EnableAutoLanguageDetection);
        Assert.True(options.KeepHtml);
        Assert.True(options.IsLandscape);
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithValidScale_SetsScaleCorrectly()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            Scale = 1.5m
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.Equal(1.5m, options.Scale);
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithValidFormat_SetsFormatCorrectly()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            Format = "Letter"
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.Equal(PaperFormat.Letter, options.Format);
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithInvalidFormat_KeepsDefaultFormat()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            Format = "InvalidFormat"
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.Equal(PaperFormat.A4, options.Format); // Should keep default
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithValidTheme_SetsThemeCorrectly()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            Theme = "GitlabDark"
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.Equal(Theme.GitlabDark, options.Theme);
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithCustomThemePath_CreatesCustomTheme()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            Theme = "/path/to/custom/theme.css"
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.NotNull(options.Theme);
        // Additional assertions would depend on Theme implementation details
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithValidCodeHighlightTheme_SetsThemeCorrectly()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            CodeHighlightTheme = "Monokai"
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.Equal(CodeHighlightTheme.Monokai, options.CodeHighlightTheme);
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithInvalidCodeHighlightTheme_KeepsDefault()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            CodeHighlightTheme = "InvalidTheme"
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.Equal(CodeHighlightTheme.Github, options.CodeHighlightTheme); // Should keep default
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithMarginOptions_SetsMarginOptionsCorrectly()
    {
        // Arrange
        var marginOptions = new MarginOptions
        {
            Top = "10px",
            Bottom = "15px",
            Left = "20px",
            Right = "25px"
        };
        var serializableOptions = new SerializableOptions
        {
            MarginOptions = marginOptions
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.NotNull(options.MarginOptions);
        Assert.Equal("10px", options.MarginOptions.Top);
        Assert.Equal("15px", options.MarginOptions.Bottom);
        Assert.Equal("20px", options.MarginOptions.Left);
        Assert.Equal("25px", options.MarginOptions.Right);
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithTableOfContents_SetsTableOfContentsCorrectly()
    {
        // Arrange
        var tocOptions = new TableOfContentsOptions
        {
            MinDepthLevel = 2,
            MaxDepthLevel = 4,
            ListStyle = ListStyle.Decimals,
            HasColoredLinks = true
        };
        var serializableOptions = new SerializableOptions
        {
            TableOfContents = tocOptions
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.NotNull(options.TableOfContents);
        Assert.Equal(2, options.TableOfContents.MinDepthLevel);
        Assert.Equal(4, options.TableOfContents.MaxDepthLevel);
        Assert.Equal(ListStyle.Decimals, options.TableOfContents.ListStyle);
        Assert.True(options.TableOfContents.HasColoredLinks);
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithValidModuleOptions_SetsModuleOptionsCorrectly()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            ModuleOptions = "Remote"
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.Equal(ModuleOptions.Remote, options.ModuleOptions);
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithCustomModulePath_CreatesCustomModuleOptions()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            ModuleOptions = "/custom/module/path"
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.NotNull(options.ModuleOptions);
        // Additional assertions would depend on ModuleOptions implementation
    }

    [Fact]
    public void ToPdfGeneratorOptions_WithMixedValidAndNullValues_HandlesCorrectly()
    {
        // Arrange
        var serializableOptions = new SerializableOptions
        {
            DocumentTitle = "Test Document",
            MetadataTitle = null, // This should remain null in result
            EnableAutoLanguageDetection = true,
            KeepHtml = null, // This should use default false
            Scale = 2.0m,
            Format = null // This should use default A4
        };

        // Act
        var options = serializableOptions.ToPdfGeneratorOptions();

        // Assert
        Assert.Equal("Test Document", options.DocumentTitle);
        Assert.Null(options.MetadataTitle);
        Assert.True(options.EnableAutoLanguageDetection);
        Assert.False(options.KeepHtml); // Default value
        Assert.Equal(2.0m, options.Scale);
        Assert.Equal(PaperFormat.A4, options.Format); // Default value
    }
}