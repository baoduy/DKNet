using System.Reflection;
using SlimBus.AppServices.Share;

namespace SlimBus.App.Tests.Unit;

public class SharedComponentTests
{
    #region Methods

    [Fact]
    public void BaseCommandByUserShouldBeSettable()
    {
        // Arrange
        var command = new TestCommand();
        var expectedUserId = "new-user-456";

        // Act
        var byUserProp = typeof(TestCommand).GetProperty(nameof(TestCommand.ByUser),
            BindingFlags.Public | BindingFlags.Instance)!;

        byUserProp.SetValue(command, expectedUserId);
        var result = byUserProp.GetValue(command);

        // Assert
        result.ShouldBe(expectedUserId);
    }

    [Fact]
    public void PageableQueryDefaultValuesShouldBeCorrect()
    {
        // Act
        var query = new PageableQuery();

        // Assert
        query.PageIndex.ShouldBe(0);
        query.PageSize.ShouldBe(100);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 100)]
    [InlineData(10, 200)]
    public void PageableQuerySetPropertiesShouldWork(int pageIndex, int pageSize)
    {
        // Act
        var query = new PageableQuery
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        // Assert
        query.PageIndex.ShouldBe(pageIndex);
        query.PageSize.ShouldBe(pageSize);
    }

    #endregion

    private record TestCommand : BaseCommand
    {
        // Test implementation of BaseCommand
    }
}