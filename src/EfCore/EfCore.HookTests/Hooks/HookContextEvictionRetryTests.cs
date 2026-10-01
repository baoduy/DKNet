using System.Reflection;
using System.Runtime.CompilerServices;
using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     DRK-1952: every async save attempt gets a fresh hook context, and no hook context outlives its attempt
///     on a cancelled save, a throwing BeforeSave hook or a concurrency conflict. Each test builds its own
///     provider, so the keyed singleton interceptor's cache holds only that test's entries.
/// </summary>
public class HookContextEvictionRetryTests : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;
    private ServiceProvider _provider = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlite(_connection).UseAutoConfigModel())
            .AddHook<HookContext, EntryRecordingHook>()
            .AddHook<HookContext, ArmedFailureHook>()
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HookContext>().Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task S1_SaveChangesAsync_RetryAfterCancelledSave_HooksSeeOnlyCurrentAttemptEntries()
    {
        // Arrange
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var hook = GetRecordingHook(scope);
        var cancelledProfile = new CustomerProfile { Name = "Cancelled" };
        db.Set<CustomerProfile>().Add(cancelledProfile);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => db.SaveChangesAsync(cancelled.Token));

        var retriedProfile = new CustomerProfile { Name = "Retried" };
        db.Set<CustomerProfile>().Add(retriedProfile);

        // Act
        await db.SaveChangesAsync();

        // Assert
        hook.BeforeSaveEntities.Count.ShouldBe(3);
        hook.BeforeSaveEntities.ShouldBe([cancelledProfile, cancelledProfile, retriedProfile], ignoreOrder: true);
        hook.AfterSaveEntities.Count.ShouldBe(2);
        hook.AfterSaveEntities.ShouldBe([cancelledProfile, retriedProfile], ignoreOrder: true);
    }

    [Fact]
    public async Task S2_SaveChangesAsync_CancelledSaveNeverRetried_LeavesNoCachedHookContext()
    {
        // Arrange
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var cache = GetInterceptorCache(db);
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Cancelled" });

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => db.SaveChangesAsync(cancelled.Token));

        // Assert
        GetRecordingHook(scope).BeforeSaveEntities.Count.ShouldBe(1);
        cache.ShouldBeEmpty();
    }

    [Fact]
    public async Task S3_SaveChangesAsync_BeforeSaveHookThrows_LeavesNoCachedHookContext()
    {
        // Arrange
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var cache = GetInterceptorCache(db);
        GetFailureHook(scope).ThrowOnNextBeforeSave = new InvalidOperationException("BeforeSave failed");
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Failed" });

        // Act
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        // Assert
        ex.Message.ShouldBe("BeforeSave failed");
        GetRecordingHook(scope).BeforeSaveEntities.Count.ShouldBe(1);
        cache.ShouldBeEmpty();
    }

    [Fact]
    public async Task S3_SaveChangesAsync_RetryAfterBeforeSaveHookThrew_AfterSaveHookSeesOnlyCurrentAttemptEntries()
    {
        // Arrange
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var hook = GetRecordingHook(scope);
        GetFailureHook(scope).ThrowOnNextBeforeSave = new InvalidOperationException("BeforeSave failed");
        var failedProfile = new CustomerProfile { Name = "Failed" };
        db.Set<CustomerProfile>().Add(failedProfile);
        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        var retriedProfile = new CustomerProfile { Name = "Retried" };
        db.Set<CustomerProfile>().Add(retriedProfile);

        // Act
        await db.SaveChangesAsync();

        // Assert
        hook.AfterSaveEntities.Count.ShouldBe(2);
        hook.AfterSaveEntities.ShouldBe([failedProfile, retriedProfile], ignoreOrder: true);
    }

    [Fact]
    public async Task S4_SaveChangesAsync_BeforeSaveHookHonoursCancellation_LeavesNoCachedHookContext()
    {
        // Arrange
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var cache = GetInterceptorCache(db);
        GetFailureHook(scope).HonourCancellation = true;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Cancelled by hook" });

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => db.SaveChangesAsync(cancelled.Token));

        // Assert
        GetRecordingHook(scope).BeforeSaveEntities.Count.ShouldBe(1);
        cache.ShouldBeEmpty();
    }

    [Fact]
    public async Task S5_SaveChangesAsync_RetryAfterConcurrencyConflict_AfterSaveHookSeesOnlyNewEntity()
    {
        // Arrange
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var hook = GetRecordingHook(scope);
        var conflicting = new CustomerProfile { Name = "Conflicting" };
        db.Set<CustomerProfile>().Add(conflicting);
        await db.SaveChangesAsync();

        // The row disappears out of band, so the UPDATE below affects zero rows.
        await db.Set<CustomerProfile>().Where(p => p.Id == conflicting.Id).ExecuteDeleteAsync();
        conflicting.Name = "Conflicting modified";
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());

        db.Entry(conflicting).State = EntityState.Detached;
        var newProfile = new CustomerProfile { Name = "New after conflict" };
        db.Set<CustomerProfile>().Add(newProfile);
        hook.BeforeSaveEntities.Clear();
        hook.AfterSaveEntities.Clear();

        // Act
        await db.SaveChangesAsync();

        // Assert
        hook.AfterSaveEntities.Count.ShouldBe(1);
        hook.AfterSaveEntities.Single().ShouldBeSameAs(newProfile);
    }

    #endregion

    #region Test helpers

    private static EntryRecordingHook GetRecordingHook(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredKeyedService<EntryRecordingHook>(typeof(HookContext).FullName);

    private static ArmedFailureHook GetFailureHook(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredKeyedService<ArmedFailureHook>(typeof(HookContext).FullName);

    /// <summary>
    ///     Reads the cache of the keyed singleton interceptor the fixture registered, after proving it is the
    ///     same instance wired into <paramref name="db" />'s options, so an empty cache cannot come from
    ///     reading the wrong interceptor.
    /// </summary>
    private ConditionalWeakTable<DbContext, DKNet.EfCore.Hooks.Internals.HookContext> GetInterceptorCache(HookContext db)
    {
        var interceptor = _provider.GetRequiredKeyedService<HookRunnerInterceptor>(typeof(HookContext).FullName);
        db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()!.Interceptors!
            .ShouldContain(interceptor);

        var cacheField = typeof(HookRunnerInterceptor).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
        return (ConditionalWeakTable<DbContext, DKNet.EfCore.Hooks.Internals.HookContext>)cacheField!.GetValue(interceptor)!;
    }

    #endregion
}

/// <summary>
///     A BeforeSave hook that does nothing until a test arms it: <see cref="ThrowOnNextBeforeSave" /> throws once,
///     <see cref="HonourCancellation" /> throws <see cref="OperationCanceledException" /> on a cancelled token.
///     Scoped per DbContext scope, so arming it never leaks into another test.
/// </summary>
public sealed class ArmedFailureHook : IBeforeSaveHookAsync
{
    #region Properties

    public bool HonourCancellation { get; set; }

    public Exception? ThrowOnNextBeforeSave { get; set; }

    #endregion

    #region Methods

    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        if (HonourCancellation) cancellationToken.ThrowIfCancellationRequested();

        var exception = ThrowOnNextBeforeSave;
        ThrowOnNextBeforeSave = null;
        return exception is null ? Task.CompletedTask : throw exception;
    }

    #endregion
}
