using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Covers the ordering rules of <c>EntityListSpecification</c> directly — specifically that a caller-chosen
///     <c>orderBy</c> always carries a unique tie-break. This has to be asserted at the specification level:
///     the HTTP fixtures run on EF InMemory, whose LINQ-to-objects sort is stable, so a paging walk over tied
///     values passes there even when the real database would repeat and drop rows across page boundaries.
///     Assertions run against a real Sqlite connection and check the generated <c>ORDER BY</c> clause plus
///     materialized row order — the ordering model itself (<c>Specification&lt;TEntity&gt;.OrderByClauses</c>)
///     is an internal implementation detail of DKNet.EfCore.Specifications, not something this project reaches into.
/// </summary>
public sealed class EntityListSpecificationTests : IAsyncLifetime
{
    #region Fields

    private SqliteConnection _connection = null!;
    private WidgetDbContext _context = null!;
    private IRepositorySpec _repository = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<WidgetDbContext>().UseSqlite(_connection).Options;
        _context = new WidgetDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new RepositorySpec<WidgetDbContext>(_context, (IServiceProvider?)null);
    }

    [Fact]
    public async Task CallerOrderBy_AppendsIdDescendingAsTieBreak()
    {
        // Name is not unique in general; without a tie-break, rows with equal names have no defined order and
        // paging over them is non-deterministic on a real database.
        var tied = new Guid("00000000-0000-0000-0000-000000000001");
        var newer = new Guid("00000000-0000-0000-0000-000000000002");
        _context.Widgets.AddRange(
            new WidgetEntity(tied, "same"),
            new WidgetEntity(newer, "same"));
        await _context.SaveChangesAsync();

        var spec = new EntityListSpecification<WidgetEntity, Guid, WidgetModel>(
            new ListQuery<WidgetEntity>(Filter: null, OrderBy: "Name", Descending: false));

        // Assert: ORDER BY is "Name" ASC then "Id" DESC — the appended tie-break must be Id, descending.
        var query = _repository.Query(spec);
        var sql = query.ToQueryString();
        var orderBy = sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];
        orderBy.ShouldContain("\"Name\"");
        orderBy.ShouldContain("\"Id\" DESC");
        orderBy.IndexOf("\"Name\"", StringComparison.Ordinal)
            .ShouldBeLessThan(orderBy.IndexOf("\"Id\"", StringComparison.Ordinal));

        // Assert: with names tied, the higher Id (newer) sorts first because the tie-break is descending.
        var results = await query.ToListAsync();
        results.Select(w => w.Id).ShouldBe([newer, tied]);
    }

    [Fact]
    public async Task CallerOrderByDescending_AppendsIdTieBreak()
    {
        var spec = new EntityListSpecification<WidgetEntity, Guid, WidgetModel>(
            new ListQuery<WidgetEntity>(Filter: null, OrderBy: "Name", Descending: true));

        var sql = _repository.Query(spec).ToQueryString();
        var orderBy = sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];
        orderBy.ShouldContain("\"Name\" DESC");
        orderBy.ShouldContain("\"Id\" DESC");
    }

    [Fact]
    public void CallerOrderById_DoesNotAppendASecondIdClause()
    {
        // Id is already unique — a second Id key would be dead weight in every ORDER BY.
        var spec = new EntityListSpecification<WidgetEntity, Guid, WidgetModel>(
            new ListQuery<WidgetEntity>(Filter: null, OrderBy: "Id", Descending: false));

        var sql = _repository.Query(spec).ToQueryString();
        var orderBy = sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];
        var idOccurrences = System.Text.RegularExpressions.Regex.Matches(orderBy, "\"Id\"").Count;
        idOccurrences.ShouldBe(1);
    }

    [Fact]
    public void NoCallerOrder_KeepsTheNewestFirstDefault()
    {
        var spec = new EntityListSpecification<WidgetEntity, Guid, WidgetModel>(
            new ListQuery<WidgetEntity>(Filter: null, OrderBy: null, Descending: false));

        // Non-audited entity: Id descending alone.
        var sql = _repository.Query(spec).ToQueryString();
        var orderBy = sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];
        orderBy.ShouldContain("\"Id\" DESC");
        var idOccurrences = System.Text.RegularExpressions.Regex.Matches(orderBy, "\"Id\"").Count;
        idOccurrences.ShouldBe(1);
    }

    #endregion
}
