using SlimBus.AppServices.Share;

namespace SlimBus.App.Tests.Unit;

public class SimpleUnitTests
{
    #region Methods

    [Fact]
    public void PageableQueryDefaultValuesShouldBeCorrect()
    {
        // Act
        var query = new PageableQuery();

        // Assert
        query.PageIndex.ShouldBe(0);
        query.PageSize.ShouldBe(100);
    }

    [Fact]
    public void PageableQuerySetPropertiesShouldWork()
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

    #endregion
}