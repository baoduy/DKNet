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

    [Fact]
    public async Task SaveChangesAsync_AfterSaveHookSavesSyncWithHooksDisabled_LaterHookSeesOuterEntries()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var nesting = scope.ServiceProvider.GetRequiredKeyedService<NestingSaveHook>(typeof(HookContext).FullName);
        var recorder = scope.ServiceProvider.GetRequiredKeyedService<PassRecordingHook>(typeof(HookContext).FullName);
        nesting.NestSyncWithHooksDisabledInAfterSave = true;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Outer" });

        // Act
        await db.SaveChangesAsync();

        // Assert: the nested sync save runs no hooks, and its exit must not pop the outer save's live context.
        recorder.BeforeSavePasses.ShouldBe(["Outer"]);
        recorder.AfterSavePasses.ShouldBe(["Outer"]);
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveChangesAsync_NestedSaveAfterSaveHookFails_LaterOuterHookSeesOuterEntries(bool cancel)
    {
        // Arrange: the nested save's AfterSave pass fails, so EF also signals SaveChangesFailed or
        // SaveChangesCanceled after SavedChangesAsync already popped the nested context.
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var nesting = scope.ServiceProvider.GetRequiredKeyedService<NestingSaveHook>(typeof(HookContext).FullName);
        var recorder = scope.ServiceProvider.GetRequiredKeyedService<PassRecordingHook>(typeof(HookContext).FullName);
        var failing = scope.ServiceProvider.GetRequiredKeyedService<InnerAfterSaveFailingHook>(typeof(HookContext).FullName);
        nesting.NestInAfterSave = true;
        nesting.SwallowNestedFailure = true;
        failing.Error = cancel ? new OperationCanceledException("inner AfterSave cancelled") : new InvalidOperationException("inner AfterSave failed");
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Outer" });

        // Act
        await db.SaveChangesAsync();

        // Assert
        nesting.SwallowedError.ShouldBeSameAs(failing.Error);
        recorder.BeforeSavePasses.ShouldBe(["Outer", "Inner"]);
        recorder.AfterSavePasses.ShouldBe(["Inner", "Outer"]);
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_NestedSaveHitsConcurrencyConflict_LaterOuterHookSeesOuterEntries()
    {
        // Arrange: the nested save updates a row that does not exist, so it fails with a concurrency conflict.
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var nesting = scope.ServiceProvider.GetRequiredKeyedService<NestingSaveHook>(typeof(HookContext).FullName);
        var recorder = scope.ServiceProvider.GetRequiredKeyedService<PassRecordingHook>(typeof(HookContext).FullName);
        nesting.NestConflictInAfterSave = true;
        nesting.SwallowNestedFailure = true;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Outer" });

        // Act
        await db.SaveChangesAsync();

        // Assert
        nesting.SwallowedError.ShouldBeOfType<DbUpdateConcurrencyException>();
        recorder.BeforeSavePasses.ShouldBe(["Outer", "Conflict"]);
        recorder.AfterSavePasses.ShouldBe(["Outer"]);
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    #endregion
}

/// <summary>
///     DRK-1956 round 3: a nested save that an interceptor registered after the hook interceptor fails leaves its
///     context above the outer save's, because EF signals no end for it. The outer save's own exit must still pop
///     its own context, not the nested leftover.
/// </summary>
public class NestedSaveRejectedByLaterInterceptorTests(NestedSaveRejectedFixture fixture)
    : IClassFixture<NestedSaveRejectedFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_BeforeSaveNestedSaveRejectedAndNotSwallowed_CacheHoldsNoEntryForDbContext()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var nesting = scope.ServiceProvider.GetRequiredKeyedService<NestingSaveHook>(typeof(HookContext).FullName);
        nesting.NestInBeforeSave = true;
        nesting.InnerName = RejectingSavingInterceptor.RejectedName;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Outer" });

        // Act
        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        // Assert
        thrown.Message.ShouldBe("Rejected by a later interceptor");
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_BeforeSaveNestedSaveRejectedAndSwallowed_AfterSaveHookSeesOuterEntries()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var nesting = scope.ServiceProvider.GetRequiredKeyedService<NestingSaveHook>(typeof(HookContext).FullName);
        var recorder = scope.ServiceProvider.GetRequiredKeyedService<PassRecordingHook>(typeof(HookContext).FullName);
        nesting.NestInBeforeSave = true;
        nesting.SwallowNestedFailure = true;
        nesting.InnerName = RejectingSavingInterceptor.RejectedName;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Outer" });

        // Act
        await db.SaveChangesAsync();

        // Assert: the nested pass saw both pending entities; the outer passes see only the outer snapshot. The
        // nested leftover was dropped and disposed when the outer BeforeSave pass ended.
        nesting.SwallowedError.ShouldBeOfType<InvalidOperationException>();
        recorder.BeforeSavePasses.ShouldBe(["Outer,Rejected", "Outer"]);
        recorder.AfterSavePasses.ShouldBe(["Outer"]);
        Should.Throw<ObjectDisposedException>(() => _ = recorder.BeforeSaveSnapshots[0].Entities);
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_RetryAfterLaterInterceptorRejectedSave_DisposesLeftoverContext()
    {
        // Arrange: a save the later interceptor rejects leaves its context behind, with no end signal.
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var recorder = scope.ServiceProvider.GetRequiredKeyedService<PassRecordingHook>(typeof(HookContext).FullName);
        var rejected = new CustomerProfile { Name = RejectingSavingInterceptor.RejectedName };
        db.Set<CustomerProfile>().Add(rejected);
        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(rejected).State = EntityState.Detached;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Retried" });

        // Act
        await db.SaveChangesAsync();

        // Assert: the retry discarded the leftover and disposed it.
        recorder.BeforeSavePasses.ShouldBe(["Rejected", "Retried"]);
        Should.Throw<ObjectDisposedException>(() => _ = recorder.BeforeSaveSnapshots[0].Entities);
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_AfterSaveNestedSaveRejectedAndSwallowed_CacheHoldsNoEntryForDbContext()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var nesting = scope.ServiceProvider.GetRequiredKeyedService<NestingSaveHook>(typeof(HookContext).FullName);
        var recorder = scope.ServiceProvider.GetRequiredKeyedService<PassRecordingHook>(typeof(HookContext).FullName);
        nesting.NestInAfterSave = true;
        nesting.SwallowNestedFailure = true;
        nesting.InnerName = RejectingSavingInterceptor.RejectedName;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Outer" });

        // Act
        await db.SaveChangesAsync();

        // Assert
        nesting.SwallowedError.ShouldBeOfType<InvalidOperationException>();
        recorder.BeforeSavePasses.ShouldBe(["Outer", "Rejected"]);
        recorder.AfterSavePasses.ShouldBe(["Outer"]);
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    #endregion
}

public sealed class NestedSaveRejectedFixture : IAsyncLifetime
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

        // Same order as LaterInterceptorThrowFixture: the hook interceptor first, the rejecting one after it.
        Provider = new ServiceCollection()
            .AddLogging()
            .AddHook<HookContext, NestingSaveHook>()
            .AddHook<HookContext, PassRecordingHook>()
            .AddDbContext<HookContext>((provider, o) =>
            {
                ((DbContextOptionsBuilder<HookContext>)o).UseSqlite(_connection).UseAutoConfigModel();
                o.UseHooks<HookContext>(provider);
                o.AddInterceptors(new RejectingSavingInterceptor());
            })
            .BuildServiceProvider();

        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HookContext>().Database.EnsureCreatedAsync();
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
            .AddHook<HookContext, InnerAfterSaveFailingHook>()
            .BuildServiceProvider();

        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HookContext>().Database.EnsureCreatedAsync();
    }

    #endregion
}

/// <summary>
///     Saves the same DbContext from inside its hook pass, once: an added "Inner" profile async with hooks running
///     or sync inside <c>DisableHooks()</c>, or an update of a missing "Conflict" row that fails the nested save.
///     With <see cref="SwallowNestedFailure" /> it records the nested save's error and detaches the nested entity
///     instead of rethrowing.
/// </summary>
public sealed class NestingSaveHook : IHookAsync
{
    #region Fields

    private bool _nested;
    private CustomerProfile? _nestedEntity;

    #endregion

    #region Properties

    public bool NestConflictInAfterSave { get; set; }

    public bool NestInAfterSave { get; set; }

    public bool NestInBeforeSave { get; set; }

    public bool NestSyncWithHooksDisabledInAfterSave { get; set; }

    public string InnerName { get; set; } = "Inner";

    public Exception? SwallowedError { get; private set; }

    public bool SwallowNestedFailure { get; set; }

    #endregion

    #region Methods

    public Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        if (NestSyncWithHooksDisabledInAfterSave) SaveInnerSyncWithHooksDisabled(context);
        if (NestConflictInAfterSave) return SaveConflictAsync(context, cancellationToken);
        return NestInAfterSave ? SaveInnerAsync(context, cancellationToken) : Task.CompletedTask;
    }

    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default) =>
        NestInBeforeSave ? SaveInnerAsync(context, cancellationToken) : Task.CompletedTask;

    private Task SaveConflictAsync(SnapshotContext context, CancellationToken cancellationToken)
    {
        if (_nested) return Task.CompletedTask;

        _nested = true;
        _nestedEntity = new CustomerProfile { Id = Guid.NewGuid(), Name = "Conflict" };
        context.DbContext.Set<CustomerProfile>().Update(_nestedEntity);
        return SaveNestedAsync(context, cancellationToken);
    }

    private Task SaveInnerAsync(SnapshotContext context, CancellationToken cancellationToken)
    {
        if (_nested) return Task.CompletedTask;

        _nested = true;
        _nestedEntity = new CustomerProfile { Name = InnerName };
        context.DbContext.Set<CustomerProfile>().Add(_nestedEntity);
        return SaveNestedAsync(context, cancellationToken);
    }

    private async Task SaveNestedAsync(SnapshotContext context, CancellationToken cancellationToken)
    {
        try
        {
            await context.DbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (SwallowNestedFailure)
        {
            // Give up on the nested entity, so the outer save does not try to save it again.
            SwallowedError = ex;
            context.DbContext.Entry(_nestedEntity!).State = EntityState.Detached;
        }
    }

    private void SaveInnerSyncWithHooksDisabled(SnapshotContext context)
    {
        if (_nested) return;

        _nested = true;
        using (context.DbContext.DisableHooks())
        {
            context.DbContext.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Inner" });
            context.DbContext.SaveChanges();
        }
    }

    #endregion
}

/// <summary>
///     Throws <see cref="Error" /> from an AfterSave pass that holds the nested save's "Inner" profile. Unarmed
///     (<c>null</c>) it does nothing. Scoped per DbContext scope, so arming it never leaks into another test.
/// </summary>
public sealed class InnerAfterSaveFailingHook : IAfterSaveHookAsync
{
    #region Properties

    public Exception? Error { get; set; }

    #endregion

    #region Methods

    public Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default) =>
        Error != null && context.Entities.Any(e => e.Entity is CustomerProfile { Name: "Inner" })
            ? throw Error
            : Task.CompletedTask;

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

    public List<SnapshotContext> BeforeSaveSnapshots { get; } = [];

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
        BeforeSaveSnapshots.Add(context);
        return Task.CompletedTask;
    }

    private static string Names(SnapshotContext context) =>
        string.Join(",", context.Entities.Select(e => ((CustomerProfile)e.Entity).Name).Order(StringComparer.Ordinal));

    #endregion
}
