using SlimBus.AppServices.Share;

namespace SlimBus.App.Tests.Unit;

public class SimpleUnitTests
{
    [Fact]
    public void PageableQuery_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var query = new PageableQuery();

        // Assert
        query.PageIndex.ShouldBe(0);
        query.PageSize.ShouldBe(100);
    }

    [Fact]
    public void PageableQuery_SetProperties_ShouldWork()
    {
        // Act
        var query = new PageableQuery
        {
            PageIndex = 5,
            PageSize = 50
        };

        // Assert
        query.PageIndex.ShouldBe(5);
        query.PageSize.ShouldBe(50);
    }
}