using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HookContext = EfCore.HookTests.Data.HookContext;
using InternalHookContext = DKNet.EfCore.Hooks.Internals.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     Acceptance tests for DRK-1899 (Option A): hooks run only on <c>SaveChangesAsync</c>, so a synchronous
///     <c>SaveChanges()</c> on a hook-enabled context must fail closed instead of silently saving with zero
///     hooks run. The synchronous <c>SavedChanges</c>/<c>SaveChangesFailed</c> members must clean up the
///     cached <see cref="InternalHookContext" /> exactly like their async twins.
/// </summary>
public class HookRunnerInterceptorSyncSaveTests : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;
    private ServiceProvider _provider = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (_provider != null!) await _provider.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<HookContext>(o => o.UseSqlite(_connection).UseAutoConfigModel())
            .AddHook<HookContext, HookTest>()
            .BuildServiceProvider();

        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<HookContext>().Database.EnsureCreatedAsync();

        _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName).Reset();
    }

    [Fact]
    public void SyncSaveChanges_OnHookedContext_ThrowsNotSupportedException()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Sync rejected" });

        var exception = Should.Throw<NotSupportedException>(() => db.SaveChanges());

        exception.Message.ShouldBe(
            "DKNet.EfCore.Hooks runs hooks only on SaveChangesAsync. Synchronous SaveChanges() on 'HookContext' " +
            "is not supported; use SaveChangesAsync() or wrap the call in DisableHooks().");
    }

    [Fact]
    public async Task SyncSaveChanges_OnHookedContext_RunsNoHookAndWritesNoRow()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HookContext>();
            db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Sync rejected" });

            Should.Throw<NotSupportedException>(() => db.SaveChanges());
        }

        HookTest.BeforeCallCount.ShouldBe(0);
        HookTest.AfterCallCount.ShouldBe(0);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<HookContext>();
        (await verifyDb.Set<CustomerProfile>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public void SyncSaveChanges_OnHookedContext_CachesNoHookContext()
    {
        var cache = GetCache(_provider.GetRequiredKeyedService<HookRunnerInterceptor>(typeof(HookContext).FullName));

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Sync rejected" });

        Should.Throw<NotSupportedException>(() => db.SaveChanges());

        cache.ShouldBeEmpty();
    }

    [Fact]
    public async Task SyncSaveChanges_InsideDisableHooks_SavesWithZeroHookCalls()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HookContext>();

            using (db.DisableHooks())
            {
                db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Sync with hooks disabled" });
                db.SaveChanges().ShouldBe(1);
            }
        }

        HookTest.BeforeCallCount.ShouldBe(0);
        HookTest.AfterCallCount.ShouldBe(0);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<HookContext>();
        (await verifyDb.Set<CustomerProfile>().Select(c => c.Name).SingleAsync()).ShouldBe("Sync with hooks disabled");
    }

    [Fact]
    public void SyncSavedChanges_RemovesAndDisposesCachedHookContext()
    {
        var interceptor = new HookRunnerInterceptor(NullLogger<HookRunnerInterceptor>.Instance);
        var cache = GetCache(interceptor);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var cached = new InternalHookContext(scope.ServiceProvider, db);
        cache[db.ContextId.InstanceId] = cached;

        var result = interceptor.SavedChanges(
            new SaveChangesCompletedEventData(CreateEventDefinition(db), (_, _) => string.Empty, db, 1),
            1);

        result.ShouldBe(1);
        cache.ShouldBeEmpty();
        Should.Throw<ObjectDisposedException>(() => _ = cached.Snapshot.Entities);
        HookTest.BeforeCallCount.ShouldBe(0);
        HookTest.AfterCallCount.ShouldBe(0);
    }

    [Fact]
    public void SyncSaveChangesFailed_RemovesAndDisposesCachedHookContext()
    {
        var interceptor = new HookRunnerInterceptor(NullLogger<HookRunnerInterceptor>.Instance);
        var cache = GetCache(interceptor);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var cached = new InternalHookContext(scope.ServiceProvider, db);
        cache[db.ContextId.InstanceId] = cached;

        Should.NotThrow(() => interceptor.SaveChangesFailed(
            new DbContextErrorEventData(
                CreateEventDefinition(db),
                (_, _) => string.Empty,
                db,
                new DbUpdateException("save failed"))));

        cache.ShouldBeEmpty();
        Should.Throw<ObjectDisposedException>(() => _ = cached.Snapshot.Entities);
        HookTest.BeforeCallCount.ShouldBe(0);
        HookTest.AfterCallCount.ShouldBe(0);
    }

    #endregion

    #region Test helpers

    private static ConcurrentDictionary<Guid, InternalHookContext> GetCache(HookRunnerInterceptor interceptor)
    {
        var field = typeof(HookRunnerInterceptor).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
        return (ConcurrentDictionary<Guid, InternalHookContext>)field!.GetValue(interceptor)!;
    }

    private static EventDefinition CreateEventDefinition(DbContext db) =>
        new(
            db.GetService<ILoggingOptions>(),
            CoreEventId.SaveChangesCompleted,
            LogLevel.Debug,
            "CoreEventId.SaveChangesCompleted",
            level => LoggerMessage.Define(level, CoreEventId.SaveChangesCompleted, "SaveChanges completed"));

    #endregion
}
