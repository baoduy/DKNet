using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Covers the default recent-activity window's query shape: because the default window's lower bound is
///     derived from <see cref="DateTimeOffset.UtcNow" />, it is a distinct value on every request, so — exactly
///     like the free-text <c>search</c> predicate <see cref="ListQuerySearchParameterizationTests" /> covers —
///     the bound must bind as a SQL parameter rather than an inlined literal, or every request seeds a fresh
///     query plan. Assertions run against a real Sqlite connection, the same pattern
///     <see cref="EntityListSpecificationTests" /> uses for ordering.
/// </summary>
public sealed class ListQueryActivityWindowParameterizationTests : IAsyncLifetime
{
    #region Fields

    private SqliteConnection _connection = null!;
    private WidgetDbContext _context = null!;

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
    }

    /// <summary>Strips <c>ToQueryString()</c>'s <c>.param set @X value</c> debug preamble (see the search test).</summary>
    private static string CommandTextOnly(string queryString)
    {
        var index = queryString.IndexOf("SELECT", StringComparison.Ordinal);
        return index < 0 ? queryString : queryString[index..];
    }

    private string CommandTextFor(DateTimeOffset from, DateTimeOffset to)
    {
        ListQuery.TryValidate<OrderEntity, OrderModel>(
            new ListQueryRequest { FromDate = from, ToDate = to },
            new ListQueryOptions(),
            out var query,
            out var error);
        error.ShouldBeNull();
        query.ShouldNotBeNull();
        query.Filter.ShouldNotBeNull();

        return CommandTextOnly(_context.Orders.Where(query.Filter).ToQueryString());
    }

    [Fact]
    public void Window_BindsBoundsAsParameters_NotLiterals()
    {
        var commandText = CommandTextFor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

        commandText.ShouldContain("@");
        commandText.ShouldNotContain("2026-01-01");
        commandText.ShouldNotContain("2026-06-01");
    }

    [Fact]
    public void Window_TwoDifferentBoundPairs_ProduceIdenticalCommandText()
    {
        // Same TEntity: both calls must reuse the cached template rather than each re-parsing/re-building its
        // own tree, so the generated command text — not just the bound values — is identical.
        var sql1 = CommandTextFor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var sql2 = CommandTextFor(
            new DateTimeOffset(2020, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2021, 9, 1, 0, 0, 0, TimeSpan.Zero));

        sql1.ShouldBe(sql2);
    }

    [Fact]
    public async Task Window_MatchesOnlyRowsInsideTheBounds()
    {
        var inside = new OrderEntity(Guid.NewGuid(), "inside", "Open");
        inside.Seed(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        var outside = new OrderEntity(Guid.NewGuid(), "outside", "Open");
        outside.Seed(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _context.Orders.AddRange(inside, outside);
        await _context.SaveChangesAsync();

        ListQuery.TryValidate<OrderEntity, OrderModel>(
            new ListQueryRequest
            {
                FromDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ToDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new ListQueryOptions(),
            out var query,
            out _);

        var results = await _context.Orders.Where(query!.Filter!).ToListAsync();

        results.ShouldHaveSingleItem();
        results[0].Reference.ShouldBe("inside");
    }

    [Fact]
    public async Task Window_NullUpdatedOn_MatchesOnCreatedOnAlone()
    {
        // A record never updated must be matched on CreatedOn alone and never dropped for a null UpdatedOn (R4).
        var order = new OrderEntity(Guid.NewGuid(), "never-updated", "Open");
        order.Seed(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        ListQuery.TryValidate<OrderEntity, OrderModel>(
            new ListQueryRequest
            {
                FromDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ToDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new ListQueryOptions(),
            out var query,
            out _);

        var results = await _context.Orders.Where(query!.Filter!).ToListAsync();

        results.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Window_UpdatedOnInRangeWhileCreatedOnIsNot_StillMatches()
    {
        var order = new OrderEntity(Guid.NewGuid(), "edited-into-range", "Open");
        order.Seed(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        ListQuery.TryValidate<OrderEntity, OrderModel>(
            new ListQueryRequest
            {
                FromDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ToDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new ListQueryOptions(),
            out var query,
            out _);

        var results = await _context.Orders.Where(query!.Filter!).ToListAsync();

        results.ShouldHaveSingleItem();
    }

    [Fact]
    public void Window_EntityWithoutAuditedProperties_BuildsNoFilterEvenWithBoundsSupplied()
    {
        // R6: bounds are ignored (never a 400) for a type with no audit timestamps — TryValidate must succeed
        // and must not fabricate a filter that references a nonexistent column.
        var succeeded = ListQuery.TryValidate<WidgetEntity, WidgetModel>(
            new ListQueryRequest
            {
                FromDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ToDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new ListQueryOptions(),
            out var query,
            out var error);

        succeeded.ShouldBeTrue();
        error.ShouldBeNull();
        query.ShouldNotBeNull();
        query.Filter.ShouldBeNull();
    }

    #endregion
}
