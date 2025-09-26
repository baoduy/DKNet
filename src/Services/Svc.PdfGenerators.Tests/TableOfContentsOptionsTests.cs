using DKNet.Svc.PdfGenerators.Options;

namespace Svc.PdfGenerators.Tests;

public class TableOfContentsOptionsTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Act
        var options = new TableOfContentsOptions();

        // Assert
        Assert.Equal(ListStyle.OrderedDefault, options.ListStyle);
        Assert.False(options.HasColoredLinks);
        Assert.Equal(1, options.MinDepthLevel);
        Assert.Equal(6, options.MaxDepthLevel);
    }

    [Fact]
    public void MinDepthLevel_ValidValue_SetsCorrectly()
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act
        options.MinDepthLevel = 3;

        // Assert
        Assert.Equal(3, options.MinDepthLevel);
    }

    [Fact]
    public void MaxDepthLevel_ValidValue_SetsCorrectly()
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act
        options.MaxDepthLevel = 4;

        // Assert
        Assert.Equal(4, options.MaxDepthLevel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(10)]
    public void MinDepthLevel_InvalidValue_ThrowsArgumentOutOfRangeException(int invalidValue)
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MinDepthLevel = invalidValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(10)]
    public void MaxDepthLevel_InvalidValue_ThrowsArgumentOutOfRangeException(int invalidValue)
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxDepthLevel = invalidValue);
    }

    [Fact]
    public void MinDepthLevel_GreaterThanMaxDepthLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new TableOfContentsOptions();
        options.MaxDepthLevel = 3;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MinDepthLevel = 4);
    }

    [Fact]
    public void MaxDepthLevel_LessThanMinDepthLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new TableOfContentsOptions();
        options.MinDepthLevel = 3;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxDepthLevel = 2);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 6)]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    [InlineData(6, 6)]
    public void MinAndMaxDepthLevel_ValidCombinations_SetsCorrectly(int min, int max)
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act
        options.MinDepthLevel = min;
        options.MaxDepthLevel = max;

        // Assert
        Assert.Equal(min, options.MinDepthLevel);
        Assert.Equal(max, options.MaxDepthLevel);
    }

    [Fact]
    public void SetMaxDepthLevelFirst_ThenMinDepthLevel_WorksCorrectly()
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act
        options.MaxDepthLevel = 4;
        options.MinDepthLevel = 2;

        // Assert
        Assert.Equal(2, options.MinDepthLevel);
        Assert.Equal(4, options.MaxDepthLevel);
    }

    [Fact]
    public void ListStyle_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act
        options.ListStyle = ListStyle.Decimals;

        // Assert
        Assert.Equal(ListStyle.Decimals, options.ListStyle);
    }

    [Fact]
    public void HasColoredLinks_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act
        options.HasColoredLinks = true;

        // Assert
        Assert.True(options.HasColoredLinks);
    }

    [Fact]
    public void MinDepthLevel_DefaultValue_ReturnsOne()
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act & Assert
        Assert.Equal(1, options.MinDepthLevel);
    }

    [Fact]
    public void MaxDepthLevel_DefaultValue_ReturnsSix()
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act & Assert
        Assert.Equal(6, options.MaxDepthLevel);
    }

    [Fact]
    public void DepthLevels_SettingEqualValues_WorksCorrectly()
    {
        // Arrange
        var options = new TableOfContentsOptions();

        // Act
        options.MinDepthLevel = 3;
        options.MaxDepthLevel = 3;

        // Assert
        Assert.Equal(3, options.MinDepthLevel);
        Assert.Equal(3, options.MaxDepthLevel);
    }

    [Fact]
    public void MinDepthLevel_SetToSameAsMaxDepthLevel_DoesNotThrow()
    {
        // Arrange
        var options = new TableOfContentsOptions();
        options.MaxDepthLevel = 4;

        // Act
        options.MinDepthLevel = 4;

        // Assert
        Assert.Equal(4, options.MinDepthLevel);
        Assert.Equal(4, options.MaxDepthLevel);
    }

    [Fact]
    public void MaxDepthLevel_SetToSameAsMinDepthLevel_DoesNotThrow()
    {
        // Arrange
        var options = new TableOfContentsOptions();
        options.MinDepthLevel = 4;

        // Act
        options.MaxDepthLevel = 4;

        // Assert
        Assert.Equal(4, options.MinDepthLevel);
        Assert.Equal(4, options.MaxDepthLevel);
    }
}