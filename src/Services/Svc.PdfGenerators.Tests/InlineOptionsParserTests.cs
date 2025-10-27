using DKNet.Svc.PdfGenerators.Options;
using DKNet.Svc.PdfGenerators.Services;

namespace Svc.PdfGenerators.Tests;

public class InlineOptionsParserTests
{
    #region Methods

    [Fact]
    public async Task ParseYamlFrontMatter_WithComplexYamlStructure_ParsesCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = @"---
document-title: Complex Document
is-landscape: true
scale: 1.5
margin-options:
  top: 20px
  bottom: 20px
  left: 15px
  right: 15px
table-of-contents:
  min-depth-level: 1
  max-depth-level: 3
---

# Test Content";

        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var options = await InlineOptionsParser.ParseYamlFrontMatter(tempFile);

            // Assert
            Assert.NotNull(options);
            Assert.Equal("Complex Document", options.DocumentTitle);
            Assert.True(options.IsLandscape);
            Assert.Equal(1.5m, options.Scale);
            Assert.NotNull(options.MarginOptions);
            Assert.Equal("20px", options.MarginOptions.Top);
            Assert.Equal("20px", options.MarginOptions.Bottom);
            Assert.Equal("15px", options.MarginOptions.Left);
            Assert.Equal("15px", options.MarginOptions.Right);
            Assert.NotNull(options.TableOfContents);
            Assert.Equal(1, options.TableOfContents.MinDepthLevel);
            Assert.Equal(3, options.TableOfContents.MaxDepthLevel);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseYamlFrontMatter_WithEmptyYamlBlock_ReturnsDefaultOptions()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = @"---
---

# Test Markdown Content";

        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var options = await InlineOptionsParser.ParseYamlFrontMatter(tempFile);

            // Assert
            Assert.NotNull(options);
            // Should have default values
            Assert.Equal(Theme.Github, options.Theme);
            Assert.Equal(CodeHighlightTheme.Github, options.CodeHighlightTheme);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseYamlFrontMatter_WithHtmlCommentStyle_ReturnsOptions()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var yamlContent = @"<!--
document-title: HTML Comment Style
keep-html: true
-->

# Test Markdown Content
This is test content.";

        await File.WriteAllTextAsync(tempFile, yamlContent);

        try
        {
            // Act
            var options = await InlineOptionsParser.ParseYamlFrontMatter(tempFile);

            // Assert
            Assert.NotNull(options);
            Assert.Equal("HTML Comment Style", options.DocumentTitle);
            Assert.True(options.KeepHtml);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseYamlFrontMatter_WithIncompleteYamlBlock_ThrowsInvalidDataException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = @"---
document-title: Incomplete Block
# Missing closing ---

# Test Markdown Content";

        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
                InlineOptionsParser.ParseYamlFrontMatter(tempFile));

            Assert.Contains("Could not find a YAML front matter block", exception.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseYamlFrontMatter_WithInvalidYamlSyntax_ThrowsException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = @"---
document-title: Test
invalid-yaml: [unclosed array
---

# Test Content";

        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(() =>
                InlineOptionsParser.ParseYamlFrontMatter(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseYamlFrontMatter_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentFile = "/path/to/nonexistent/file.md";

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            InlineOptionsParser.ParseYamlFrontMatter(nonExistentFile));
    }

    [Fact]
    public async Task ParseYamlFrontMatter_WithNoYamlBlock_ThrowsInvalidDataException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = @"# Test Markdown Content
This is test content without YAML front matter.";

        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
                InlineOptionsParser.ParseYamlFrontMatter(tempFile));

            Assert.Contains("Could not find a YAML front matter block", exception.Message);
            Assert.Contains(tempFile, exception.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseYamlFrontMatter_WithValidYamlBlock_ReturnsOptions()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var yamlContent = @"---
document-title: Test Document
metadata-title: Test Meta Title
header-html: <div>Header</div>
footer-html: <div>Footer</div>
---

# Test Markdown Content
This is test content.";

        await File.WriteAllTextAsync(tempFile, yamlContent);

        try
        {
            // Act
            var options = await InlineOptionsParser.ParseYamlFrontMatter(tempFile);

            // Assert
            Assert.NotNull(options);
            Assert.Equal("Test Document", options.DocumentTitle);
            Assert.Equal("Test Meta Title", options.MetadataTitle);
            Assert.Equal("<div>Header</div>", options.HeaderHtml);
            Assert.Equal("<div>Footer</div>", options.FooterHtml);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseYamlFrontMatter_WithWhitespaceInYaml_ParsesCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = @"---

document-title: Document With Whitespace
keep-html: false

---

# Test Content";

        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var options = await InlineOptionsParser.ParseYamlFrontMatter(tempFile);

            // Assert
            Assert.NotNull(options);
            Assert.Equal("Document With Whitespace", options.DocumentTitle);
            Assert.False(options.KeepHtml);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion
}