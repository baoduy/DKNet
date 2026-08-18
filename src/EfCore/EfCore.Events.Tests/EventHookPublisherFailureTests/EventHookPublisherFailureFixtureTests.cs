using Microsoft.Extensions.Logging;

namespace EfCore.Events.Tests.EventHookPublisherFailureTests;

/// <summary>
///     Exercises the test-harness plumbing branches that behavioral tests never hit.
/// </summary>
public class EventHookPublisherFailureFixtureTests
{
    #region Methods

    [Fact]
    public void Log_WithLogLevelNone_DoesNotAddEntry()
    {
        // Arrange
        var logger = new TestLoggerProvider();

        // Act
        logger.Log(LogLevel.None, default, "state", null, (s, _) => s.ToString()!);

        // Assert
        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void IsEnabled_WithLogLevelNone_ReturnsFalse()
    {
        // Arrange
        var logger = new TestLoggerProvider();

        // Act & Assert
        logger.IsEnabled(LogLevel.None).ShouldBeFalse();
        logger.IsEnabled(LogLevel.Error).ShouldBeTrue();
    }

    [Fact]
    public void BeginScope_ReturnsNull()
    {
        // Arrange
        var logger = new TestLoggerProvider();

        // Act
        using var scope = logger.BeginScope("scope");

        // Assert
        scope.ShouldBeNull();
    }

    [Fact]
    public async Task DisposeAsync_WithoutInitialization_DoesNotThrow()
    {
        // Arrange
        var fixture = new EventHookPublisherFailureFixture();

        // Act & Assert - no exception means the null-connection branch is safe
        await fixture.DisposeAsync();
    }

    #endregion
}