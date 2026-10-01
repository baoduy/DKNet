using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.Data.Sqlite;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     DRK-1956 finding 1: a hook that saves the same DbContext again must not dispose the outer save's live
///     hook context. A later hook of the outer save still reads the outer snapshot, and the nested save gets
///     its own context.
/// </summary>
public class NestedSaveReentryTests(NestedSaveFixture fixture) : IClassFixture<NestedSaveFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_AfterSaveHookSavesSameDbContext_LaterHookSeesOuterEntries()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var nesting = scope.ServiceProvider.GetRequiredKeyedService<NestingSaveHook>(typeof(HookContext).FullName);
        var recorder = scope.ServiceProvider.GetRequiredKeyedService<PassRecordingHook>(typeof(HookContext).FullName);
        nesting.NestInAfterSave = true;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Outer" });

        // Act
        await db.SaveChangesAsync();

        // Assert: the nested save runs inside the outer AfterSave pass, before the recorder's outer pass.
        recorder.BeforeSavePasses.ShouldBe(["Outer", "Inner"]);
        recorder.AfterSavePasses.ShouldBe(["Inner", "Outer"]);
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_BeforeSaveHookSavesSameDbContext_LaterHookSeesOuterEntries()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var nesting = scope.ServiceProvider.GetRequiredKeyedService<NestingSaveHook>(typeof(HookContext).FullName);
        var recorder = scope.ServiceProvider.GetRequiredKeyedService<PassRecordingHook>(typeof(HookContext).FullName);
        nesting.NestInBeforeSave = true;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Outer" });

        // Act
        await db.SaveChangesAsync();

        // Assert: the outer entity is still pending when the nested save runs, so EF saves it there too and the
        // nested hooks see both. The outer passes still see only the outer snapshot, never a disposed one.
        recorder.BeforeSavePasses.ShouldBe(["Inner,Outer", "Outer"]);
        recorder.AfterSavePasses.ShouldBe(["Inner,Outer", "Outer"]);
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    #endregion
}

/// <summary>
///     DRK-1956: a leftover context from a save that a later interceptor failed is discarded, and the retry's
///     own context is evicted once it completes, so nothing stays cached for the DbContext.
/// </summary>
public class LaterInterceptorThrowRetryEvictionTests(LaterInterceptorThrowFixture fixture)
    : IClassFixture<LaterInterceptorThrowFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_RetryAfterLaterInterceptorThrew_CacheHoldsNoEntryForDbContext()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var rejected = new CustomerProfile { Name = RejectingSavingInterceptor.RejectedName };
        db.Set<CustomerProfile>().Add(rejected);
        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(rejected).State = EntityState.Detached;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Retried" });

        // Act
        await db.SaveChangesAsync();

        // Assert
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    #endregion
}

public sealed class NestedSaveFixture : IAsyncLifetime
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

        // The nesting hook is registered first, so the recording hook runs after the nested save returns.
        Provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<HookContext>(o => o.UseSqlite(_connection).UseAutoConfigModel())
            .AddHook<HookContext, NestingSaveHook>()
            .AddHook<HookContext, PassRecordingHook>()
            .BuildServiceProvider();

        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HookContext>().Database.EnsureCreatedAsync();
    }

    #endregion
}

/// <summary>
///     Adds an "Inner" profile and saves the same DbContext from inside its hook pass, once.
/// </summary>
public sealed class NestingSaveHook : IHookAsync
{
    #region Fields

    private bool _nested;

    #endregion

    #region Properties

    public bool NestInAfterSave { get; set; }

    public bool NestInBeforeSave { get; set; }

    #endregion

    #region Methods

    public Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default) =>
        NestInAfterSave ? SaveInnerAsync(context, cancellationToken) : Task.CompletedTask;

    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default) =>
        NestInBeforeSave ? SaveInnerAsync(context, cancellationToken) : Task.CompletedTask;

    private async Task SaveInnerAsync(SnapshotContext context, CancellationToken cancellationToken)
    {
        if (_nested) return;

        _nested = true;
        context.DbContext.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Inner" });
        await context.DbContext.SaveChangesAsync(cancellationToken);
    }

    #endregion
}

/// <summary>
///     Records each hook pass as the sorted, comma-joined profile names it received.
/// </summary>
public sealed class PassRecordingHook : IHookAsync
{
    #region Properties

    public List<string> AfterSavePasses { get; } = [];

    public List<string> BeforeSavePasses { get; } = [];

    #endregion

    #region Methods

    public Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        AfterSavePasses.Add(Names(context));
        return Task.CompletedTask;
    }

    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        BeforeSavePasses.Add(Names(context));
        return Task.CompletedTask;
    }

    private static string Names(SnapshotContext context) =>
        string.Join(",", context.Entities.Select(e => ((CustomerProfile)e.Entity).Name).Order(StringComparer.Ordinal));

    #endregion
}
