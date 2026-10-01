using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     DRK-1904 R3: every BeforeSave pass captures the change tracker, even when a cancelled save left the
///     interceptor's cached hook context (and its already-initialized snapshot) behind for the retry.
/// </summary>
public class SnapshotRecaptureOnRetryTests(SnapshotCaptureOnceFixture fixture)
    : IClassFixture<SnapshotCaptureOnceFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_RetryAfterCancelledSave_BeforeSaveHookSeesNewEntity()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var hook = scope.ServiceProvider.GetRequiredKeyedService<EntryRecordingHook>(typeof(HookContext).FullName);
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Cancelled" });

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => db.SaveChangesAsync(cancelled.Token));
        hook.BeforeSaveEntities.Count.ShouldBe(1);

        var retried = new CustomerProfile { Name = "Retried" };
        db.Set<CustomerProfile>().Add(retried);

        // Act
        await db.SaveChangesAsync();

        // Assert
        hook.BeforeSaveEntities.ShouldContain(retried);
    }

    #endregion
}
