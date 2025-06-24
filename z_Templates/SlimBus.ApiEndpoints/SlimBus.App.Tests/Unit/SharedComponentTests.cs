using SlimBus.AppServices.Share;

namespace SlimBus.App.Tests.Unit;

public class SharedComponentTests
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

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 100)]
    [InlineData(10, 200)]
    public void PageableQuery_SetProperties_ShouldWork(int pageIndex, int pageSize)
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

    [Fact]
    public void BaseCommand_UserId_ShouldReturnValue()
    {
        // Arrange
        var command = new TestCommand();
        var userId = "test-user-123";

        // Act
        var testUserId = typeof(TestCommand).GetProperty("UserId", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        testUserId?.SetValue(command, userId);
        var result = testUserId?.GetValue(command);

        // Assert
        result.ShouldBe(userId);
    }

    [Fact]
    public void BaseCommand_UserId_ShouldBeSettable()
    {
        // Arrange
        var command = new TestCommand();
        var expectedUserId = "new-user-456";

        // Act
        var testUserId = typeof(TestCommand).GetProperty("UserId", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        testUserId?.SetValue(command, expectedUserId);
        var result = testUserId?.GetValue(command);

        // Assert
        result.ShouldBe(expectedUserId);
    }

    private record TestCommand : BaseCommand
    {
        // Test implementation of BaseCommand
    }
}