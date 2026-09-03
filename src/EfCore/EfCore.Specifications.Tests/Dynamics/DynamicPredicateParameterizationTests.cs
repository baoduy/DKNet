// <copyright file="DynamicPredicateParameterizationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Specifications.Dynamics;

namespace EfCore.Specifications.Tests.Dynamics;

/// <summary>
///     Verifies that the typed <c>DynamicAnd</c>/<c>DynamicOr</c> path binds filter values as SQL
///     parameters rather than inlining them as literals, so a list endpoint reuses one query plan
///     across every distinct filter value instead of producing one plan per value.
/// </summary>
public class DynamicPredicateParameterizationTests(TestDbFixture fixture) : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context = fixture.Db!;

    #endregion

    #region Methods

    /// <summary>
    ///     Strips <c>ToQueryString()</c>'s <c>.param set @X value</c> debug preamble, leaving only the
    ///     actual SQL command text that gets sent to the database (and cached as a query plan).
    /// </summary>
    private static string CommandTextOnly(string queryString)
    {
        var index = queryString.IndexOf("SELECT", StringComparison.Ordinal);
        return index < 0 ? queryString : queryString[index..];
    }

    private string CommandTextFor(string propertyName, Ops operation, object? value) =>
        CommandTextOnly(_context.Products
            .AsExpandable()
            .Where(PredicateBuilder.New<Product>(true).DynamicAnd(propertyName, operation, value))
            .ToQueryString());

    /// <summary>
    ///     A numeric comparison binds its value as a parameter: the command text carries a placeholder,
    ///     not the value's digits.
    /// </summary>
    [Fact]
    public void DynamicAnd_NumericComparison_BindsValueAsParameter_NotLiteral()
    {
        // Act
        var commandText = CommandTextFor("Price", Ops.GreaterThan, 424242m);

        // Assert
        commandText.ShouldContain("@");
        commandText.ShouldNotContain("424242");
    }

    /// <summary>
    ///     Two predicates differing only in the filter value produce byte-identical command text — the
    ///     property that makes the plan cache work.
    /// </summary>
    [Fact]
    public void DynamicAnd_DifferentValues_ProduceIdenticalCommandText()
    {
        // Act
        var sql1 = CommandTextFor("Price", Ops.GreaterThan, 100m);
        var sql2 = CommandTextFor("Price", Ops.GreaterThan, 250m);

        // Assert
        sql1.ShouldBe(sql2);
    }

    /// <summary>
    ///     The string operations build a two-part clause (<c>prop != null &amp;&amp; prop.Contains(@0)</c>),
    ///     so they are worth asserting separately from the scalar comparisons.
    /// </summary>
    [Fact]
    public void DynamicAnd_Contains_DifferentValues_ProduceIdenticalCommandText()
    {
        // Act
        var sql1 = CommandTextFor("Name", Ops.Contains, "widget");
        var sql2 = CommandTextFor("Name", Ops.Contains, "gadget");

        // Assert
        sql1.ShouldBe(sql2);
        sql1.ShouldNotContain("widget");
    }

    /// <summary>
    ///     A multi-condition predicate — the realistic list-endpoint shape — parameterizes every condition,
    ///     not just the first.
    /// </summary>
    [Fact]
    public void DynamicAnd_MultipleConditions_DifferentValues_ProduceIdenticalCommandText()
    {
        // Act
        var sql1 = CommandTextOnly(_context.Products
            .AsExpandable()
            .Where(PredicateBuilder.New<Product>(true)
                .DynamicAnd("Price", Ops.GreaterThan, 10m)
                .DynamicAnd("StockQuantity", Ops.LessThanOrEqual, 5)
                .DynamicAnd("Name", Ops.StartsWith, "aaa"))
            .ToQueryString());
        var sql2 = CommandTextOnly(_context.Products
            .AsExpandable()
            .Where(PredicateBuilder.New<Product>(true)
                .DynamicAnd("Price", Ops.GreaterThan, 20m)
                .DynamicAnd("StockQuantity", Ops.LessThanOrEqual, 6)
                .DynamicAnd("Name", Ops.StartsWith, "bbb"))
            .ToQueryString());

        // Assert
        sql1.ShouldBe(sql2);
    }

    /// <summary>
    ///     Parameterization must not change which rows come back. The same filter is asserted against
    ///     materialized results, not just SQL text.
    /// </summary>
    [Fact]
    public async Task DynamicAnd_Parameterized_ReturnsSameRowsAsTypedPredicate()
    {
        // Arrange
        var dynamicPredicate = PredicateBuilder.New<Product>(true).DynamicAnd("Price", Ops.GreaterThan, 100m);

        // Act
        var viaDynamic = await _context.Products.AsExpandable().Where(dynamicPredicate)
            .Select(p => p.Id).ToListAsync();
        var viaTyped = await _context.Products.Where(p => p.Price > 100m)
            .Select(p => p.Id).ToListAsync();

        // Assert
        viaDynamic.ShouldBe(viaTyped, ignoreOrder: true);
    }

    #endregion
}
