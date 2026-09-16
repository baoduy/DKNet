using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     Proves DRK-1398 R1/R7 directly on <c>DataOwnerHook</c>, without <c>EfCoreAuditHook</c> in the
///     container: a registered <see cref="ICurrentUserProvider" /> returning a non-empty value must stop
///     <c>DataOwnerHook</c> itself from stamping <c>CreatedBy</c> from the ownership key — the decision has
///     to hold on this hook's own logic, not rely on <c>EfCoreAuditHook</c> stamping it back afterwards.
/// </summary>
public sealed class DataOwnerHookCurrentUserPrecedenceTests : IAsyncLifetime
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
            .AddScoped<ICurrentUserProvider, FixedCurrentUserProvider>()
            .AddDataOwnerProvider<PrecedenceDbContext, MultiKeyOwnerProvider>()
            .AddDbContextWithHook<PrecedenceDbContext>(builder => builder.UseSqlite(_connection))
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<PrecedenceDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task RegisteredCurrentUserProvider_StopsDataOwnerHookFromStampingCreatedBy()
    {
        // Arrange: no EfCoreAuditHook is registered here, so if DataOwnerHook doesn't defer, nothing else
        // fills CreatedBy either — isolating the precedence decision to DataOwnerHook alone.
        var db = Provider.GetRequiredService<PrecedenceDbContext>();
        var entity = new PrecedenceEntity();

        // Act
        db.Add(entity);
        await db.SaveChangesAsync();

        // Assert: OwnedBy still comes from the tenant key, but CreatedBy is left for EfCoreAuditHook
        entity.OwnedBy.ShouldBe("Steven");
        entity.CreatedBy.ShouldBeNullOrEmpty();
    }

    #endregion
}

/// <summary>
///     Mirrors <see cref="DataOwnerHookCurrentUserPrecedenceTests" /> with no <see cref="ICurrentUserProvider" />
///     registered at all — <c>DataOwnerHook</c> must fall back to stamping <c>CreatedBy</c> from the
///     ownership key exactly as it did before DRK-1398 (R2).
/// </summary>
public sealed class DataOwnerHookNoCurrentUserProviderTests : IAsyncLifetime
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
            .AddDataOwnerProvider<PrecedenceDbContext, MultiKeyOwnerProvider>()
            .AddDbContextWithHook<PrecedenceDbContext>(builder => builder.UseSqlite(_connection))
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<PrecedenceDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task NoCurrentUserProvider_DataOwnerHookStampsCreatedByFromOwnershipKey()
    {
        var db = Provider.GetRequiredService<PrecedenceDbContext>();
        var entity = new PrecedenceEntity();

        db.Add(entity);
        await db.SaveChangesAsync();

        entity.CreatedBy.ShouldBe("Steven");
        entity.OwnedBy.ShouldBe("Steven");
    }

    #endregion
}

#region Test doubles

internal sealed class FixedCurrentUserProvider : ICurrentUserProvider
{
    #region Methods

    public string? GetCurrentUser() => "steven.hoang@transwap.com";

    #endregion
}

internal sealed class PrecedenceEntity : AuditedEntity<Guid>, IOwnedBy
{
    #region Constructors

    public PrecedenceEntity() : base(Guid.NewGuid())
    {
    }

    #endregion

    #region Properties

    public string OwnedBy { get; private set; } = string.Empty;

    #endregion
}

internal sealed class PrecedenceDbContext(
    DbContextOptions<PrecedenceDbContext> options,
    IEnumerable<IDataOwnerProvider> dataOwnerProviders) : DbContext(options), IDataOwnerDbContext
{
    #region Fields

    private readonly IDataOwnerProvider? _dataOwnerProvider = dataOwnerProviders.FirstOrDefault();

    #endregion

    #region Properties

    public IEnumerable<string> AccessibleKeys => _dataOwnerProvider?.GetAccessibleKeys() ?? [];

    public DbSet<PrecedenceEntity> Items => Set<PrecedenceEntity>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CreatedBy is non-nullable on IAuditedProperties, but this test deliberately leaves it unset —
        // this fixture's own mapping, not the framework, allows the column to be null.
        modelBuilder.Entity<PrecedenceEntity>().Property(p => p.CreatedBy).IsRequired(false);
    }

    #endregion
}

#endregion
