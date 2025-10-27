using DKNet.Svc.PdfGenerators.Options;
using Shouldly;

namespace Svc.PdfGenerators.Tests;

public class PageNumberOptionsTests
{
    [Fact]
    public void Constructor_SetsDefaultLeader()
    {
        // Act
        var options = new PageNumberOptions();

        // Assert
        options.TabLeader.ShouldBe(Leader.Dots);
    }

    [Fact]
    public void TabLeader_CanBeSet()
    {
        // Arrange
        var options = new PageNumberOptions();

        // Act
        options.TabLeader = Leader.Dashes;

        // Assert
        options.TabLeader.ShouldBe(Leader.Dashes);
    }

    [Fact]
    public void TabLeader_CanBeSetToNone()
    {
        // Arrange
        var options = new PageNumberOptions();

        // Act
        options.TabLeader = Leader.None;

        // Assert
        options.TabLeader.ShouldBe(Leader.None);
    }
}
