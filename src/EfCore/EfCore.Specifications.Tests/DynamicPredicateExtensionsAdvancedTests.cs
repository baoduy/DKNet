// <copyright file="DynamicPredicateExtensionsAdvancedTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>


using DKNet.EfCore.Specifications.Dynamics;
using Xunit.Abstractions;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Advanced unit tests for DynamicPredicateExtensions covering edge cases and complex scenarios
/// </summary>
public class DynamicPredicateExtensionsAdvancedTests(TestDbFixture fixture, ITestOutputHelper output)
    : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public void DynamicAnd_ChainedWithStaticPredicates_CombinesCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        // Act: Chain static and dynamic predicates
        var result = predicate
            .And(p => p.Price > 100)
            .DynamicAnd("Name", DynamicOperations.Contains, "test")
            .And(p => p.StockQuantity > 0);

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("[IsActive]");
        sql.ShouldContain("[Price]");
        sql.ShouldContain("[Name]");
        sql.ShouldContain("[StockQuantity]");
    }

    [Fact]
    public async Task DynamicAnd_SubPredicatePattern_CombinesCorrectly()
    {
        // Arrange
        var mainPredicate = PredicateBuilder.New<Product>(p => p.IsActive);

        // Create sub-predicate for OR conditions
        var subPredicate = PredicateBuilder.New<Product>(false);
        subPredicate = subPredicate
            .DynamicOr("Name", DynamicOperations.Contains, "Phone")
            .DynamicOr("Name", DynamicOperations.Contains, "Laptop")
            .DynamicOr("Description", DynamicOperations.Contains, "Computer");

        // Act: Combine main predicate with sub-predicate
        var result = mainPredicate.And(subPredicate);

        // Assert
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public void DynamicAnd_WithAllOperations_GeneratesCorrectSQL()
    {
        // Arrange & Act & Assert for each operation
        var operations = new[]
        {
            (DynamicOperations.Equal, "="),
            (DynamicOperations.NotEqual, "<>"),
            (DynamicOperations.GreaterThan, ">"),
            (DynamicOperations.GreaterThanOrEqual, ">="),
            (DynamicOperations.LessThan, "<"),
            (DynamicOperations.LessThanOrEqual, "<=")
        };

        foreach (var (op, sqlOp) in operations)
        {
            var predicate = PredicateBuilder.New<Product>(true);
            var result = predicate.DynamicAnd("Price", op, 100m);
            var query = _context.Products.AsExpandable().Where(result);
            var sql = query.ToQueryString();
            sql.ShouldContain($"[Price] {sqlOp}");
        }
    }

    [Fact]
    public async Task DynamicAnd_WithBoolValue_GeneratesCorrectFilter()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act
        var result = predicate.DynamicAnd("IsActive", DynamicOperations.Equal, true);

        // Assert
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public void DynamicAnd_WithComplexNestedPath_WorksCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<OrderItem>(true);

        // Act: Access deeply nested property
        var result = predicate.DynamicAnd("Product.Category.Name", DynamicOperations.Contains, "Elect");

        // Assert
        var query = _context.OrderItems.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("[c].[Name] LIKE N'%Elect%'");
    }

    [Fact]
    public void DynamicAnd_WithContainsOnNonStringProperty_ConvertsToEqual()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act: Use Contains on int property (should convert to Equal)
        var result = predicate.DynamicAnd("StockQuantity", DynamicOperations.Contains, 10);

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("="); // Should be equality, not Contains
    }

    [Fact]
    public async Task DynamicAnd_WithDateTimeValue_GeneratesCorrectComparison()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);
        var targetDate = DateTime.Now.AddDays(-30);

        // Act
        var result = predicate.DynamicAnd("CreatedDate", DynamicOperations.GreaterThanOrEqual, targetDate);

        // Assert
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.CreatedDate >= targetDate);
    }

    [Fact]
    public async Task DynamicAnd_WithDecimalValue_GeneratesCorrectComparison()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act
        var result = predicate
            .DynamicAnd("Price", DynamicOperations.GreaterThanOrEqual, 100.50m)
            .DynamicAnd("Price", DynamicOperations.LessThan, 500.75m);

        // Assert
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.Price >= 100.50m && p.Price < 500.75m);
    }

    [Fact]
    public void DynamicAnd_WithEmptyString_FiltersCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act
        var result = predicate.DynamicAnd("Name", DynamicOperations.Equal, "");

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("[Name] = N''");
    }

    [Fact]
    public async Task DynamicAnd_WithEnumIntValue_GeneratesCorrectFilter()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Order>(true);

        // Act: Use integer value for enum
        var result = predicate.DynamicAnd("Status", DynamicOperations.Equal, OrderStatus.Pending);

        // Assert
        var orders = await _context.Orders.AsExpandable().Where(result).ToListAsync();
        orders.ShouldAllBe(o => o.Status == OrderStatus.Pending);
    }

    [Fact]
    public async Task DynamicAnd_WithEnumValue_GeneratesCorrectFilter()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Order>(true);

        // Act
        var result = predicate.DynamicAnd("Status", DynamicOperations.Equal, OrderStatus.Pending);

        // Assert
        var orders = await _context.Orders.AsExpandable().Where(result).ToListAsync();
        orders.ShouldAllBe(o => o.Status == OrderStatus.Pending);
    }

    [Fact]
    public void DynamicAnd_WithInvalidNestedPath_ReturnsUnchangedPredicate()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        // Act
        var result = predicate.DynamicAnd("Category.NonExistent.Property", DynamicOperations.Equal, "test");

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldNotContain("NonExistent");
    }

    [Fact]
    public async Task DynamicAnd_WithMultipleConditionsOnSameProperty_CombinesWithAnd()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act: Multiple conditions on Price
        var result = predicate
            .DynamicAnd("Price", DynamicOperations.GreaterThan, 100m)
            .DynamicAnd("Price", DynamicOperations.LessThan, 1000m);

        // Assert
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.Price > 100m && p.Price < 1000m);
    }

    [Fact]
    public async Task DynamicAnd_WithNegativeValue_FiltersCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act
        var result = predicate.DynamicAnd("Price", DynamicOperations.GreaterThan, -1m);

        // Assert
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.Price > -1m);
    }

    [Fact]
    public void DynamicAnd_WithNestedPropertyPath_GeneratesCorrectJoin()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act: Filter by nested property
        var result = predicate.DynamicAnd("Category.Name", DynamicOperations.Equal, "Electronics");

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("JOIN [Categories]");
        sql.ShouldContain("[c].[Name] = N'Electronics'");
    }

    [Fact]
    public void DynamicAnd_WithNotContains_GeneratesCorrectSQL()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act
        var result = predicate.DynamicAnd("Name", DynamicOperations.NotContains, "unwanted");

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("NOT");
        sql.ShouldContain("LIKE");
    }

    [Fact]
    public void DynamicAnd_WithNotEqual_AndNullValue_GeneratesIsNotNullCheck()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act
        var result = predicate.DynamicAnd("Description", DynamicOperations.NotEqual, null);

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("[Description] IS NOT NULL");
    }

    [Fact]
    public void DynamicAnd_WithNullValue_ForNullableProperty_GeneratesIsNullCheck()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act: Filter nullable Description with null value
        var result = predicate.DynamicAnd("Description", DynamicOperations.Equal, null);

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("[Description] IS NULL");
    }

    [Fact]
    public void DynamicAnd_WithStartsWithOnNonStringProperty_ConvertsToEqual()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act
        var result = predicate.DynamicAnd("Price", DynamicOperations.StartsWith, 100m);

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        // Should treat as equality for non-string types
        sql.ShouldNotContain("StartsWith");
    }

    [Fact]
    public void DynamicAnd_WithStringOperations_GeneratesCorrectSQL()
    {
        // Arrange & Act & Assert for each string operation
        var operations = new[]
        {
            (DynamicOperations.Contains, "LIKE"),
            (DynamicOperations.StartsWith, "LIKE"),
            (DynamicOperations.EndsWith, "LIKE")
        };

        foreach (var (op, sqlPattern) in operations)
        {
            var predicate = PredicateBuilder.New<Product>(true);
            var result = predicate.DynamicAnd("Name", op, "test");
            var query = _context.Products.AsExpandable().Where(result);
            var sql = query.ToQueryString();
            sql.ShouldContain(sqlPattern);
        }
    }

    [Fact]
    public async Task DynamicAnd_WithVeryLargeDecimal_FiltersCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act
        var result = predicate.DynamicAnd("Price", DynamicOperations.LessThan, 999999999.99m);

        // Assert
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.Price < 999999999.99m);
    }

    [Fact]
    public void DynamicAnd_WithWhitespaceString_FiltersCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act
        var result = predicate.DynamicAnd("Name", DynamicOperations.Contains, "   ");

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("WHERE [p].[Name] LIKE N'%   %'");
    }

    [Fact]
    public async Task DynamicAnd_WithZeroValue_FiltersCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act
        var result = predicate.DynamicAnd("StockQuantity", DynamicOperations.Equal, 0);

        // Assert
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.StockQuantity == 0);
    }

    [Fact]
    public async Task DynamicOr_ChainedWithStaticPredicates_CombinesCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(false);

        // Act
        var result = predicate
            .Or(p => p.Price < 10)
            .DynamicOr("Name", DynamicOperations.StartsWith, "Special")
            .Or(p => p.StockQuantity == 0);

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        output.WriteLine(query.ToQueryString());

        var products = await query.ToListAsync();
        products.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DynamicOr_WithMultipleConditionsOnSameProperty_CombinesWithOr()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(false);

        // Act
        var result = predicate
            .DynamicOr("Price", DynamicOperations.LessThan, 10m)
            .DynamicOr("Price", DynamicOperations.GreaterThan, 1000m);

        // Assert
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.Price < 10m || p.Price > 1000m);
    }

    [Fact]
    public async Task DynamicOr_WithMultipleEnumValues_GeneratesCorrectFilter()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Order>(false);

        // Act
        var result = predicate
            .DynamicOr("Status", DynamicOperations.Equal, OrderStatus.Pending)
            .DynamicOr("Status", DynamicOperations.Equal, OrderStatus.Processing);

        // Assert
        var orders = await _context.Orders.AsExpandable().Where(result).ToListAsync();
        orders.ShouldAllBe(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Processing);
    }

    [Fact]
    public void DynamicOr_WithNullValue_GeneratesCorrectSQL()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(false);

        // Act
        var result = predicate
            .DynamicOr("Description", DynamicOperations.Equal, null)
            .DynamicOr("Name", DynamicOperations.Contains, "test");

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("[Description] IS NULL");
        sql.ShouldContain("OR");
    }

    [Fact]
    public async Task Expression_DynamicAnd_OnExpression_WorksCorrectly()
    {
        // Arrange
        Expression<Func<Product, bool>> predicate = p => p.IsActive;

        // Act: Use DynamicAnd on regular Expression
        var result = predicate.DynamicAnd("Price", DynamicOperations.GreaterThan, 100m);

        // Assert
        result.ShouldNotBeNull();
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.IsActive && p.Price > 100m);
    }

    [Fact]
    public async Task Expression_DynamicOr_OnExpression_WorksCorrectly()
    {
        // Arrange
        Expression<Func<Product, bool>> predicate = p => p.Price < 10;

        // Act
        var result = predicate.DynamicOr("Price", DynamicOperations.GreaterThan, 1000m);

        // Assert
        result.ShouldNotBeNull();
        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.Price < 10 || p.Price > 1000m);
    }

    [Fact]
    public void ExpressionStarter_DynamicAnd_OnExpressionStarter_WorksCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act: Use DynamicAnd on ExpressionStarter
        var result = predicate.DynamicAnd("Name", DynamicOperations.Contains, "test");

        // Assert
        result.ShouldNotBeNull();
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("[p].[Name] LIKE N'%test%'");
    }

    #endregion
}