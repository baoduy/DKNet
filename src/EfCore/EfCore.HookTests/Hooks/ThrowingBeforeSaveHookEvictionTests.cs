using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.Data.Sqlite;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     DRK-1951: a BeforeSave hook that throws ends the save before EF's own <c>try</c>, so EF signals no end
///     callback. The hook context must still be evicted (R2), the hook's exception must reach the caller
///     unchanged (R4), and a retry must hand the hooks only its own pending entries (R1).
/// </summary>
public class ThrowingBeforeSaveHookEvictionTests(ThrowingBeforeSaveHookFixture fixture)
    : IClassFixture<ThrowingBeforeSaveHookFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_BeforeSaveHookThrows_CacheHoldsNoEntryForDbContext()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Thrown" });

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        // Assert
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_BeforeSaveHookThrows_PropagatesSameExceptionInstance()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var throwing = scope.ServiceProvider.GetRequiredKeyedService<ThrowOnceBeforeSaveHook>(
            typeof(HookContext).FullName);
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Thrown" });

        // Act
        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        // Assert
        thrown.ShouldBeSameAs(throwing.Error);
    }

    [Fact]
    public async Task SaveChangesAsync_RetryAfterBeforeSaveHookThrew_HooksSeeOnlyRetriedEntity()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var hook = scope.ServiceProvider.GetRequiredKeyedService<EntryRecordingHook>(typeof(HookContext).FullName);
        var thrown = new CustomerProfile { Name = "Thrown" };
        db.Set<CustomerProfile>().Add(thrown);

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        hook.BeforeSaveEntities.Count.ShouldBe(1);
        hook.AfterSaveEntities.Count.ShouldBe(0);
        hook.BeforeSaveEntities.Clear();

        // The caller drops the rejected entity and saves something else on the same DbContext.
        db.Entry(thrown).State = EntityState.Detached;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Retried" });

        // Act
        await db.SaveChangesAsync();

        // Assert
        hook.BeforeSaveEntities.Cast<CustomerProfile>().Select(p => p.Name).ShouldBe(["Retried"]);
        hook.AfterSaveEntities.Cast<CustomerProfile>().Select(p => p.Name).ShouldBe(["Retried"]);
    }

    #endregion
}

public sealed class ThrowingBeforeSaveHookFixture : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;

    #endregion

    #region Properties

    public ServiceProvider Provider { get; private set; } = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await Provider.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        // The recording hook is registered first, so it records the failing pass before the throwing hook runs.
        Provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlite(_connection).UseAutoConfigModel())
            .AddHook<HookContext, EntryRecordingHook>()
            .AddHook<HookContext, ThrowOnceBeforeSaveHook>()
            .BuildServiceProvider();

        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HookContext>().Database.EnsureCreatedAsync();
    }

    #endregion
}

/// <summary>
///     Throws on its first BeforeSave pass only. Scoped per DbContext scope, so each test gets one throw.
/// </summary>
public sealed class ThrowOnceBeforeSaveHook : IBeforeSaveHookAsync
{
    #region Fields

    private bool _thrown;

    #endregion

    #region Properties

    public InvalidOperationException Error { get; } = new("BeforeSave hook failed");

    #endregion

    #region Methods

    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        if (_thrown) return Task.CompletedTask;

        _thrown = true;
        throw Error;
    }

    #endregion
}
