using System.Linq.Dynamic.Core;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Comprehensive tests that verify SQL query generation from dynamic predicates.
///     These tests demonstrate that ToQueryString() produces correct SQL for various scenarios.
/// </summary>
public class QueryStringVerificationTests : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _db;

    #endregion

    #region Constructors

    public QueryStringVerificationTests(TestDbFixture fixture) => _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public void ComplexMultiConditionFilter_GeneratesAllConditions()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("IsActive", FilterOperations.Equal, true)
            .With("Price", FilterOperations.GreaterThan, 50m)
            .With("StockQuantity", FilterOperations.GreaterThan, 0)
            .With("Name", FilterOperations.Contains, "Product");

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products.Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[IsActive]");
        sql.ShouldContain("[p].[Price] >");
        sql.ShouldContain("[p].[StockQuantity] >");
        sql.ShouldContain("[p].[Name] LIKE");

        // Count AND occurrences (should be 3 for 4 conditions)
        var andCount = sql.Split(new[] { "AND" }, StringSplitOptions.None).Length - 1;
        andCount.ShouldBe(3);
    }

    [Fact]
    public void DateComparisonFilter_GeneratesCorrectDateComparison()
    {
        // Arrange
        var cutoffDate = new DateTime(2024, 1, 1);
        var builder = new DynamicPredicateBuilder()
            .With("CreatedDate", FilterOperations.GreaterThanOrEqual, cutoffDate);

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products.Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[CreatedDate] >=");
    }

    [Fact]
    public void DynamicAndWithExpressionStarter_GeneratesCorrectSql()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder
                .With("Price", FilterOperations.GreaterThan, 100m)
                .With("StockQuantity", FilterOperations.GreaterThan, 0));

        // Act
        var query = _db.Products.AsExpandable().Where(predicate);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[IsActive]");
        sql.ShouldContain("[p].[Price] >");
        sql.ShouldContain("[p].[StockQuantity] >");

        // All conditions should be combined with AND
        var andCount = sql.Split(new[] { "AND" }, StringSplitOptions.None).Length - 1;
        andCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void DynamicOrWithExpressionStarter_GeneratesCorrectSql()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.Price > 800m)
            .DynamicOr(builder => builder
                .With("StockQuantity", FilterOperations.Equal, 0));

        // Act
        var query = _db.Products.AsExpandable().Where(predicate);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[Price] >");
        sql.ShouldContain("[p].[StockQuantity] =");
        sql.ShouldContain("OR");
    }

    [Fact]
    public void NavigationPropertyFilter_GeneratesInnerJoin()
    {
        // Arrange
        var categoryName = _db.Categories.First().Name;
        var builder = new DynamicPredicateBuilder()
            .With("Category.Name", FilterOperations.Equal, categoryName);

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products
            .Include(p => p.Category)
            .Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("INNER JOIN [Categories]");
        sql.ShouldContain("[c].[Name] =");
    }

    [Fact]
    public void NotEqualFilter_GeneratesNotEqualInSql()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("IsActive", FilterOperations.NotEqual, false);

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products.Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE [p].[IsActive] = CAST(1 AS bit)");
    }

    [Fact]
    public void QueryWithGroupBy_GeneratesGroupByClause()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("IsActive", FilterOperations.Equal, true);

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products
            .Where(expression, parameters)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("GROUP BY [p].[CategoryId]");
        sql.ShouldContain("COUNT(*)");
    }

    [Fact]
    public void QueryWithOrderBy_GeneratesOrderByClause()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("IsActive", FilterOperations.Equal, true);

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products
            .Where(expression, parameters)
            .OrderBy(p => p.Price);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("ORDER BY [p].[Price]");
    }

    [Fact]
    public void QueryWithProjection_GeneratesSelectWithSpecificColumns()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("IsActive", FilterOperations.Equal, true);

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products
            .Where(expression, parameters)
            .Select(p => new { p.Id, p.Name, p.Price });
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[Id]");
        sql.ShouldContain("[p].[Name]");
        sql.ShouldContain("[p].[Price]");

        // Should not select all columns
        sql.ShouldNotContain("[p].[Description]");
        sql.ShouldNotContain("[p].[StockQuantity]");
    }

    [Fact]
    public void QueryWithSkipTake_GeneratesOffsetFetch()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("IsActive", FilterOperations.Equal, true);

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products
            .Where(expression, parameters)
            .OrderBy(p => p.Id)
            .Skip(10)
            .Take(5);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("ORDER BY");
        sql.ShouldContain("OFFSET");
        sql.ShouldContain("ROWS FETCH NEXT");
    }

    [Fact]
    public void RangeFilter_GeneratesCorrectSqlWithAndConditions()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("Price", FilterOperations.GreaterThanOrEqual, 100m)
            .With("Price", FilterOperations.LessThanOrEqual, 500m);

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products.Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[Price] >=");
        sql.ShouldContain("AND [p].[Price] <=");
    }

    [Fact]
    public void SimpleFilter_GeneratesCorrectSqlWithWhere()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("IsActive", FilterOperations.Equal, true);

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products.Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("SELECT");
        sql.ShouldContain("FROM [Products]");
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[IsActive] = CAST(1 AS bit)");
    }

    [Fact]
    public void StringContainsFilter_GeneratesLikeInSql()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("Name", FilterOperations.Contains, "Test");

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products.Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[Name] LIKE");
        sql.ShouldContain("%Test%");
    }

    [Fact]
    public void StringEndsWithFilter_GeneratesCorrectLikePattern()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("Name", FilterOperations.EndsWith, "ing");

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products.Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[Name] LIKE");
        sql.ShouldContain("%ing");
    }

    [Fact]
    public void StringStartsWithFilter_GeneratesCorrectLikePattern()
    {
        // Arrange
        var builder = new DynamicPredicateBuilder()
            .With("Name", FilterOperations.StartsWith, "Pro");

        var (expression, parameters) = builder.Build();

        // Act
        var query = _db.Products.Where(expression, parameters);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[Name] LIKE");
        sql.ShouldContain("Pro%");
    }

    #endregion
}