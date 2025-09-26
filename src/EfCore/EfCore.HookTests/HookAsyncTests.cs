using DKNet.EfCore.Extensions.Snapshots;

namespace EfCore.HookTests;

public class HookAsyncTests
{
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
        await hook.RunBeforeSaveAsync(snapshot, CancellationToken.None);

        // The default implementation should complete successfully
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
        await hook.RunAfterSaveAsync(snapshot, CancellationToken.None);

        // The default implementation should complete successfully
        Assert.True(true);
    }

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
        await hook.RunBeforeSaveAsync(snapshot, cancellationToken);
        await hook.RunAfterSaveAsync(snapshot, cancellationToken);

        // The default implementations should handle cancellation tokens properly
        Assert.True(true);
    }

    // Test implementation of HookAsync base class
    private class TestHookAsync : HookAsync
    {
        // Uses default implementations from HookAsync base class
    }
}