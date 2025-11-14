using System.Linq.Dynamic.Core;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Edge case tests for DynamicPredicateBuilder and DynamicPredicateExtensions
/// </summary>
public class DynamicPredicateEdgeCaseTests(TestDbFixture fixture) : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public void Build_WithAllComparisonOperators_ShouldWork()
    {
        // Test each operator individually
        var operators = new[]
        {
            FilterOperations.Equal,
            FilterOperations.NotEqual,
            FilterOperations.GreaterThan,
            FilterOperations.GreaterThanOrEqual,
            FilterOperations.LessThan,
            FilterOperations.LessThanOrEqual
        };

        foreach (var op in operators)
        {
            // Arrange
            var builder = new DynamicPredicateBuilder<Product>()
                .With("Price", op, 100m);

            var (expression, parameters) = builder.Build(Conditions.And);

            // Act & Assert
            Should.NotThrow(() =>
                {
                    var results = _db.Products.Where(expression, parameters).ToList();
                }, $"Operation {op} should work");
        }
    }

    [Fact]
    public void Build_WithAllStringOperators_ShouldWork()
    {
        // Test each string operator
        var operators = new[]
        {
            FilterOperations.Contains,
            FilterOperations.NotContains,
            FilterOperations.StartsWith,
            FilterOperations.EndsWith
        };

        foreach (var op in operators)
        {
            // Arrange
            var builder = new DynamicPredicateBuilder<Product>()
                .With("Name", op, "Test");

            var (expression, parameters) = builder.Build(Conditions.And);

            // Act & Assert
            Should.NotThrow(() =>
                {
                    var results = _db.Products.Where(expression, parameters).ToList();
                }, $"Operation {op} should work");
        }
    }

    [Fact]
    public void Build_WithBooleanFalse_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("IsActive", FilterOperations.Equal, false);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert
        results.ShouldAllBe(p => !p.IsActive);
    }

    [Fact]
    public void Build_WithBooleanNotEqual_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("IsActive", FilterOperations.NotEqual, true);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert
        results.ShouldAllBe(p => !p.IsActive);
    }

    [Fact]
    public void Build_WithBooleanTrue_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("IsActive", FilterOperations.Equal, true);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert
        results.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public void Build_WithCaseSensitiveContains_ShouldRespectCase()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Name", FilterOperations.Contains, "PRODUCT");

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var sql = _db.Products.Where(expression, parameters).ToQueryString();

        // Assert
        sql.ShouldContain("LIKE");
    }

    [Fact]
    public void Build_WithDecimalPrecision_ShouldMaintainPrecision()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Price", FilterOperations.Equal, 99.99m);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var sql = _db.Products.Where(expression, parameters).ToQueryString();

        // Assert
        sql.ShouldContain("99.99");
    }

    [Fact]
    public void Build_WithDeepNavigationProperty_ShouldGenerateCorrectJoins()
    {
        // Arrange
        var categoryName = _db.Categories.First().Name;
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Category.Name", FilterOperations.Equal, categoryName);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var sql = _db.Products
            .Include(p => p.Category)
            .Where(expression, parameters)
            .ToQueryString();

        // Assert
        sql.ShouldContain("INNER JOIN");
        sql.ShouldContain("[Categories]");
    }

    [Fact]
    public void Build_WithEmptyString_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Name", FilterOperations.Equal, "");

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert
        results.ShouldAllBe(p => p.Name == "");
    }

    [Fact]
    public void Build_WithManyConditions_ShouldNotDegrade()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>();
        for (var i = 0; i < 20; i++) builder.With("Price", FilterOperations.GreaterThan, i * 10m);

        // Act & Assert
        Should.NotThrow(() =>
        {
            var (expression, parameters) = builder.Build(Conditions.And);
            var results = _db.Products.Where(expression, parameters).Take(10).ToList();
        });
    }

    [Fact]
    public void Build_WithMaxDateTime_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("CreatedDate", FilterOperations.LessThan, DateTime.MaxValue);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert
        results.ShouldAllBe(p => p.CreatedDate < DateTime.MaxValue);
    }

    [Fact]
    public void Build_WithMaxDecimalValue_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Price", FilterOperations.LessThan, decimal.MaxValue);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act & Assert
        Should.NotThrow(() =>
        {
            var results = _db.Products.Where(expression, parameters).ToList();
        });
    }

    [Fact]
    public void Build_WithMinDateTime_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("CreatedDate", FilterOperations.GreaterThan, DateTime.MinValue);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert
        results.ShouldAllBe(p => p.CreatedDate > DateTime.MinValue);
    }

    [Fact]
    public void Build_WithMixedNullAndNonNullConditions_ShouldHandleBoth()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Description", FilterOperations.Equal, null)
            .With("Price", FilterOperations.GreaterThan, 100m)
            .With("Name", FilterOperations.NotEqual, null);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Assert
        expression.ShouldBe("Description == null and Price > @0 and Name != null");
        parameters.Length.ShouldBe(1);
        parameters[0].ShouldBe(100m);

        // Act - Verify it works with actual query
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert results
        results.ShouldAllBe(p => p.Description == null && p.Price > 100m);
    }

    [Fact]
    public void Build_WithNegativeValue_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Price", FilterOperations.GreaterThan, -100m);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert
        results.ShouldAllBe(p => p.Price > -100m);
    }

    [Fact]
    public void Build_WithNotEqualNull_ShouldGenerateIsNotNullClause()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Description", FilterOperations.NotEqual, null);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Assert
        expression.ShouldBe("Description != null");
        parameters.ShouldBeEmpty();

        // Act - Verify it works with actual query
        var query = _db.Products.Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert SQL generation
        sql.ShouldContain("WHERE");
        sql.ShouldContain("IS NOT NULL");
    }

    [Fact]
    public void Build_WithNullValue_ShouldGenerateIsNullClause()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Description", FilterOperations.Equal, null);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Assert
        expression.ShouldBe("Description == null");
        parameters.ShouldBeEmpty();

        // Act - Verify it works with actual query
        var query = _db.Products.Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert SQL generation
        sql.ShouldContain("WHERE");
        sql.ShouldContain("IS NULL");
    }

    [Fact]
    public void Build_WithNullValueInMiddle_ShouldMaintainCorrectParameterIndexing()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Price", FilterOperations.GreaterThan, 50m)
            .With("Description", FilterOperations.Equal, null)
            .With("StockQuantity", FilterOperations.LessThan, 100);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Assert
        expression.ShouldBe("Price > @0 and Description == null and StockQuantity < @1");
        parameters.Length.ShouldBe(2);
        parameters[0].ShouldBe(50m);
        parameters[1].ShouldBe(100);

        // Act - Verify it works with actual query
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert results
        results.ShouldAllBe(p => p.Price > 50m && p.Description == null && p.StockQuantity < 100);
    }

    [Fact]
    public void Build_WithSpecialCharactersInValue_ShouldEscape()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Name", FilterOperations.Contains, "%_[]");

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var sql = _db.Products.Where(expression, parameters).ToQueryString();

        // Assert
        sql.ShouldContain("LIKE");
    }

    [Fact]
    public void Build_WithUnicodeCharacters_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Name", FilterOperations.Equal, "产品测试");

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act & Assert
        Should.NotThrow(() =>
        {
            var results = _db.Products.Where(expression, parameters).ToList();
        });
    }

    [Fact]
    public void Build_WithUtcNow_ShouldWork()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var builder = new DynamicPredicateBuilder<Product>()
            .With("CreatedDate", FilterOperations.LessThanOrEqual, now);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert
        results.ShouldAllBe(p => p.CreatedDate <= now);
    }

    [Fact]
    public void Build_WithWhitespaceValue_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("Description", FilterOperations.Contains, "   ");

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act & Assert
        Should.NotThrow(() =>
        {
            var results = _db.Products.Where(expression, parameters).ToList();
        });
    }

    [Fact]
    public void Build_WithZeroValue_ShouldWork()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder<Product>()
            .With("StockQuantity", FilterOperations.Equal, 0);

        var (expression, parameters) = builder.Build(Conditions.And);

        // Act
        var results = _db.Products.Where(expression, parameters).ToList();

        // Assert
        results.ShouldAllBe(p => p.StockQuantity == 0);
    }

    [Fact]
    public void DynamicAnd_ChainedMultipleTimes_ShouldCombineAllConditions()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder.With("Price", FilterOperations.GreaterThan, 50m))
            .DynamicAnd(builder => builder.With("StockQuantity", FilterOperations.GreaterThan, 0))
            .DynamicAnd(builder => builder.With("Name", FilterOperations.Contains, "Product"));

        // Act
        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldAllBe(p =>
            p.IsActive &&
            p.Price > 50m &&
            p.StockQuantity > 0 &&
            p.Name.Contains("Product"));
    }

    [Fact]
    public void DynamicAnd_WithEmptyBuilderInChain_ShouldNotAffectOtherConditions()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder.With("Price", FilterOperations.GreaterThan, 100m))
            .DynamicAnd(builder => builder.With("StockQuantity", FilterOperations.GreaterThan, 0));

        // Act
        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldAllBe(p => p.IsActive && p.Price > 100m && p.StockQuantity > 0);
    }

    [Fact]
    public void DynamicOr_ChainedMultipleTimes_ShouldCombineAllConditions()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.Price > 800m)
            .DynamicOr(builder => builder.With("Price", FilterOperations.LessThan, 50m))
            .DynamicOr(builder => builder.With("StockQuantity", FilterOperations.Equal, 0));

        // Act
        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldAllBe(p =>
            p.Price > 800m ||
            p.Price < 50m ||
            p.StockQuantity == 0);
    }

    #endregion
}