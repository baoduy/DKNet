// <copyright file="DynamicPredicateNullSemanticsTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Specifications.Dynamics;
using EfCore.Specifications.Tests.Fixtures;
using EfCore.Specifications.Tests.TestEntities;
using Xunit.Abstractions;

namespace EfCore.Specifications.Tests.Dynamics;

/// <summary>
///     Pins how the string and negation operations behave against real PostgreSQL, on both a nullable and a
///     non-nullable column, and records the SQL each one produces.
/// </summary>
/// <remarks>
///     <para>
///         These need a real database because SQL uses three-valued logic where C# does not: <c>NULL LIKE
///         '%x%'</c> and <c>NULL &lt;&gt; 'x'</c> both evaluate to <c>NULL</c> rather than to
///         <see langword="true" />. The naive expectation is that a row whose column is null therefore falls
///         out of a negated condition it plainly satisfies. <b>It does not</b> — EF Core rewrites these into
///         null-aware SQL before they reach the provider, and the tests below are what established that.
///     </para>
///     <para>
///         What the clauses do carry is an explicit null check, so the same expression means the same thing
///         when it is evaluated as ordinary LINQ instead of translated — see the compiled-predicate cases at
///         the end. The non-nullable cases exist to prove that guard changes no result on a column that can
///         never be null, which is the majority of every real query.
///     </para>
///     <para>
///         The fixture seeds ten merchants, exactly one of which carries a <c>TradingName</c>.
///     </para>
/// </remarks>
public sealed class DynamicPredicateNullSemanticsTests(MerchantPostgresFixture fixture, ITestOutputHelper output)
    : IClassFixture<MerchantPostgresFixture>
{
    #region Methods

    /// <summary>Runs a filter against PostgreSQL, recording the SQL it generated, and returns the matched ids.</summary>
    private async Task<int[]> QueryIdsAsync(string property, Ops operation, object? value)
    {
        var predicate = PredicateBuilder.New<Merchant>(true).DynamicAnd(property, operation, value);
        var query = fixture.Db.Merchants.AsExpandable().Where(predicate);

        output.WriteLine($"--- {property} {operation} '{value}'");
        output.WriteLine(query.ToQueryString());

        var merchants = await query.ToListAsync();
        return [.. merchants.Select(m => m.Id).Order()];
    }

    [Fact]
    public async Task DynamicAnd_ContainsOnNullableColumn_MatchesOnlyRowsWithAValue()
    {
        var predicate = PredicateBuilder.New<Merchant>(true)
            .DynamicAnd("TradingName", Ops.Contains, "Acme");

        var merchants = await fixture.Db.Merchants.AsExpandable().Where(predicate).ToListAsync();

        merchants.Select(m => m.Id).ShouldBe([1]);
    }

    [Fact]
    public async Task DynamicAnd_NotContainsOnNullableColumn_IncludesRowsWhereItIsNull()
    {
        // A merchant with no trading name does not contain "Acme", so it belongs in the result. Under SQL's
        // three-valued logic NOT (NULL LIKE '%Acme%') is NULL, which silently drops all nine of them.
        var predicate = PredicateBuilder.New<Merchant>(true)
            .DynamicAnd("TradingName", Ops.NotContains, "Acme");

        var merchants = await fixture.Db.Merchants.AsExpandable().Where(predicate).ToListAsync();

        merchants.Count.ShouldBe(9);
        merchants.ShouldAllBe(m => m.TradingName == null);
    }

    [Fact]
    public async Task DynamicAnd_NotEqualOnNullableColumn_IncludesRowsWhereItIsNull()
    {
        // Same defect through a different operation: NULL <> 'Acme Trading' is NULL, not true.
        var predicate = PredicateBuilder.New<Merchant>(true)
            .DynamicAnd("TradingName", Ops.NotEqual, "Acme Trading");

        var merchants = await fixture.Db.Merchants.AsExpandable().Where(predicate).ToListAsync();

        merchants.Count.ShouldBe(9);
    }

    [Fact]
    public async Task DynamicAnd_StartsWithOnNullableColumn_MatchesOnlyRowsWithAValue()
    {
        var predicate = PredicateBuilder.New<Merchant>(true)
            .DynamicAnd("TradingName", Ops.StartsWith, "Acme");

        var merchants = await fixture.Db.Merchants.AsExpandable().Where(predicate).ToListAsync();

        merchants.Select(m => m.Id).ShouldBe([1]);
    }

    // --- Non-nullable columns: the null check must not alter a single result -------------------------------
    // Name and Country can never be null, so every one of these would return the same rows with or without
    // the guard. They are the regression net for the change: the guard was added for nullable columns and the
    // compiled-predicate path, and it must be inert everywhere else.

    [Fact]
    public async Task DynamicAnd_ContainsOnNonNullableColumn_MatchesTheSubstring()
    {
        // "Borneo Trading" only; "Kuala Traders" contains "Trader", not "Trading".
        (await QueryIdsAsync("Name", Ops.Contains, "Trading")).ShouldBe([2]);
    }

    [Fact]
    public async Task DynamicAnd_NotContainsOnNonNullableColumn_MatchesEveryOtherRow()
    {
        // The exact complement of the case above — no nulls exist, so this is plain negation.
        (await QueryIdsAsync("Name", Ops.NotContains, "Trading")).ShouldBe([1, 3, 4, 5, 6, 7, 8, 9, 10]);
    }

    [Fact]
    public async Task DynamicAnd_StartsWithOnNonNullableColumn_MatchesThePrefix()
    {
        // Sumatra Spice, Sabah Timber, Sentosa Imports. PostgreSQL LIKE is case-sensitive, which is the point
        // of leaving case to the collation rather than lowercasing both sides.
        (await QueryIdsAsync("Name", Ops.StartsWith, "S")).ShouldBe([3, 8, 10]);
    }

    [Fact]
    public async Task DynamicAnd_EndsWithOnNonNullableColumn_MatchesTheSuffix()
    {
        (await QueryIdsAsync("Name", Ops.EndsWith, "s")).ShouldBe([1, 4, 5, 6, 7, 9, 10]);
    }

    [Fact]
    public async Task DynamicAnd_ContainsOnASecondNonNullableColumn_MatchesAcrossGroups()
    {
        // Indonesia and Malaysia both contain "ia"; Singapore does not.
        (await QueryIdsAsync("Country", Ops.Contains, "ia")).ShouldBe([1, 2, 3, 4, 5, 6, 7, 8]);
    }

    // --- Degenerate values, where a null check could plausibly change the answer ---------------------------

    [Fact]
    public async Task DynamicAnd_ContainsEmptyStringOnNonNullableColumn_MatchesEveryRow()
    {
        // LIKE '%%' matches anything, and the guard cannot exclude a column that is never null.
        (await QueryIdsAsync("Name", Ops.Contains, string.Empty))
            .ShouldBe([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    }

    [Fact]
    public async Task DynamicAnd_ContainsEmptyStringOnNullableColumn_MatchesOnlyTheNonNullRow()
    {
        // Here the guard is what decides the answer: LIKE '%%' would match every row were the column not
        // null, so only the one merchant that actually has a trading name qualifies.
        (await QueryIdsAsync("TradingName", Ops.Contains, string.Empty)).ShouldBe([1]);
    }

    [Fact]
    public async Task DynamicAnd_NotContainsEmptyStringOnNullableColumn_MatchesOnlyTheNullRows()
    {
        // The complement of the case above: nothing "does not contain" the empty string except the rows that
        // hold nothing at all.
        (await QueryIdsAsync("TradingName", Ops.NotContains, string.Empty))
            .ShouldBe([2, 3, 4, 5, 6, 7, 8, 9, 10]);
    }

    // --- The same predicates evaluated as ordinary LINQ rather than translated to SQL --------------------
    // A relational provider never evaluates these in C#: EF Core rewrites them into null-aware SQL, which is
    // why every database-backed case above passed even before the clauses carried a null check. Anything that
    // evaluates the expression directly gets no such rewriting — the InMemory provider, client-side evaluation
    // of a projection, or a caller compiling a predicate obtained from TryBuildPredicate. Without the guard a
    // null dereferences and throws; these assert the compiled form now agrees with the SQL above.

    [Fact]
    public void DynamicAnd_ContainsCompiled_ExcludesANullValueWithoutThrowing()
    {
        var predicate = PredicateBuilder.New<Merchant>(true)
            .DynamicAnd("TradingName", Ops.Contains, "Acme")
            .Compile();

        predicate(new Merchant { TradingName = null }).ShouldBeFalse();
        predicate(new Merchant { TradingName = "Acme Trading" }).ShouldBeTrue();
    }

    [Fact]
    public void DynamicAnd_NotContainsCompiled_IncludesANullValue()
    {
        // Matches what PostgreSQL returns for the same filter: a row holding nothing does not contain "Acme".
        var predicate = PredicateBuilder.New<Merchant>(true)
            .DynamicAnd("TradingName", Ops.NotContains, "Acme")
            .Compile();

        predicate(new Merchant { TradingName = null }).ShouldBeTrue();
        predicate(new Merchant { TradingName = "Acme Trading" }).ShouldBeFalse();
    }

    [Fact]
    public void DynamicAnd_StartsWithCompiled_ExcludesANullValueWithoutThrowing()
    {
        var predicate = PredicateBuilder.New<Merchant>(true)
            .DynamicAnd("TradingName", Ops.StartsWith, "Acme")
            .Compile();

        predicate(new Merchant { TradingName = null }).ShouldBeFalse();
        predicate(new Merchant { TradingName = "Acme Trading" }).ShouldBeTrue();
    }

    [Fact]
    public void DynamicAnd_EndsWithCompiled_ExcludesANullValueWithoutThrowing()
    {
        var predicate = PredicateBuilder.New<Merchant>(true)
            .DynamicAnd("TradingName", Ops.EndsWith, "Trading")
            .Compile();

        predicate(new Merchant { TradingName = null }).ShouldBeFalse();
        predicate(new Merchant { TradingName = "Acme Trading" }).ShouldBeTrue();
    }

    #endregion
}
