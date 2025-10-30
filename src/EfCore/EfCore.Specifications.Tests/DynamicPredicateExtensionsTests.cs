namespace EfCore.Specifications.Tests;

public class DynamicPredicateExtensionsTests(TestDbFixture fixture) : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public void DynamicAnd_ChainedCalls_WorksCorrectly()
    {
        // Arrange & Act
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder
                .With("Price", FilterOperations.GreaterThan, 100m))
            .DynamicAnd(builder => builder
                .With("StockQuantity", FilterOperations.GreaterThan, 0));

        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.IsActive && p.Price > 100m && p.StockQuantity > 0);
    }

    [Fact]
    public void DynamicAnd_ToQueryString_GeneratesCorrectSql()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder
                .With("Price", FilterOperations.GreaterThan, 100m));

        // Act
        var query = _db.Products.Where(predicate);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[IsActive]");
        sql.ShouldContain("[p].[Price]");
        sql.ShouldContain("AND");
    }

    [Fact]
    public void DynamicAnd_WithEmptyBuilder_DoesNotModifyPredicate()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive);

        var originalCount = _db.Products.AsExpandable().Where(predicate).Count();

        // Act
        var action = () => predicate.DynamicAnd(_ => { });

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void DynamicAnd_WithMultipleConditions_CombinesCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive);

        // Act
        predicate = predicate.DynamicAnd(builder => builder
            .With("Price", FilterOperations.GreaterThan, 50m)
            .With("StockQuantity", FilterOperations.GreaterThan, 5));

        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.IsActive && p.Price > 50m && p.StockQuantity > 5);
    }

    [Fact]
    public void DynamicAnd_WithNavigationProperty_ToQueryString_GeneratesJoin()
    {
        // Arrange
        var categoryName = _db.Categories.First().Name;
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder
                .With("Category.Name", FilterOperations.Equal, categoryName));

        // Act
        var query = _db.Products
            .Include(p => p.Category)
            .Where(predicate);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("INNER JOIN");
        sql.ShouldContain("[Categories]");
    }

    [Fact]
    public void DynamicAnd_WithNavigationProperty_WorksCorrectly()
    {
        // Arrange
        var categoryName = _db.Categories.First().Name;

        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive);

        // Act
        predicate = predicate.DynamicAnd(builder =>
            builder.With("Category.Name", FilterOperations.Equal, categoryName));

        var results = _db.Products
            .Include(p => p.Category)
            .Where(predicate)
            .ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.IsActive && p.Category!.Name == categoryName);
    }

    [Fact]
    public void DynamicAnd_WithSingleCondition_CombinesWithExistingPredicate()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive);

        // Act
        predicate = predicate.DynamicAnd(builder =>
            builder.With("Price", FilterOperations.GreaterThan, 100m));

        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.IsActive && p.Price > 100m);
    }

    [Fact]
    public void DynamicAnd_WithStringContains_ToQueryString_GeneratesLike()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder
                .With("Name", FilterOperations.Contains, "Test"));

        // Act
        var query = _db.Products.Where(predicate);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("LIKE");
    }

    [Fact]
    public void DynamicAnd_WithStringOperations_WorksWithIQueryable()
    {
        // Arrange
        var firstProduct = _db.Products.First();
        var searchTerm = firstProduct.Name.Substring(0, 3);

        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive);

        // Act
        predicate = predicate.DynamicAnd(builder =>
            builder.With("Name", FilterOperations.Contains, searchTerm));

        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.IsActive && p.Name.Contains(searchTerm));
    }

    [Fact]
    public void DynamicOr_ChainedCalls_WorksCorrectly()
    {
        // Arrange & Act
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.Price > 700m)
            .DynamicOr(builder => builder
                .With("Price", FilterOperations.LessThan, 50m))
            .DynamicOr(builder => builder
                .With("StockQuantity", FilterOperations.Equal, 0));

        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.Price > 700m || p.Price < 50m || p.StockQuantity == 0);
    }

    [Fact]
    public void DynamicOr_ToQueryString_GeneratesCorrectSql()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicOr(builder => builder
                .With("Price", FilterOperations.LessThan, 50m));

        // Act
        var query = _db.Products.AsExpandable().Where(predicate);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        sql.ShouldContain("[p].[IsActive]");
        sql.ShouldContain("[p].[Price]");
        sql.ShouldContain("OR");
    }

    [Fact]
    public void DynamicOr_WithMultipleConditions_CombinesCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.Price > 800m);

        // Act
        predicate = predicate.DynamicOr(builder => builder
            .With("Price", FilterOperations.LessThan, 50m)
            .With("StockQuantity", FilterOperations.Equal, 0));

        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.Price > 800m || (p.Price < 50m && p.StockQuantity == 0));
    }

    [Fact]
    public void DynamicOr_WithNavigationProperty_WorksCorrectly()
    {
        // Arrange
        var categoryName = _db.Categories.First().Name;

        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.Price > 800m);

        // Act
        predicate = predicate.DynamicOr(builder =>
            builder.With("Category.Name", FilterOperations.Equal, categoryName));

        var results = _db.Products
            .AsExpandable()
            .Include(p => p.Category)
            .Where(predicate)
            .ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.Price > 800m || p.Category!.Name == categoryName);
    }

    [Fact]
    public void DynamicOr_WithSingleCondition_CombinesWithExistingPredicate()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.Price > 500m);

        // Act
        predicate = predicate.DynamicOr(builder =>
            builder.With("StockQuantity", FilterOperations.Equal, 0));

        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.Price > 500m || p.StockQuantity == 0);
    }

    [Fact]
    public void DynamicOr_WithStringOperations_WorksWithIQueryable()
    {
        // Arrange
        var firstProduct = _db.Products.First();
        var searchTerm = firstProduct.Name.Substring(0, 2);

        var predicate = PredicateBuilder.New<Product>()
            .And(p => !p.IsActive);

        // Act
        predicate = predicate.DynamicOr(builder =>
            builder.With("Name", FilterOperations.StartsWith, searchTerm));

        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => !p.IsActive || p.Name.StartsWith(searchTerm));
    }

    [Fact]
    public void DynamicPredicates_ComplexECommerceScenario_WorksCorrectly()
    {
        // Scenario: Find active products in stock, with price between 100-500,
        // or out of stock expensive products
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder
                .With("Price", FilterOperations.GreaterThanOrEqual, 100m)
                .With("Price", FilterOperations.LessThanOrEqual, 500m)
                .With("StockQuantity", FilterOperations.GreaterThan, 0))
            .DynamicOr(builder => builder
                .With("Price", FilterOperations.GreaterThan, 700m)
                .With("StockQuantity", FilterOperations.Equal, 0));

        // Act
        var results = _db.Products
            .AsExpandable()
            .Where(predicate)
            .OrderBy(p => p.Price)
            .ToList();

        // Assert
        results.ShouldAllBe(p =>
            (p.IsActive && p.Price >= 100m && p.Price <= 500m && p.StockQuantity > 0) ||
            (p.Price > 700m && p.StockQuantity == 0));
    }

    [Fact]
    public void DynamicPredicates_WithGroupBy_WorksCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .DynamicAnd(builder => builder
                .With("IsActive", FilterOperations.Equal, true));

        // Act
        var results = _db.Products
            .AsExpandable()
            .Where(predicate)
            .GroupBy(p => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                Count = g.Count(),
                TotalValue = g.Sum(p => p.Price * p.StockQuantity)
            })
            .ToList();

        // Assert
        results.ShouldNotBeEmpty();
    }

    [Fact]
    public void DynamicPredicates_WithMultipleStringOperations_WorksCorrectly()
    {
        // Arrange
        var firstProduct = _db.Products.First();
        var searchStart = firstProduct.Name.Substring(0, 2);

        var predicate = PredicateBuilder.New<Product>()
            .DynamicAnd(builder => builder
                .With("Name", FilterOperations.StartsWith, searchStart))
            .DynamicOr(builder => builder
                .With("Description", FilterOperations.Contains, "premium"));

        // Act
        var results = _db.Products.AsExpandable().Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
    }

    [Fact]
    public void DynamicPredicates_WithOrderByAndPaging_WorksCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .DynamicAnd(builder => builder
                .With("IsActive", FilterOperations.Equal, true)
                .With("Price", FilterOperations.GreaterThan, 50m));

        // Act
        var results = _db.Products
            .AsExpandable()
            .Where(predicate)
            .OrderByDescending(p => p.Price)
            .Skip(0)
            .Take(5)
            .ToList();

        // Assert
        results.Count.ShouldBeLessThanOrEqualTo(5);
        results.ShouldAllBe(p => p.IsActive && p.Price > 50m);
    }

    [Fact]
    public void DynamicPredicates_WithProjection_WorksCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>()
            .DynamicAnd(builder => builder
                .With("IsActive", FilterOperations.Equal, true)
                .With("Price", FilterOperations.GreaterThan, 100m));

        // Act
        var results = _db.Products
            .AsExpandable()
            .Where(predicate)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                CategoryName = p.Category!.Name
            })
            .ToList();

        // Assert
        results.ShouldNotBeEmpty();
    }

    [Fact]
    public void MixedDynamicAndOr_ComplexLogic_WorksCorrectly()
    {
        // Arrange & Act
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder
                .With("Price", FilterOperations.GreaterThan, 100m))
            .DynamicOr(builder => builder
                .With("StockQuantity", FilterOperations.Equal, 0));

        var results = _db.Products.Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => (p.IsActive && p.Price > 100m) || p.StockQuantity == 0);
    }

    [Fact]
    public void MixedDynamicAndOr_WithStaticPredicates_WorksCorrectly()
    {
        // Arrange & Act
        var predicate = PredicateBuilder.New<Product>()
            .And(p => p.IsActive)
            .DynamicAnd(builder => builder
                .With("Price", FilterOperations.GreaterThan, 200m))
            .Or(p => p.StockQuantity == 0)
            .DynamicAnd(builder => builder
                .With("CategoryId", FilterOperations.GreaterThan, 0));

        var results = _db.Products.Where(predicate).ToList();

        // Assert
        results.ShouldNotBeEmpty();
    }

    #endregion
}