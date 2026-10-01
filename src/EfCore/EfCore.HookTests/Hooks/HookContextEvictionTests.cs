using System.Collections;
using System.Reflection;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     DRK-1951: a save that ends by cancellation or by a concurrency failure leaves no hook context cached
///     for its DbContext (R2), and a retry on that DbContext hands its hooks only its own pending entries (R1).
/// </summary>
public class HookContextEvictionTests(SnapshotCaptureOnceFixture fixture)
    : IClassFixture<SnapshotCaptureOnceFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_Cancelled_CacheHoldsNoEntryForDbContext()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Cancelled" });

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => db.SaveChangesAsync(cancelled.Token));

        // Assert
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_ConcurrencyFailure_CacheHoldsNoEntryForDbContext()
    {
        // Arrange: an UPDATE of a row no other writer left in place affects zero rows.
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        db.Set<CustomerProfile>().Update(new CustomerProfile { Id = Guid.NewGuid(), Name = "Conflict" });

        // Act
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());

        // Assert
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_RetryAfterConcurrencyFailure_HooksSeeOnlyRetriedEntity()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var hook = scope.ServiceProvider.GetRequiredKeyedService<EntryRecordingHook>(typeof(HookContext).FullName);
        var conflict = new CustomerProfile { Id = Guid.NewGuid(), Name = "Conflict" };
        db.Set<CustomerProfile>().Update(conflict);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        hook.BeforeSaveEntities.Count.ShouldBe(1);
        hook.AfterSaveEntities.Count.ShouldBe(0);
        hook.BeforeSaveEntities.Clear();

        // The caller gives up on the conflicting update and saves something else on the same DbContext.
        db.Entry(conflict).State = EntityState.Detached;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Retried" });

        // Act
        await db.SaveChangesAsync();

        // Assert
        hook.BeforeSaveEntities.Cast<CustomerProfile>().Select(p => p.Name).ShouldBe(["Retried"]);
        hook.AfterSaveEntities.Cast<CustomerProfile>().Select(p => p.Name).ShouldBe(["Retried"]);
    }

    #endregion
}

/// <summary>
///     Reads the hook interceptor's private cache without naming its concrete type, so the probe holds whether
///     the cache is keyed by <see cref="DbContextId.InstanceId" /> or by the DbContext instance itself.
/// </summary>
internal static class HookCacheProbe
{
    #region Methods

    public static bool HasEntryFor(IServiceProvider provider, DbContext db)
    {
        var interceptor = provider.GetRequiredKeyedService<HookRunnerInterceptor>(db.GetType().FullName);
        var field = typeof(HookRunnerInterceptor).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
        var cache = (IEnumerable)field!.GetValue(interceptor)!;

        foreach (var entry in cache)
        {
            var key = entry.GetType().GetProperty("Key")!.GetValue(entry);
            if (key is DbContext keyDb ? ReferenceEquals(keyDb, db) : key is Guid id && id == db.ContextId.InstanceId)
                return true;
        }

        return false;
    }

    #endregion
}
