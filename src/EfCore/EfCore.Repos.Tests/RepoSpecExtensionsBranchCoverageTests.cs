using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using DKNet.EfCore.Repos.Repositories;
using DKNet.EfCore.Specifications.Definitions;

namespace EfCore.Repos.Tests;

/// <summary>
///     Covers <see cref="RepoExtensions" />'s local-copy specification-application branches that
///     <see cref="RepoSpecExtensionsTests" />'s single ascending-order/no-includes/no-ignore-filters spec never
///     reaches: <c>IgnoreQueryFilters</c>, includes, descending-only ordering, mixed ascending+descending ordering,
///     and the "no ordering" guard in <c>EnsureSpecHasOrdering</c>.
/// </summary>
public class RepoSpecExtensionsBranchCoverageTests : IAsyncLifetime
{
    #region Fields

    private RepoSpecExtensionsTests.TestDbContext? _dbContext;
    private IReadRepository<RepoSpecExtensionsTests.TestEntity>? _repository;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.CloseConnectionAsync();
            await _dbContext.DisposeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<RepoSpecExtensionsTests.TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new RepoSpecExtensionsTests.TestDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        _repository = new ReadRepository<RepoSpecExtensionsTests.TestEntity>(_dbContext);

        _dbContext.TestEntities.AddRange(
            new RepoSpecExtensionsTests.TestEntity { Id = 1, Name = "Bravo", IsActive = true },
            new RepoSpecExtensionsTests.TestEntity { Id = 2, Name = "Alpha", IsActive = true },
            new RepoSpecExtensionsTests.TestEntity { Id = 3, Name = "Charlie", IsActive = true });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public void QuerySpecs_IgnoreQueryFiltersSpec_AppliesWithoutThrowing()
    {
        // No global query filter is registered on this DbContext, so IgnorableFilterKeys is empty and the
        // IgnoreQueryFilters(keys) call is skipped — this proves the IsIgnoreQueryFilters branch itself is safe
        // to take even when there's nothing to ignore.
        var spec = new IgnoreFiltersSpec();

        var query = _repository!.QuerySpecs(spec);

        query.Count().ShouldBe(3);
    }

    [Fact]
    public void QuerySpecs_SpecWithIncludeBuilder_AppliesIncludeChainWithoutThrowing()
    {
        var spec = new IncludeBuilderSpec();

        var query = _repository!.QuerySpecs(spec);

        query.ShouldNotBeNull();
        query.Count().ShouldBe(3);
    }

    [Fact]
    public void QuerySpecs_DescendingOnlySpec_OrdersDescending()
    {
        var spec = new NameDescendingSpec();

        var result = _repository!.QuerySpecs(spec).ToList();

        result.Select(e => e.Name).ShouldBe(["Charlie", "Bravo", "Alpha"]);
    }

    [Fact]
    public void QuerySpecs_AscendingThenDescendingSpec_AppliesBothOrderings()
    {
        // IsActive ascending (all equal here) then Name descending as tie-break — exercises the
        // "ordered != null" ThenByDescending path, distinct from the descending-only path above.
        var spec = new ActiveThenNameDescendingSpec();

        var result = _repository!.QuerySpecs(spec).ToList();

        result.Select(e => e.Name).ShouldBe(["Charlie", "Bravo", "Alpha"]);
    }

    [Fact]
    public void SpecsToPageEnumerable_SpecWithNoOrdering_ThrowsNotSupportedException()
    {
        var spec = new NoOrderingSpec();

        Should.Throw<NotSupportedException>(() => _repository!.SpecsToPageEnumerable(spec));
    }

    #endregion

    private sealed class IgnoreFiltersSpec : Specification<RepoSpecExtensionsTests.TestEntity>
    {
        public IgnoreFiltersSpec()
        {
            IgnoreQueryFilters();
            AddOrderBy(e => e.Id);
        }
    }

    private sealed class IncludeBuilderSpec : Specification<RepoSpecExtensionsTests.TestEntity>
    {
        // An identity include-chain — this entity has no navigations, so the point is exercising the
        // IncludeBuilders aggregation branch itself, not any particular eager-loaded data.
        public IncludeBuilderSpec()
        {
            AddInclude(q => q);
            AddOrderBy(e => e.Id);
        }
    }

    private sealed class NameDescendingSpec : Specification<RepoSpecExtensionsTests.TestEntity>
    {
        public NameDescendingSpec() => AddOrderByDescending(e => e.Name);
    }

    private sealed class ActiveThenNameDescendingSpec : Specification<RepoSpecExtensionsTests.TestEntity>
    {
        public ActiveThenNameDescendingSpec()
        {
            AddOrderBy(e => e.IsActive);
            AddOrderByDescending(e => e.Name);
        }
    }

    private sealed class NoOrderingSpec : Specification<RepoSpecExtensionsTests.TestEntity>;
}
