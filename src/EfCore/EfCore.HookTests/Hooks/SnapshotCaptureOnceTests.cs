using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.Data.Sqlite;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     DRK-1904: one save hands each pending entity to the BeforeSave and AfterSave hooks exactly once,
///     whether or not <c>acceptAllChangesOnSuccess</c> is set.
/// </summary>
public class SnapshotCaptureOnceTests(SnapshotCaptureOnceFixture fixture) : IClassFixture<SnapshotCaptureOnceFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_WithoutAcceptAllChanges_AfterSaveHookSeesAddedEntityOnce()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var hook = scope.ServiceProvider.GetRequiredKeyedService<EntryRecordingHook>(typeof(HookContext).FullName);
        var profile = new CustomerProfile { Name = "Accept false" };
        db.Set<CustomerProfile>().Add(profile);

        // Act
        await db.SaveChangesAsync(false);

        // Assert
        hook.BeforeSaveEntities.Count.ShouldBe(1);
        hook.BeforeSaveEntities.Single().ShouldBeSameAs(profile);
        hook.AfterSaveEntities.Count.ShouldBe(1);
        hook.AfterSaveEntities.Single().ShouldBeSameAs(profile);
    }

    [Fact]
    public async Task SaveChangesAsync_Default_AfterSaveHookSeesAddedEntityOnce()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var hook = scope.ServiceProvider.GetRequiredKeyedService<EntryRecordingHook>(typeof(HookContext).FullName);
        var profile = new CustomerProfile { Name = "Accept default" };
        db.Set<CustomerProfile>().Add(profile);

        // Act
        await db.SaveChangesAsync();

        // Assert
        hook.BeforeSaveEntities.Count.ShouldBe(1);
        hook.BeforeSaveEntities.Single().ShouldBeSameAs(profile);
        hook.AfterSaveEntities.Count.ShouldBe(1);
        hook.AfterSaveEntities.Single().ShouldBeSameAs(profile);
    }

    #endregion
}

public sealed class SnapshotCaptureOnceFixture : IAsyncLifetime
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
        // Use a shared connection for SQLite in-memory database
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        Provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlite(_connection).UseAutoConfigModel())
            .AddHook<HookContext, EntryRecordingHook>()
            .BuildServiceProvider();

        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HookContext>().Database.EnsureCreatedAsync();
    }

    #endregion
}

/// <summary>
///     Records the entities each hook pass receives. Scoped per DbContext scope, so no state is shared between tests.
/// </summary>
public sealed class EntryRecordingHook : IHookAsync
{
    #region Properties

    public List<object> AfterSaveEntities { get; } = [];

    public List<object> BeforeSaveEntities { get; } = [];

    #endregion

    #region Methods

    public Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        AfterSaveEntities.AddRange(context.Entities.Select(e => e.Entity));
        return Task.CompletedTask;
    }

    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        BeforeSaveEntities.AddRange(context.Entities.Select(e => e.Entity));
        return Task.CompletedTask;
    }

    #endregion
}
