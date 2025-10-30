using DKNet.Svc.PdfGenerators.Services;

namespace Svc.PdfGenerators.Tests;

public class TemplateFillerTests
{
    #region Methods

    [Fact]
    public void FillTemplate_WithCaseInsensitiveKeys_HandlesCorrectly()
    {
        // Arrange
        var template = "Hello @(name) and @(location)!";
        var model = new Dictionary<string, string>
        {
            { "name", "John" },
            { "location", "World" }
        };

        // Act
        var result = TemplateFiller.FillTemplate(template, model);

        // Assert
        Assert.Equal("Hello John and World!", result);
    }

    [Fact]
    public void FillTemplate_WithDuplicateTokens_ReplacesAllOccurrences()
    {
        // Arrange
        var template = "@(greeting) @(name), @(greeting) again @(name)!";
        var model = new Dictionary<string, string>
        {
            { "greeting", "Hello" },
            { "name", "John" }
        };

        // Act
        var result = TemplateFiller.FillTemplate(template, model);

        // Assert
        Assert.Equal("Hello John, Hello again John!", result);
    }

    [Fact]
    public void FillTemplate_WithEmptyModel_ReplacesTokensWithEmptyString()
    {
        // Arrange
        var template = "Hello @(name), welcome to @(location)!";
        var model = new Dictionary<string, string>();

        // Act
        var result = TemplateFiller.FillTemplate(template, model);

        // Assert
        Assert.Equal("Hello , welcome to !", result);
    }

    [Fact]
    public void FillTemplate_WithEmptyTemplate_ReturnsEmptyString()
    {
        // Arrange
        var template = "";
        var model = new Dictionary<string, string>
        {
            { "name", "John Doe" }
        };

        // Act
        var result = TemplateFiller.FillTemplate(template, model);

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void FillTemplate_WithMalformedTokens_IgnoresMalformedTokens()
    {
        // Arrange
        var template = "Hello @(name) and @invalidtoken and @(location)";
        var model = new Dictionary<string, string>
        {
            { "name", "John" },
            { "location", "World" }
        };

        // Act
        var result = TemplateFiller.FillTemplate(template, model);

        // Assert
        Assert.Equal("Hello John and @invalidtoken and World", result);
    }

    [Fact]
    public void FillTemplate_WithMissingKeys_ReplacesWithEmptyString()
    {
        // Arrange
        var template = "Hello @(name), welcome to @(location)!";
        var model = new Dictionary<string, string>
        {
            { "name", "John Doe" }

            // location is missing
        };

        // Act
        var result = TemplateFiller.FillTemplate(template, model);

        // Assert
        Assert.Equal("Hello John Doe, welcome to !", result);
    }

    [Fact]
    public void FillTemplate_WithNestedParentheses_HandlesSimpleCase()
    {
        // Arrange
        var template = "Result: @(calculation)";
        var model = new Dictionary<string, string>
        {
            { "calculation", "(2 + 3) * 4 = 20" }
        };

        // Act
        var result = TemplateFiller.FillTemplate(template, model);

        // Assert
        Assert.Equal("Result: (2 + 3) * 4 = 20", result);
    }

    [Fact]
    public void FillTemplate_WithNoTokens_ReturnsOriginalTemplate()
    {
        // Arrange
        var template = "Hello world, no tokens here!";
        var model = new Dictionary<string, string>
        {
            { "name", "John Doe" }
        };

        // Act
        var result = TemplateFiller.FillTemplate(template, model);

        // Assert
        Assert.Equal(template, result);
    }

    [Fact]
    public void FillTemplate_WithSpecialCharactersInValues_HandlesCorrectly()
    {
        // Arrange
        var template = "Content: @(content)";
        var model = new Dictionary<string, string>
        {
            { "content", "<html>&amp; special chars \"quotes\"</html>" }
        };

        // Act
        var result = TemplateFiller.FillTemplate(template, model);

        // Assert
        Assert.Equal("Content: <html>&amp; special chars \"quotes\"</html>", result);
    }

    [Fact]
    public void FillTemplate_WithValidTokens_ReplacesTokensCorrectly()
    {
        // Arrange
        var template = "Hello @(name), welcome to @(location)!";
        var model = new Dictionary<string, string>
        {
            { "name", "John Doe" },
            { "location", "DKNet" }
        };

        // Act
        var result = TemplateFiller.FillTemplate(template, model);

        // Assert
        Assert.Equal("Hello John Doe, welcome to DKNet!", result);
    }

    #endregion
}