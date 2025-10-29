using DKNet.Svc.PdfGenerators.Options;
using Shouldly;

namespace Svc.PdfGenerators.Tests;

public class CodeHighlightThemeTests
{
    #region Methods

    [Fact]
    public void A11yDark_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.A11YDark;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("a11y-dark.css");
    }

    [Fact]
    public void A11yLight_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.A11YLight;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("a11y-light.css");
    }

    [Fact]
    public void Agate_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.Agate;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("agate.css");
    }

    [Fact]
    public void AndroidStudio_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.AndroidStudio;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("androidstudio.css");
    }

    [Fact]
    public void AnOldHope_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.AnOldHope;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("an-old-hope.css");
    }

    [Fact]
    public void AtomOneDark_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.AtomOneDark;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("atom-one-dark.css");
    }

    [Fact]
    public void Default_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.Default;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("default.css");
    }

    [Fact]
    public void Github_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.Github;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("github.css");
    }

    [Fact]
    public void GithubDark_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.GithubDark;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("github-dark.css");
    }

    [Fact]
    public void Monokai_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.Monokai;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("monokai.css");
    }

    [Fact]
    public void None_ReturnsEmptyTheme()
    {
        // Act
        var theme = CodeHighlightTheme.None;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void Nord_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.Nord;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("nord.css");
    }

    [Fact]
    public void Obsidian_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.Obsidian;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("obsidian.css");
    }

    [Fact]
    public void TokyoNightDark_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.TokyoNightDark;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("tokyo-night-dark.css");
    }

    [Fact]
    public void ToString_ReturnsSheetName()
    {
        // Arrange
        var theme = CodeHighlightTheme.Github;

        // Act
        var result = theme.ToString();

        // Assert
        result.ShouldBe("github.css");
    }

    [Fact]
    public void Vs_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.Vs;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("vs.css");
    }

    [Fact]
    public void Vs2015_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.Vs2015;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("vs2015.css");
    }

    [Fact]
    public void Xcode_ReturnsTheme()
    {
        // Act
        var theme = CodeHighlightTheme.Xcode;

        // Assert
        theme.ShouldNotBeNull();
        theme.ToString().ShouldBe("xcode.css");
    }

    #endregion
}