using DKNet.EfCore.DataAuthorization.Internals;
using DKNet.EfCore.Hooks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     Covers <see cref="DataOwnerHook.BeforeSaveAsync" />'s guard against silent <see cref="IOwnedBy.OwnedBy" />
///     reassignment on update, and the surrounding <c>AutoDetectChangesEnabled</c> save-context handling.
/// </summary>
/// <remarks>
///     Sets up its own SQLite connection and DI container per test (rather than sharing one via
///     <see cref="IClassFixture{TFixture}" />) because a couple of scenarios here deliberately push
///     <c>SaveChangesAsync</c> into throwing — a shared, class-wide <see cref="DddContext" /> would carry a
///     poisoned/half-added entity over into the next test.
/// </remarks>
public class DataOwnerHookTests : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;

    #endregion

    #region Properties

    private ServiceProvider Provider { get; set; } = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (_connection != null) await _connection.DisposeAsync();
        await Provider.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        Provider = new ServiceCollection()
            .AddLogging()
            .AddDataOwnerProvider<DddContext, MultiKeyOwnerProvider>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlite(_connection)
                    .UseAutoConfigModel())
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task GuardOwnedByReassignment_ToAccessibleKey_PersistsNewOwner()
    {
        // Arrange: entity persisted under an owner the current context can access
        var db = Provider.GetRequiredService<DddContext>();
        var entity = new Root("Guard Test - Accessible", "Steven");
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Act: reassign to another key that IS in GetAccessibleKeys() ("Bob")
        entity.SetOwnedBy("Bob");
        await db.SaveChangesAsync();

        // Assert: the reassignment persists — it isn't reverted
        var reloaded = await db.Set<Root>().AsNoTracking().FirstAsync(r => r.Id == entity.Id);
        reloaded.OwnedBy.ShouldBe("Bob");
    }

    [Fact]
    public async Task GuardOwnedByReassignment_ToBlank_RevertsToOriginalOwner()
    {
        // Arrange
        var db = Provider.GetRequiredService<DddContext>();
        var entity = new Root("Guard Test - Blank", "Steven");
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Act: blank out OwnedBy and save
        entity.SetOwnedBy(string.Empty);
        await db.SaveChangesAsync();

        // Assert: reverted to the original owner instead of persisting an orphaned row
        var reloaded = await db.Set<Root>().AsNoTracking().FirstAsync(r => r.Id == entity.Id);
        reloaded.OwnedBy.ShouldBe("Steven");
    }

    [Fact]
    public async Task GuardOwnedByReassignment_ToInaccessibleKey_RevertsToOriginalOwner()
    {
        // Arrange: entity persisted under an owner the current context can access
        var db = Provider.GetRequiredService<DddContext>();
        var entity = new Root("Guard Test - Inaccessible", "Steven");
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Act: attempt to move ownership to a key outside GetAccessibleKeys() ("intruder")
        entity.SetOwnedBy("intruder");
        await db.SaveChangesAsync();

        // Assert: persisted value reverted to the original owner — never moves to another tenant
        var reloaded = await db.Set<Root>().AsNoTracking().FirstAsync(r => r.Id == entity.Id);
        reloaded.OwnedBy.ShouldBe("Steven");
    }

    [Fact]
    public async Task StampAddedEntity_NewEntityWithoutOwnedBy_GetsStampedWithOwnershipKey()
    {
        // Arrange: a new entity added without an OwnedBy value pre-assigned, relying on the hook to
        // stamp GetOwnershipKey() on save (the "Added" contract described by DataOwnerHook's own docs).
        var db = Provider.GetRequiredService<DddContext>();
        var entity = new Root("Never Stamped", "placeholder");
        entity.SetOwnedBy(string.Empty);

        // Act
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Assert: the hook stamps the current context's ownership key
        entity.OwnedBy.ShouldBe("Steven");
    }

    [Fact]
    public async Task StampAddedEntity_OwnedByHasNoSetter_ThrowsInsteadOfSwallowingFailure()
    {
        // Arrange: OwnedBy is a computed, getter-only property with no backing field, so EF Core's own
        // model excludes it and the reflection fallback finds no writable property either. The stamping
        // must surface that failure rather than silently doing nothing.
        var db = Provider.GetRequiredService<DddContext>();
        var entity = new NoSetterOwnedEntity("Never Stamped");
        await db.AddAsync(entity);

        // Act
        var ex = await Record.ExceptionAsync(() => db.SaveChangesAsync());

        // Assert
        ex.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public async Task UpdatingOwner_RestoresCallersPriorAutoDetectChangesEnabledSetting()
    {
        // Arrange: caller had explicitly turned change detection off before saving
        var db = Provider.GetRequiredService<DddContext>();
        var entity = new Root("AutoDetect Restore Test", "Steven");
        await db.AddAsync(entity);
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        // Act
        await db.SaveChangesAsync();

        // Assert: the hook's own try/finally toggling doesn't leak past the save
        db.ChangeTracker.AutoDetectChangesEnabled.ShouldBeFalse();
    }

    #endregion
}
