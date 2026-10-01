using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     DRK-1951 R5: when an interceptor that runs before the hook interceptor suppresses the concurrency
///     exception, the save continues, so the hook context must not be evicted and the AfterSave hooks still get
///     the BeforeSave snapshot.
/// </summary>
public class SuppressedConcurrencyExceptionTests(SuppressedConcurrencyExceptionFixture fixture)
    : IClassFixture<SuppressedConcurrencyExceptionFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_ConcurrencyExceptionSuppressed_AfterSaveHookSeesEntityOnce()
    {
        // Arrange: an UPDATE of a row that does not exist affects zero rows.
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        var hook = scope.ServiceProvider.GetRequiredKeyedService<EntryRecordingHook>(typeof(HookContext).FullName);
        db.Set<CustomerProfile>().Update(new CustomerProfile { Id = Guid.NewGuid(), Name = "Suppressed" });

        // Act
        await db.SaveChangesAsync();

        // Assert
        hook.BeforeSaveEntities.Cast<CustomerProfile>().Select(p => p.Name).ShouldBe(["Suppressed"]);
        hook.AfterSaveEntities.Cast<CustomerProfile>().Select(p => p.Name).ShouldBe(["Suppressed"]);
        HookCacheProbe.HasEntryFor(fixture.Provider, db).ShouldBeFalse();
    }

    #endregion
}

public sealed class SuppressedConcurrencyExceptionFixture : IAsyncLifetime
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

        // AddDbContextWithHook adds the hook interceptor after the caller's builder runs, so the suppressing
        // interceptor added here runs first and the hook interceptor receives an already suppressed result.
        Provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlite(_connection).UseAutoConfigModel()
                    .AddInterceptors(new SuppressingConcurrencyInterceptor()))
            .AddHook<HookContext, EntryRecordingHook>()
            .BuildServiceProvider();

        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HookContext>().Database.EnsureCreatedAsync();
    }

    #endregion
}

/// <summary>
///     Suppresses every concurrency exception, so the save completes as if the rows were affected.
/// </summary>
public sealed class SuppressingConcurrencyInterceptor : SaveChangesInterceptor
{
    #region Methods

    public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(
        ConcurrencyExceptionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(InterceptionResult.Suppress());

    #endregion
}
