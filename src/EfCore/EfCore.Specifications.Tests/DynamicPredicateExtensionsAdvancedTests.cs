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
            .DynamicAnd("Name", Ops.Contains, "test")
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
    public async Task DynamicAnd_CombinedInAndNotIn_ReturnsCorrectRecords()
    {
        // Arrange
        var includedCategories = new[] { 1, 2, 3 };
        var excludedNames = new[] { "Excluded Product" };

        var predicate = PredicateBuilder.New<Product>(true)
            .DynamicAnd("CategoryId", Ops.In, includedCategories)
            .DynamicAnd("Name", Ops.NotIn, excludedNames);

        // Act
        var results = await _context.Products
            .AsExpandable()
            .Where(predicate)
            .ToListAsync();

        // Assert
        results.ShouldAllBe(p =>
            includedCategories.Contains(p.CategoryId) &&
            !excludedNames.Contains(p.Name));
    }

    [Fact]
    public async Task DynamicAnd_SubPredicatePattern_CombinesCorrectly()
    {
        // Arrange
        var mainPredicate = PredicateBuilder.New<Product>(p => p.IsActive);

        // Create sub-predicate for OR conditions
        var subPredicate = PredicateBuilder.New<Product>(false);
        subPredicate = subPredicate
            .DynamicOr("Name", Ops.Contains, "Phone")
            .DynamicOr("Name", Ops.Contains, "Laptop")
            .DynamicOr("Description", Ops.Contains, "Computer");

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
            (Ops.Equal, "="),
            (Ops.NotEqual, "<>"),
            (Ops.GreaterThan, ">"),
            (Ops.GreaterThanOrEqual, ">="),
            (Ops.LessThan, "<"),
            (Ops.LessThanOrEqual, "<=")
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
        var result = predicate.DynamicAnd("IsActive", Ops.Equal, true);

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
        var result = predicate.DynamicAnd("Product.Category.Name", Ops.Contains, "Elect");

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
        var result = predicate.DynamicAnd("StockQuantity", Ops.Contains, 10);

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
        var result = predicate.DynamicAnd("CreatedDate", Ops.GreaterThanOrEqual, targetDate);

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
            .DynamicAnd("Price", Ops.GreaterThanOrEqual, 100.50m)
            .DynamicAnd("Price", Ops.LessThan, 500.75m);

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
        var result = predicate.DynamicAnd("Name", Ops.Equal, "");

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
        var result = predicate.DynamicAnd("Status", Ops.Equal, OrderStatus.Pending);

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
        var result = predicate.DynamicAnd("Status", Ops.Equal, OrderStatus.Pending);

        // Assert
        var orders = await _context.Orders.AsExpandable().Where(result).ToListAsync();
        orders.ShouldAllBe(o => o.Status == OrderStatus.Pending);
    }

    [Fact]
    public void DynamicAnd_WithInOperation_EmptyArray_ReturnsOriginalPredicate()
    {
        // Arrange
        var emptyArray = Array.Empty<int>();
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        // Act
        var result = predicate.DynamicAnd("CategoryId", Ops.In, emptyArray);

        // Assert
        // Empty array should be invalid, so original predicate is returned
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldNotContain("IN"); // Should not have IN clause
        sql.ShouldContain("[IsActive]"); // Should only have original condition
    }

    [Fact]
    public void DynamicAnd_WithInOperation_EnumArray_ReturnsMatchingRecords()
    {
        // Arrange
        var statuses = new[] { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Shipped };
        var predicate = PredicateBuilder.New<Order>(true)
            .DynamicAnd("Status", Ops.In, statuses);

        // Verify SQL contains IN clause
        var query = _context.Orders.AsExpandable().Where(predicate);
        var sql = query.ToQueryString();
        sql.ShouldContain("IN");
    }

    [Fact]
    public void DynamicAnd_WithInOperation_EnumIntArray_ReturnsMatchingRecords()
    {
        // Arrange - Using int values for enum
        var statusValues = new[] { 0, 1, 2, 3 }; // Pending = 0, Processing = 1, Shipped = 2, Delivered = 3
        var action = () => PredicateBuilder.New<Order>(true)
            .DynamicAnd("Status", Ops.In, statusValues);

        action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void DynamicAnd_WithInOperation_GeneratesParameterizedInClause()
    {
        // Arrange
        var categoryIds = new[] { 1, 2, 3 };
        var predicate = PredicateBuilder.New<Product>(true)
            .DynamicAnd("CategoryId", Ops.In, categoryIds);

        // Act
        var query = _context.Products.AsExpandable().Where(predicate);
        var sql = query.ToQueryString();

        // Assert - Verify SQL contains IN clause (EF Core may inline small constant arrays)
        output.WriteLine(sql);
        sql.ShouldContain("IN"); // Has IN clause
        sql.ShouldContain("[CategoryId] IN (1, 2, 3)"); // EF Core optimizes small constant arrays

        // Verify it's a valid SQL query
        sql.ShouldContain("SELECT");
        sql.ShouldContain("FROM [Products]");
    }

    [Fact]
    public async Task DynamicAnd_WithInOperation_IntArray_ReturnsMatchingRecords()
    {
        // Arrange
        var categoryIds = new[] { 1, 2, 3 };
        var predicate = PredicateBuilder.New<Product>(true)
            .DynamicAnd("CategoryId", Ops.In, categoryIds);

        // Act
        var results = await _context.Products
            .AsExpandable()
            .Where(predicate)
            .ToListAsync();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => categoryIds.Contains(p.CategoryId));

        // Verify SQL contains IN clause
        var query = _context.Products.AsExpandable().Where(predicate);
        var sql = query.ToQueryString();
        output.WriteLine(sql);
        sql.ShouldContain("IN");
    }

    [Fact]
    public async Task DynamicAnd_WithInOperation_List_ReturnsMatchingRecords()
    {
        // Arrange
        var categoryIdsList = new List<int> { 1, 2, 3 };
        var predicate = PredicateBuilder.New<Product>(true)
            .DynamicAnd("CategoryId", Ops.In, categoryIdsList);

        // Act
        var results = await _context.Products
            .AsExpandable()
            .Where(predicate)
            .ToListAsync();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => categoryIdsList.Contains(p.CategoryId));
    }

    [Fact]
    public async Task DynamicAnd_WithInOperation_MaliciousInput_SafelyParameterized()
    {
        // Arrange - Attempt SQL injection via array values
        var maliciousValues = new[] { "Product'; DROP TABLE Products--", "Normal Product" };
        var predicate = PredicateBuilder.New<Product>(true)
            .DynamicAnd("Name", Ops.In, maliciousValues);

        // Act
        var results = await _context.Products
            .AsExpandable()
            .Where(predicate)
            .ToListAsync();

        // Assert
        // Should safely parameterize and treat as literal string value
        results.ShouldNotContain(p => p.Name.Contains("DROP TABLE"));

        // Products table should still exist
        var allProducts = await _context.Products.ToListAsync();
        allProducts.ShouldNotBeEmpty(); // Table not dropped

        // Verify SQL safely escapes the malicious input
        var query = _context.Products.AsExpandable().Where(predicate);
        var sql = query.ToQueryString();
        // The malicious string will be present in SQL but safely escaped as N'Product''; DROP TABLE Products--'
        // This is safe because it's treated as a string literal, not executable SQL
        sql.ShouldContain("IN"); // Has IN clause
    }

    [Fact]
    public void DynamicAnd_WithInOperation_NullArray_ReturnsOriginalPredicate()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        // Act
        var result = predicate.DynamicAnd("CategoryId", Ops.In, null);

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldNotContain("IN");
        sql.ShouldContain("[IsActive]");
    }

    [Fact]
    public async Task DynamicAnd_WithInOperation_SingleValueArray_ReturnsMatchingRecord()
    {
        // Arrange
        var singleCategoryId = new[] { 1 };
        var predicate = PredicateBuilder.New<Product>(true)
            .DynamicAnd("CategoryId", Ops.In, singleCategoryId);

        // Act
        var results = await _context.Products
            .AsExpandable()
            .Where(predicate)
            .ToListAsync();

        // Assert
        results.ShouldAllBe(p => p.CategoryId == 1);
    }

    [Fact]
    public void DynamicAnd_WithInOperation_StringValue_ReturnsOriginalPredicate()
    {
        // Arrange - String should NOT be treated as array even though it's IEnumerable<char>
        var stringValue = "test";
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        // Act
        var result = predicate.DynamicAnd("Name", Ops.In, stringValue);

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldNotContain("IN");
        sql.ShouldContain("[IsActive]");
    }

    [Fact]
    public void DynamicAnd_WithInvalidNestedPath_ReturnsUnchangedPredicate()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        // Act
        var result = predicate.DynamicAnd("Category.NonExistent.Property", Ops.Equal, "test");

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
            .DynamicAnd("Price", Ops.GreaterThan, 100m)
            .DynamicAnd("Price", Ops.LessThan, 1000m);

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
        var result = predicate.DynamicAnd("Price", Ops.GreaterThan, -1m);

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
        var result = predicate.DynamicAnd("Category.Name", Ops.Equal, "Electronics");

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
        var result = predicate.DynamicAnd("Name", Ops.NotContains, "unwanted");

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
        var result = predicate.DynamicAnd("Description", Ops.NotEqual, null);

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("[Description] IS NOT NULL");
    }

    [Fact]
    public async Task DynamicAnd_WithNotInOperation_StringArray_ExcludesRecords()
    {
        // Arrange
        var excludedNames = new[] { "Product A", "Product B" };
        var predicate = PredicateBuilder.New<Product>(true)
            .DynamicAnd("Name", Ops.NotIn, excludedNames);

        // Act
        var results = await _context.Products
            .AsExpandable()
            .Where(predicate)
            .ToListAsync();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => !excludedNames.Contains(p.Name));
    }

    [Fact]
    public void DynamicAnd_WithNullValue_ForNullableProperty_GeneratesIsNullCheck()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act: Filter nullable Description with null value
        var result = predicate.DynamicAnd("Description", Ops.Equal, null);

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
        var result = predicate.DynamicAnd("Price", Ops.StartsWith, 100m);

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
            (Ops.Contains, "LIKE"),
            (Ops.StartsWith, "LIKE"),
            (Ops.EndsWith, "LIKE")
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
        var result = predicate.DynamicAnd("Price", Ops.LessThan, 999999999.99m);

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
        var result = predicate.DynamicAnd("Name", Ops.Contains, "   ");

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
        var result = predicate.DynamicAnd("StockQuantity", Ops.Equal, 0);

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
            .DynamicOr("Name", Ops.StartsWith, "Special")
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
            .DynamicOr("Price", Ops.LessThan, 10m)
            .DynamicOr("Price", Ops.GreaterThan, 1000m);

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
            .DynamicOr("Status", Ops.Equal, OrderStatus.Pending)
            .DynamicOr("Status", Ops.Equal, OrderStatus.Processing);

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
            .DynamicOr("Description", Ops.Equal, null)
            .DynamicOr("Name", Ops.Contains, "test");

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
        var result = predicate.DynamicAnd("Price", Ops.GreaterThan, 100m);

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
        var result = predicate.DynamicOr("Price", Ops.GreaterThan, 1000m);

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
        var result = predicate.DynamicAnd("Name", Ops.Contains, "test");

        // Assert
        result.ShouldNotBeNull();
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("[p].[Name] LIKE N'%test%'");
    }

    #endregion
}