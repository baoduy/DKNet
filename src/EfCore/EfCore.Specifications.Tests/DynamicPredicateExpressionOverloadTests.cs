// <copyright file="DynamicPredicateExpressionOverloadTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Specifications.Dynamics;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Covers the four overloads of <see cref="DynamicPredicateExtensions" /> that operate on
///     <see cref="Expression{TDelegate}" /> (rather than LinqKit's <see cref="ExpressionStarter{T}" />)
///     plus the raw dynamic-LINQ string overloads on both predicate kinds. These paths were not
///     exercised by the existing test suite, so the branches in <c>BuildDynamicExpression</c> and the
///     <c>ValidateExpression</c> + parse pipeline ran without coverage.
/// </summary>
public class DynamicPredicateExpressionOverloadTests(TestDbFixture fixture) : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context = fixture.Db!;

    #endregion

    #region Expression<Func<T,bool>> property-name overloads

    [Fact]
    public void DynamicAnd_OnExpressionFunc_WithValidProperty_CombinesWithAnd()
    {
        Expression<Func<Product, bool>> baseExpr = p => p.IsActive;

        var combined = baseExpr.DynamicAnd("Price", Ops.GreaterThan, 100m);

        // sanity check: predicate is usable in EF query
        var sql = _context.Products.AsExpandable().Where(combined).ToQueryString();
        sql.ShouldContain("IsActive");
        sql.ShouldContain("Price");
    }

    [Fact]
    public void DynamicAnd_OnExpressionFunc_WithInvalidPropertyName_ReturnsOriginalPredicate()
    {
        Expression<Func<Product, bool>> baseExpr = p => p.IsActive;

        var combined = baseExpr.DynamicAnd("does.not.exist", Ops.Equal, "x");

        // identical instance — the BuildDynamicExpression returned null, so the original is returned unchanged
        combined.ShouldBeSameAs(baseExpr);
    }

    [Fact]
    public void DynamicAnd_OnExpressionFunc_WithDangerousPropertyName_ReturnsOriginalPredicate()
    {
        Expression<Func<Product, bool>> baseExpr = p => p.IsActive;

        // semicolon will fail IsValidPropertyName → BuildDynamicExpression returns null
        var combined = baseExpr.DynamicAnd("Name; DROP TABLE", Ops.Equal, "x");

        combined.ShouldBeSameAs(baseExpr);
    }

    [Fact]
    public void DynamicOr_OnExpressionFunc_WithValidProperty_CombinesWithOr()
    {
        Expression<Func<Product, bool>> baseExpr = p => p.IsActive;

        var combined = baseExpr.DynamicOr("Price", Ops.LessThan, 50m);

        var sql = _context.Products.AsExpandable().Where(combined).ToQueryString();
        sql.ShouldContain("IsActive");
        sql.ShouldContain("Price");
    }

    [Fact]
    public void DynamicOr_OnExpressionFunc_WithInvalidPropertyName_ReturnsOriginalPredicate()
    {
        Expression<Func<Product, bool>> baseExpr = p => p.IsActive;

        var combined = baseExpr.DynamicOr("nope", Ops.Equal, "x");

        combined.ShouldBeSameAs(baseExpr);
    }

    #endregion

    #region Expression<Func<T,bool>> raw expression overloads

    [Fact]
    public void DynamicAnd_OnExpressionFunc_WithRawExpression_ParsesAndAndsCorrectly()
    {
        Expression<Func<Product, bool>> baseExpr = p => p.IsActive;

        var combined = baseExpr.DynamicAnd("Price > @0", 100m);

        var sql = _context.Products.AsExpandable().Where(combined).ToQueryString();
        sql.ShouldContain("Price");
        sql.ShouldContain("IsActive");
    }

    [Fact]
    public void DynamicOr_OnExpressionFunc_WithRawExpression_ParsesAndOrsCorrectly()
    {
        Expression<Func<Product, bool>> baseExpr = p => p.IsActive;

        var combined = baseExpr.DynamicOr("Price < @0", 50m);

        var sql = _context.Products.AsExpandable().Where(combined).ToQueryString();
        sql.ShouldContain("Price");
        sql.ShouldContain("IsActive");
    }

    [Fact]
    public void DynamicAnd_OnExpressionFunc_WithDangerousRawExpression_Throws()
    {
        Expression<Func<Product, bool>> baseExpr = p => p.IsActive;

        Should.Throw<ArgumentException>(() => baseExpr.DynamicAnd("System.IO.File.Delete(@0)", new object?[] { "x" }));
    }

    [Fact]
    public void DynamicOr_OnExpressionFunc_WithDangerousRawExpression_Throws()
    {
        Expression<Func<Product, bool>> baseExpr = p => p.IsActive;

        Should.Throw<ArgumentException>(() => baseExpr.DynamicOr("typeof(Object).ToString()", new object?[] { "x" }));
    }

    #endregion

    #region ExpressionStarter<T> raw expression overloads

    [Fact]
    public void DynamicAnd_OnExpressionStarter_WithRawExpression_ParsesAndAndsCorrectly()
    {
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        var combined = predicate.DynamicAnd("Price > @0", 100m);

        var sql = _context.Products.AsExpandable().Where(combined).ToQueryString();
        sql.ShouldContain("Price");
        sql.ShouldContain("IsActive");
    }

    [Fact]
    public void DynamicOr_OnExpressionStarter_WithRawExpression_ParsesAndOrsCorrectly()
    {
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        var combined = predicate.DynamicOr("Price < @0", 50m);

        var sql = _context.Products.AsExpandable().Where(combined).ToQueryString();
        sql.ShouldContain("Price");
        sql.ShouldContain("IsActive");
    }

    [Fact]
    public void DynamicAnd_OnExpressionStarter_WithDangerousRawExpression_Throws()
    {
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        Should.Throw<ArgumentException>(() => predicate.DynamicAnd("Activator.CreateInstance(@0)", new object?[] { "x" }));
    }

    [Fact]
    public void DynamicOr_OnExpressionStarter_WithDangerousRawExpression_Throws()
    {
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        Should.Throw<ArgumentException>(() => predicate.DynamicOr("Process.Start(@0)", new object?[] { "x" }));
    }

    #endregion
}
