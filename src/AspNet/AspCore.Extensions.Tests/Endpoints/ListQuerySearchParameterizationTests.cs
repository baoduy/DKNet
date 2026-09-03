using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Covers the free-text <c>search</c> predicate's query shape (performance finding P5): the search value
///     must bind as a SQL parameter, not a literal, and — because the predicate template is parsed once per
///     <c>(TModel, TEntity)</c> pair and reused by swapping a boxed placeholder — two different search values
///     must produce byte-identical command text. Assertions run against a real Sqlite connection, the same
///     pattern <see cref="EntityListSpecificationTests" /> uses for ordering.
/// </summary>
public sealed class ListQuerySearchParameterizationTests : IAsyncLifetime
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

    /// <summary>
    ///     Strips <c>ToQueryString()</c>'s <c>.param set @X value</c> debug preamble, leaving only the actual
    ///     SQL command text sent to the database (and cached as a query plan) — the preamble echoes the bound
    ///     value back for readability even when the command text itself carries a placeholder.
    /// </summary>
    private static string CommandTextOnly(string queryString)
    {
        var index = queryString.IndexOf("SELECT", StringComparison.Ordinal);
        return index < 0 ? queryString : queryString[index..];
    }

    private string CommandTextFor(string search)
    {
        ListQuery.TryValidate<WidgetEntity, WidgetModel>(
            new ListQueryRequest { Search = search }, out var query, out var error);
        error.ShouldBeNull();
        query.ShouldNotBeNull();
        query.Filter.ShouldNotBeNull();

        return CommandTextOnly(_context.Widgets.Where(query.Filter).ToQueryString());
    }

    [Fact]
    public void Search_BindsTheValueAsAParameter_NotALiteral()
    {
        var commandText = CommandTextFor("widget");

        commandText.ShouldContain("@");
        commandText.ShouldNotContain("'widget'");
    }

    [Fact]
    public void Search_TwoDifferentValues_ProduceIdenticalCommandText()
    {
        // Same (TModel, TEntity) pair: both calls must reuse the cached template rather than each re-parsing
        // its own clause, so the generated command text — not just the bound value — is identical.
        var sql1 = CommandTextFor("widget");
        var sql2 = CommandTextFor("gadget");

        sql1.ShouldBe(sql2);
    }

    [Fact]
    public async Task Search_MatchesOnlyRowsContainingTheGivenValue()
    {
        _context.Widgets.AddRange(
            new WidgetEntity(Guid.NewGuid(), "red widget"),
            new WidgetEntity(Guid.NewGuid(), "blue gadget"));
        await _context.SaveChangesAsync();

        ListQuery.TryValidate<WidgetEntity, WidgetModel>(
            new ListQueryRequest { Search = "widget" }, out var query, out _);

        var results = await _context.Widgets.Where(query!.Filter!).ToListAsync();

        results.ShouldHaveSingleItem();
        results[0].Name.ShouldBe("red widget");
    }

    #endregion
}
