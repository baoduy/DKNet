using DKNet.EfCore.Extensions.Snapshots;

namespace EfCore.HookTests;

public class HookAsyncTests
{
    #region Methods

    [Fact]
    public async Task HookAsync_BothMethods_ShouldCompleteWithCancellationToken()
    {
        // Arrange
        var hook = new TestHookAsync();
        await using var context = new HookContext(new DbContextOptionsBuilder<HookContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        await using var snapshot = new SnapshotContext(context);
        var cancellationToken = new CancellationToken();

        // Act & Assert - should complete without throwing
        await hook.BeforeSaveAsync(snapshot, cancellationToken);
        await hook.AfterSaveAsync(snapshot, cancellationToken);

        // The default implementations should handle cancellation tokens properly
        Assert.True(true);
    }

    [Fact]
    public async Task HookAsync_RunAfterSaveAsync_ShouldCompleteWithoutAction()
    {
        // Arrange
        var hook = new TestHookAsync();
        await using var context = new HookContext(new DbContextOptionsBuilder<HookContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        await using var snapshot = new SnapshotContext(context);

        // Act & Assert - should complete without throwing
        await hook.AfterSaveAsync(snapshot, CancellationToken.None);

        // The default implementation should complete successfully
        Assert.True(true);
    }

    [Fact]
    public async Task HookAsync_RunBeforeSaveAsync_ShouldCompleteWithoutAction()
    {
        // Arrange
        var hook = new TestHookAsync();
        await using var context = new HookContext(new DbContextOptionsBuilder<HookContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        await using var snapshot = new SnapshotContext(context);

        // Act & Assert - should complete without throwing
        await hook.BeforeSaveAsync(snapshot, CancellationToken.None);

        // The default implementation should complete successfully
        Assert.True(true);
    }

    #endregion

    // Test implementation of HookAsync base class
    private class TestHookAsync : HookAsync
    {
        // Uses default implementations from HookAsync base class
    }
}