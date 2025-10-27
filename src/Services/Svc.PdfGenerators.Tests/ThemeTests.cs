using DKNet.Svc.PdfGenerators.Options;
using Shouldly;

namespace Svc.PdfGenerators.Tests;

public class ThemeTests
{
    [Fact]
    public void Github_ReturnsTheme()
    {
        // Act
        var theme = Theme.Github;

        // Assert
        theme.ShouldNotBeNull();
    }

    [Fact]
    public void Latex_ReturnsTheme()
    {
        // Act
        var theme = Theme.Latex;

        // Assert
        theme.ShouldNotBeNull();
    }

    [Fact]
    public void None_ReturnsTheme()
    {
        // Act
        var theme = Theme.None;

        // Assert
        theme.ShouldNotBeNull();
    }

    [Fact]
    public void Custom_WithCssPath_ReturnsCustomTheme()
    {
        // Arrange
        var cssPath = "/path/to/custom.css";

        // Act
        var theme = Theme.Custom(cssPath);

        // Assert
        theme.ShouldNotBeNull();
    }

    [Fact]
    public void Custom_WithEmptyPath_ReturnsCustomTheme()
    {
        // Arrange
        var cssPath = string.Empty;

        // Act
        var theme = Theme.Custom(cssPath);

        // Assert
        theme.ShouldNotBeNull();
    }

    [Fact]
    public void Github_IsPredefinedTheme()
    {
        // Act
        var theme = Theme.Github;

        // Assert
        theme.ShouldBeOfType<PredefinedTheme>();
    }

    [Fact]
    public void Latex_IsPredefinedTheme()
    {
        // Act
        var theme = Theme.Latex;

        // Assert
        theme.ShouldBeOfType<PredefinedTheme>();
    }

    [Fact]
    public void None_IsPredefinedTheme()
    {
        // Act
        var theme = Theme.None;

        // Assert
        theme.ShouldBeOfType<PredefinedTheme>();
    }

    [Fact]
    public void Custom_IsCustomTheme()
    {
        // Arrange
        var cssPath = "/path/to/custom.css";

        // Act
        var theme = Theme.Custom(cssPath);

        // Assert
        theme.ShouldBeOfType<CustomTheme>();
    }
}
