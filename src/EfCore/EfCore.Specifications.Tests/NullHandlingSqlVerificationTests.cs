using System.Linq.Dynamic.Core;

namespace EfCore.Specifications.Tests;

/// <summary>
///     SQL Generation verification for null handling
/// </summary>
public class NullHandlingSqlVerificationTests : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _db;

    #endregion

    #region Constructors

    public NullHandlingSqlVerificationTests(TestDbFixture fixture) => _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public void Build_NullHandling_WorksWithDynamicAnd()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder
                .With("Description", FilterOperations.Equal, null)
                .With("Price", FilterOperations.GreaterThan, 100m));

        // Act
        var query = _db.Products.AsExpandable().Where(predicate);
        var sql = query.ToQueryString();
        var results = query.ToList();

        // Assert
        sql.ShouldContain("[p].[Description] IS NULL");
        results.ShouldAllBe(p => p.IsActive && p.Description == null && p.Price > 100m);
    }

    [Fact]
    public void Build_NullHandling_WorksWithNavigationProperties()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("Category.Description", FilterOperations.Equal, null);

        var (expression, parameters) = builder.Build();

        // Act
        var sql = _db.Products
            .Include(p => p.Category)
            .Where(expression, parameters)
            .ToQueryString();

        // Assert
        sql.ShouldContain("WHERE [c].[Description] IS NULL"); //"Because Name property is required."
        parameters.ShouldBeEmpty();
    }

    [Fact]
    public void Build_WithMultipleNullConditions_GeneratesCorrectSql()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("Description", FilterOperations.Equal, null)
            .With("Name", FilterOperations.NotEqual, null);

        var (expression, parameters) = builder.Build();

        // Act
        var sql = _db.Products.Where(expression, parameters).ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[Description] IS NULL");
        expression.ShouldBe("Description == null and Name != null");
        parameters.ShouldBeEmpty();
    }

    [Fact]
    public void Build_WithNullAndRegularConditions_GeneratesCorrectParameterIndexing()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("Price", FilterOperations.GreaterThan, 100m)
            .With("Description", FilterOperations.Equal, null)
            .With("IsActive", FilterOperations.Equal, true)
            .With("Name", FilterOperations.NotEqual, null)
            .With("StockQuantity", FilterOperations.LessThan, 50);

        var (expression, parameters) = builder.Build();

        // Act
        var sql = _db.Products.Where(expression, parameters).ToQueryString();

        // Assert - Verify expression structure
        expression.ShouldBe(
            "Price > @0 and Description == null and IsActive == @1 and Name != null and StockQuantity < @2");

        // Assert - Verify parameters (only non-null values)
        parameters.Length.ShouldBe(3);
        parameters[0].ShouldBe(100m);
        parameters[1].ShouldBe(true);
        parameters[2].ShouldBe(50);

        // Assert - Verify SQL contains IS NULL/IS NOT NULL
        sql.ShouldContain("[p].[Price] >");
        sql.ShouldContain("[p].[Description] IS NULL");
        sql.ShouldContain("[p].[IsActive] = CAST(1 AS bit)");
        sql.ShouldContain("[p].[StockQuantity] <");
    }

    [Fact]
    public void Build_WithNullEqual_GeneratesIsNullInSql()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("Description", FilterOperations.Equal, null);

        var (expression, parameters) = builder.Build();

        // Act
        var sql = _db.Products.Where(expression, parameters).ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[Description] IS NULL");
        expression.ShouldBe("Description == null");
        parameters.ShouldBeEmpty();
    }

    [Fact]
    public void Build_WithNullNotEqual_GeneratesIsNotNullInSql()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("Description", FilterOperations.NotEqual, null);

        var (expression, parameters) = builder.Build();

        // Act
        var sql = _db.Products.Where(expression, parameters).ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[Description] IS NOT NULL");
        expression.ShouldBe("Description != null");
        parameters.ShouldBeEmpty();
    }

    [Fact]
    public void Build_WithOnlyNullConditions_GeneratesNoParameters()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("Description", FilterOperations.Equal, null)
            .With("Name", FilterOperations.NotEqual, null);

        var (expression, parameters) = builder.Build();

        // Assert
        expression.ShouldBe("Description == null and Name != null");
        parameters.ShouldBeEmpty();

        // Verify query execution works
        var results = _db.Products.Where(expression, parameters).ToList();
        results.ShouldAllBe(p => p.Description == null && p.Name != null);
    }

    #endregion
}