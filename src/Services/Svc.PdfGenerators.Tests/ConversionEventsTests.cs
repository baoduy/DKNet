using DKNet.Svc.PdfGenerators;

namespace Svc.PdfGenerators.Tests;

public class ConversionEventsTests
{
    #region Methods

    [Fact]
    public void EventArgs_InheritFromEventArgs()
    {
        // Arrange & Act
        var markdownArgs = new MarkdownEventArgs("test");
        var templateArgs = new TemplateModelEventArgs(new Dictionary<string, string>());
        var pdfArgs = new PdfEventArgs("test.pdf");

        // Assert
        Assert.IsAssignableFrom<EventArgs>(markdownArgs);
        Assert.IsAssignableFrom<EventArgs>(templateArgs);
        Assert.IsAssignableFrom<EventArgs>(pdfArgs);
    }

    [Fact]
    public void MarkdownEventArgs_Constructor_SetsMarkdownContent()
    {
        // Arrange
        var markdownContent = "# Test Markdown\n\nThis is test content.";

        // Act
        var args = new MarkdownEventArgs(markdownContent);

        // Assert
        Assert.Equal(markdownContent, args.MarkdownContent);
    }

    [Fact]
    public void MarkdownEventArgs_MarkdownContent_CanBeModified()
    {
        // Arrange
        var originalContent = "# Original Content";
        var modifiedContent = "# Modified Content";
        var args = new MarkdownEventArgs(originalContent)
        {
            // Act
            MarkdownContent = modifiedContent
        };

        // Assert
        Assert.Equal(modifiedContent, args.MarkdownContent);
    }

    [Fact]
    public void MarkdownEventArgs_WithEmptyContent_HandlesCorrectly()
    {
        // Arrange
        var emptyContent = "";

        // Act
        var args = new MarkdownEventArgs(emptyContent);

        // Assert
        Assert.Equal(emptyContent, args.MarkdownContent);
    }

    [Fact]
    public void MarkdownEventArgs_WithLargeContent_HandlesCorrectly()
    {
        // Arrange
        var largeContent = new string('a', 10000) + "\n# Large Content\n" + new string('b', 10000);

        // Act
        var args = new MarkdownEventArgs(largeContent);

        // Assert
        Assert.Equal(largeContent, args.MarkdownContent);
        Assert.True(args.MarkdownContent.Length > 20000);
    }

    [Fact]
    public void MarkdownEventArgs_WithNullContent_HandlesCorrectly()
    {
        // Arrange
        string? nullContent = null;

        // Act
        var args = new MarkdownEventArgs(nullContent!);

        // Assert
        Assert.Null(args.MarkdownContent);
    }

    [Fact]
    public void PdfEventArgs_Constructor_SetsPdfPath()
    {
        // Arrange
        var pdfPath = "/path/to/generated/file.pdf";

        // Act
        var args = new PdfEventArgs(pdfPath);

        // Assert
        Assert.Equal(pdfPath, args.PdfPath);
    }

    [Fact]
    public void PdfEventArgs_PdfPath_IsReadOnly()
    {
        // Arrange
        var pdfPath = "/path/to/file.pdf";
        var args = new PdfEventArgs(pdfPath);

        // Act & Assert
        // Verify that PdfPath doesn't have a setter by checking if it's get-only
        var property = typeof(PdfEventArgs).GetProperty(nameof(PdfEventArgs.PdfPath));
        Assert.NotNull(property);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
    }

    [Fact]
    public void PdfEventArgs_WithEmptyPath_HandlesCorrectly()
    {
        // Arrange
        var emptyPath = "";

        // Act
        var args = new PdfEventArgs(emptyPath);

        // Assert
        Assert.Equal(emptyPath, args.PdfPath);
    }

    [Fact]
    public void PdfEventArgs_WithNullPath_HandlesCorrectly()
    {
        // Arrange
        string? nullPath = null;

        // Act
        var args = new PdfEventArgs(nullPath!);

        // Assert
        Assert.Null(args.PdfPath);
    }

    [Fact]
    public void TemplateModelEventArgs_Constructor_SetsTemplateModel()
    {
        // Arrange
        var templateModel = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        // Act
        var args = new TemplateModelEventArgs(templateModel);

        // Assert
        Assert.Same(templateModel, args.TemplateModel);
        Assert.Equal(2, args.TemplateModel.Count);
        Assert.Equal("value1", args.TemplateModel["key1"]);
        Assert.Equal("value2", args.TemplateModel["key2"]);
    }

    [Fact]
    public void TemplateModelEventArgs_TemplateModel_CanBeModified()
    {
        // Arrange
        var templateModel = new Dictionary<string, string>
        {
            { "original", "value" }
        };
        var args = new TemplateModelEventArgs(templateModel)
        {
            TemplateModel =
            {
                // Act
                ["new"] = "newValue",
                ["original"] = "modifiedValue"
            }
        };

        // Assert
        Assert.Equal(2, args.TemplateModel.Count);
        Assert.Equal("modifiedValue", args.TemplateModel["original"]);
        Assert.Equal("newValue", args.TemplateModel["new"]);
    }

    [Fact]
    public void TemplateModelEventArgs_TemplateModel_IsReadOnly()
    {
        // Arrange
        var templateModel = new Dictionary<string, string>();
        var args = new TemplateModelEventArgs(templateModel);

        // Act & Assert
        // Verify that TemplateModel property doesn't have a setter
        var property = typeof(TemplateModelEventArgs).GetProperty(nameof(TemplateModelEventArgs.TemplateModel));
        Assert.NotNull(property);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
    }

    [Fact]
    public void TemplateModelEventArgs_WithEmptyDictionary_HandlesCorrectly()
    {
        // Arrange
        var emptyModel = new Dictionary<string, string>();

        // Act
        var args = new TemplateModelEventArgs(emptyModel);

        // Assert
        Assert.Same(emptyModel, args.TemplateModel);
        Assert.Empty(args.TemplateModel);
    }

    [Fact]
    public void TemplateModelEventArgs_WithSpecialCharacterKeys_HandlesCorrectly()
    {
        // Arrange
        var templateModel = new Dictionary<string, string>
        {
            { "key-with-dash", "value1" },
            { "key_with_underscore", "value2" },
            { "key.with.dots", "value3" },
            { "key with spaces", "value4" }
        };

        // Act
        var args = new TemplateModelEventArgs(templateModel);

        // Assert
        Assert.Equal(4, args.TemplateModel.Count);
        Assert.Equal("value1", args.TemplateModel["key-with-dash"]);
        Assert.Equal("value2", args.TemplateModel["key_with_underscore"]);
        Assert.Equal("value3", args.TemplateModel["key.with.dots"]);
        Assert.Equal("value4", args.TemplateModel["key with spaces"]);
    }

    #endregion
}