using System.Linq.Expressions;
using DKNet.EfCore.DataAuthorization.Internals;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     Grants access to "tenant-a" only, used to verify that <see cref="ISpecification{TEntity}.IsIgnoreQueryFilters" />
///     never re-exposes rows owned by other tenants.
/// </summary>
internal sealed class TenantAAccessibleKeysProvider : IDataOwnerProvider
{
    public ICollection<string> GetAccessibleKeys() => ["tenant-a"];

    public string GetOwnershipKey() => "tenant-a";
}

/// <summary>
///     A <see cref="Specification{TEntity}" /> for <see cref="Root" /> that optionally requests
///     <see cref="IsIgnoreQueryFilters" />, used to drive <c>ApplySpecs</c> in tests.
/// </summary>
internal sealed class RootSpec : Specification<Root>
{
    public RootSpec(bool ignoreQueryFilters)
    {
        if (ignoreQueryFilters) IgnoreQueryFilters();
    }
}

public sealed class TenantIsolationFixture : IAsyncLifetime
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
        if (_connection != null) await _connection.DisposeAsync();
        await Provider.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        Provider = new ServiceCollection()
            .AddLogging()
            .AddDataOwnerProvider<DddContext, TenantAAccessibleKeysProvider>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlite(_connection)
                    .UseAutoConfigModel())
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}

/// <summary>
///     Regression tests for DRK-83: <c>ApplySpecs</c>' <see cref="ISpecification{TEntity}.IsIgnoreQueryFilters" />
///     flag must only bypass global query filters that opt in via <see cref="GlobalQueryFilter.IsIgnorable" />,
///     never the row-level tenant/owner isolation filter.
/// </summary>
public class IgnoreQueryFiltersTests(TenantIsolationFixture fixture) : IClassFixture<TenantIsolationFixture>
{
    #region Methods

    [Fact]
    public void DataOwnerAuthQuery_IsIgnorable_IsFalse()
    {
        new DataOwnerAuthQuery().IsIgnorable.ShouldBeFalse();
    }

    [Fact]
    public async Task ApplySpecs_TenantFilter_StaysApplied_WhenIsIgnoreQueryFiltersIsTrue()
    {
        var db = fixture.Provider.GetRequiredService<DddContext>();
        db.AddRange(new Root("A-Item", "tenant-a"), new Root("B-Item", "tenant-b"));
        await db.SaveChangesAsync();

        var repo = new RepositorySpec<DddContext>(db);
        var result = repo.Query(new RootSpec(true)).ToList();

        result.ShouldNotBeEmpty();
        result.ShouldAllBe(r => r.OwnedBy == "tenant-a");
    }

    [Fact]
    public async Task ApplySpecs_IgnorableFilter_IsBypassed_WhenIsIgnoreQueryFiltersIsTrue()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new HiddenFlagDbContext(
            new DbContextOptionsBuilder<HiddenFlagDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        db.Items.AddRange(
            new HiddenFlagEntity { Id = 1, IsHidden = false },
            new HiddenFlagEntity { Id = 2, IsHidden = true });
        await db.SaveChangesAsync();

        var repo = new RepositorySpec<HiddenFlagDbContext>(db);

        repo.Query(new HiddenFlagSpec(false)).ToList()
            .Select(x => x.Id).ShouldBe([1]);

        repo.Query(new HiddenFlagSpec(true)).ToList()
            .Select(x => x.Id).OrderBy(x => x).ShouldBe([1, 2]);
    }

    #endregion
}

/// <summary>Marker used by <see cref="TestOnlyIgnorableFilter" /> to locate the entities it filters.</summary>
internal interface ITestHiddenFlag
{
    bool IsHidden { get; }
}

internal sealed class HiddenFlagEntity : ITestHiddenFlag
{
    public int Id { get; init; }
    public bool IsHidden { get; init; }
}

/// <summary>
///     A test-only <see cref="GlobalQueryFilter" /> that does not override <see cref="IsIgnorable" />, so it keeps
///     the default bypassable behaviour — proving <c>ApplySpecs</c> still bypasses filters that opt in.
/// </summary>
internal sealed class TestOnlyIgnorableFilter : GlobalQueryFilter
{
    public override string FilterKey => nameof(TestOnlyIgnorableFilter);

    protected override IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder) =>
        modelBuilder.Model.GetEntityTypes().Where(t => typeof(ITestHiddenFlag).IsAssignableFrom(t.ClrType));

    protected override Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context) =>
        x => !((ITestHiddenFlag)x).IsHidden;
}

internal sealed class HiddenFlagDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<HiddenFlagEntity> Items => Set<HiddenFlagEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<HiddenFlagEntity>().HasKey(e => e.Id);
        new TestOnlyIgnorableFilter().Apply(modelBuilder, this);
    }
}

internal sealed class HiddenFlagSpec : Specification<HiddenFlagEntity>
{
    public HiddenFlagSpec(bool ignoreQueryFilters)
    {
        if (ignoreQueryFilters) IgnoreQueryFilters();
    }
}
