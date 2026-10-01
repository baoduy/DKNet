using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     DRK-1951: an interceptor registered after the hook interceptor throws in <c>SavingChangesAsync</c>, after
///     the BeforeSave hooks ran. EF signals no end callback for that exit, so the hook context stays cached (R3),
///     and the next save on the DbContext must replace it rather than reuse it (R1).
/// </summary>
public class LaterInterceptorThrowEvictionTests(LaterInterceptorThrowFixture fixture)
    : IClassFixture<LaterInterceptorThrowFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_LaterInterceptorThrows_CacheStillHoldsEntryForDbContext()
    {
        // Presence sibling of the cache-empty checks: proves HookCacheProbe sees an entry that is there.
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = RejectingSavingInterceptor.RejectedName });

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        // Assert
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveChangesAsync_RetryAfterLaterInterceptorThrew_HooksSeeOnlyRetriedEntity()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var hook = scope.ServiceProvider.GetRequiredKeyedService<EntryRecordingHook>(typeof(HookContext).FullName);
        var rejected = new CustomerProfile { Name = RejectingSavingInterceptor.RejectedName };
        db.Set<CustomerProfile>().Add(rejected);

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        hook.BeforeSaveEntities.Count.ShouldBe(1);
        hook.AfterSaveEntities.Count.ShouldBe(0);
        hook.BeforeSaveEntities.Clear();

        // The caller drops the rejected entity and saves something else on the same DbContext.
        db.Entry(rejected).State = EntityState.Detached;
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Retried" });

        // Act
        await db.SaveChangesAsync();

        // Assert
        hook.BeforeSaveEntities.Cast<CustomerProfile>().Select(p => p.Name).ShouldBe(["Retried"]);
        hook.AfterSaveEntities.Cast<CustomerProfile>().Select(p => p.Name).ShouldBe(["Retried"]);
    }

    #endregion
}

public sealed class LaterInterceptorThrowFixture : IAsyncLifetime
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

        // AddDbContextWithHook adds the hook interceptor after the caller's builder runs, so a caller-added
        // interceptor would run first. Register the hook interceptor explicitly, then the rejecting one after it.
        Provider = new ServiceCollection()
            .AddLogging()
            .AddHook<HookContext, EntryRecordingHook>()
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
///     Fails <c>SavingChangesAsync</c> while a <see cref="CustomerProfile" /> named <see cref="RejectedName" /> is
///     tracked. Stateless, so it needs no reset between tests.
/// </summary>
public sealed class RejectingSavingInterceptor : SaveChangesInterceptor
{
    #region Fields

    public const string RejectedName = "Rejected";

    #endregion

    #region Methods

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<CustomerProfile>().Any(e => e.Entity.Name == RejectedName))
            throw new InvalidOperationException("Rejected by a later interceptor");

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    #endregion
}
