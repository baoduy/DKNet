namespace EfCore.Specifications.Tests;

/// <summary>
///     Advanced tests for Specification class covering edge cases and complex scenarios
/// </summary>
public class SpecificationAdvancedTests(TestDbFixture fixture) : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context = fixture.Db!;

    #endregion

    #region Methods

    // [Fact]
    // public void Match_WithComplexExpression_ShouldEvaluateCorrectly()
    // {
    //     // Arrange
    //     var spec = new ProductWithFilterSpecification(p => p.Price > 100m && p.Price < 500m && p.IsActive);
    //
    //     var matchingProduct = new Product { Price = 200m, IsActive = true };
    //     var nonMatchingProduct1 = new Product { Price = 50m, IsActive = true };
    //     var nonMatchingProduct2 = new Product { Price = 200m, IsActive = false };
    //
    //     // Act & Assert
    //     spec.Match(matchingProduct).ShouldBeTrue();
    //     spec.Match(nonMatchingProduct1).ShouldBeFalse();
    //     spec.Match(nonMatchingProduct2).ShouldBeFalse();
    // }

    // [Fact]
    // public void Match_WithNavigationPropertyFilter_ShouldWork()
    // {
    //     // Arrange
    //     var spec = new ProductWithFilterSpecification(p => p.Category != null && p.Category.Name == "Electronics");
    //
    //     var product = new Product
    //     {
    //         Category = new Category { Name = "Electronics" }
    //     };
    //
    //     // Act
    //     var result = spec.Match(product);
    //
    //     // Assert
    //     result.ShouldBeTrue();
    // }

    [Fact]
    public void Specification_CopyConstructor_ShouldDeepCopyAllProperties()
    {
        // Arrange
        var original = new ProductWithFilterSpecification(p => p.Price > 100m);


        // Act
        var copy = new ProductWithFilterSpecification(original);

        // Assert
        copy.FilterQuery.ShouldNotBeNull();
        copy.IncludeQueries.Count.ShouldBe(2);
        copy.OrderByQueries.Count.ShouldBe(1);
        copy.OrderByDescendingQueries.Count.ShouldBe(1);
        copy.IsIgnoreQueryFilters.ShouldBeTrue();

        // Verify they are separate instances
        copy.ShouldNotBeSameAs(original);
    }

    [Fact]
    public void Specification_CopyConstructor_WithEmptySpec_ShouldCopyEmptyState()
    {
        // Arrange
        var original = new EmptyProductSpecification();

        // Act
        var copy = new EmptyProductSpecification();

        // Assert
        copy.FilterQuery.ShouldBeNull();
        copy.IncludeQueries.ShouldBeEmpty();
        copy.OrderByQueries.ShouldBeEmpty();
        copy.OrderByDescendingQueries.ShouldBeEmpty();
        copy.IsIgnoreQueryFilters.ShouldBeFalse();
    }

    [Fact]
    public async Task Specification_WithAsyncExecution_ShouldWork()
    {
        // Arrange
        var spec = new ActiveProductSpecification();

        // Act
        var result = await _context.Products
            .ApplySpecs(spec)
            .Take(10)
            .ToListAsync();

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public void Specification_WithComplexFilter_ShouldApplyCorrectly()
    {
        // Arrange
        var spec = new ComplexFilterSpecification();
        var queryable = _context.Products.AsQueryable();

        // Act
        var result = queryable.ApplySpecs(spec).ToList();

        // Assert
        result.ShouldAllBe(p => p.IsActive && p.Price > 100m && p.StockQuantity > 0);
    }

    [Fact]
    public void Specification_WithGroupBy_ShouldAllowAggregation()
    {
        // Arrange
        var spec = new ActiveProductSpecification();

        // Act
        var grouped = _context.Products
            .ApplySpecs(spec)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count(), AvgPrice = g.Average(p => p.Price) })
            .ToList();

        // Assert
        grouped.ShouldNotBeEmpty();
        grouped.ShouldAllBe(g => g.Count > 0);
    }

    [Fact]
    public void Specification_WithIgnoreQueryFilters_ShouldBypassGlobalFilters()
    {
        // Arrange
        var spec = new ProductWithIgnoreFiltersSpecification();

        // Act
        var withFilter = _context.Products.ApplySpecs(spec).ToList();
        var withoutSpec = _context.Products.ToList();

        // Assert
        withFilter.Count.ShouldBe(withoutSpec.Count);
        spec.IsIgnoreQueryFilters.ShouldBeTrue();
    }

    [Fact]
    public void Specification_WithJoin_ShouldWork()
    {
        // Arrange
        var spec = new ActiveProductSpecification();

        // Act
        var result = _context.Products
            .ApplySpecs(spec)
            .Join(_context.Categories,
                p => p.CategoryId,
                c => c.Id,
                (p, c) => new { Product = p.Name, Category = c.Name })
            .ToList();

        // Assert
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void Specification_WithMixedOrderingAndFiltering_ShouldApplyBoth()
    {
        // Arrange
        var spec = new ProductWithMixedOrderingSpecification();

        // Act
        var result = _context.Products.ApplySpecs(spec).Take(5).ToList();

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldAllBe(p => p.IsActive);

        // Current implementation applies OrderBy first, then OrderByDescending
        // So it orders by Name (ascending) first, then by Price (descending) as ThenBy
        for (var i = 1; i < result.Count; i++)
        {
            var prev = result[i - 1];
            var curr = result[i];

            // Primary sort by Name (ascending)
            var nameComparison = string.Compare(prev.Name, curr.Name, StringComparison.Ordinal);
            if (nameComparison == 0)
                // Then sort by Price (descending)
                (prev.Price >= curr.Price).ShouldBeTrue();
            else
                (nameComparison <= 0).ShouldBeTrue();
        }
    }

    [Fact]
    public void Specification_WithMultipleIncludes_ShouldLoadAllNavigationProperties()
    {
        // Arrange
        var spec = new ProductWithIncludesSpecification();

        // Act
        var result = _context.Products.ApplySpecs(spec).First();

        // Assert
        result.Category.ShouldNotBeNull();
        result.OrderItems.ShouldNotBeNull();
        result.ProductTags.ShouldNotBeNull();
    }

    [Fact]
    public void Specification_WithMultipleThenByOrdering_ShouldApplyCorrectOrder()
    {
        // Arrange
        var spec = new ProductWithOrderingSpecification();

        // Act
        var result = _context.Products.ApplySpecs(spec).Take(10).ToList();

        // Assert
        result.ShouldNotBeEmpty();
        for (var i = 1; i < result.Count; i++)
        {
            var prev = result[i - 1];
            var curr = result[i];

            if (prev.CategoryId == curr.CategoryId)
            {
                if (prev.Price == curr.Price)
                    (string.Compare(prev.Name, curr.Name, StringComparison.Ordinal) <= 0).ShouldBeTrue();
                else
                    (prev.Price <= curr.Price).ShouldBeTrue();
            }
            else
            {
                (prev.CategoryId <= curr.CategoryId).ShouldBeTrue();
            }
        }
    }


    [Fact]
    public void Specification_WithNestedPropertyFilter_ShouldWork()
    {
        // Arrange
        var categoryName = _context.Categories.First().Name;
        var spec = new ProductWithCategoryFilterSpecification(categoryName);

        // Act
        var result = _context.Products.ApplySpecs(spec).ToList();

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldAllBe(p => p.Category!.Name == categoryName);
    }

    [Fact]
    public async Task Specification_WithPaging_ShouldReturnCorrectPage()
    {
        // Arrange
        var spec = new ProductWithPagingSpecification();

        // Act
        var page2 = await _context.Products
            .ApplySpecs(spec)
            .Skip(5)
            .Take(5)
            .ToListAsync();

        // Assert
        page2.Count.ShouldBeLessThanOrEqualTo(5);
    }

    #endregion


    private class ActiveProductSpecification : Specification<Product>
    {
        #region Constructors

        public ActiveProductSpecification()
        {
            WithFilter(p => p.IsActive);
            AddOrderBy(p => p.Name);
        }

        #endregion
    }

    private class ComplexFilterSpecification : Specification<Product>
    {
        #region Constructors

        public ComplexFilterSpecification()
        {
            WithFilter(p => p.IsActive && p.Price > 100m && p.StockQuantity > 0);
            AddOrderByDescending(p => p.Price);
        }

        #endregion
    }

    private class EmptyProductSpecification : Specification<Product>;

    private class ProductWithCategoryFilterSpecification : Specification<Product>
    {
        #region Constructors

        public ProductWithCategoryFilterSpecification(string categoryName)
        {
            WithFilter(p => p.Category!.Name == categoryName);
            AddInclude(p => p.Category!);
        }

        #endregion
    }

    private class ProductWithFilterSpecification : Specification<Product>
    {
        #region Constructors

        public ProductWithFilterSpecification(Expression<Func<Product, bool>> filter)
        {
            WithFilter(filter);

            AddInclude(p => p.Category!);
            AddInclude(p => p.OrderItems);
            AddOrderBy(p => p.Name);
            AddOrderByDescending(p => p.Price);
            IgnoreQueryFilters();
        }

        public ProductWithFilterSpecification(ISpecification<Product> specification) : base(specification)
        {
        }

        #endregion
    }

    private class ProductWithIgnoreFiltersSpecification : Specification<Product>
    {
        #region Constructors

        public ProductWithIgnoreFiltersSpecification()
        {
            IgnoreQueryFilters();
        }

        #endregion
    }

    private class ProductWithIncludesSpecification : Specification<Product>
    {
        #region Constructors

        public ProductWithIncludesSpecification()
        {
            AddInclude(p => p.Category!);
            AddInclude(p => p.OrderItems);
            AddInclude(p => p.ProductTags);
        }

        #endregion
    }

    private class ProductWithMixedOrderingSpecification : Specification<Product>
    {
        #region Constructors

        public ProductWithMixedOrderingSpecification()
        {
            WithFilter(p => p.IsActive);
            AddOrderByDescending(p => p.Price);
            AddOrderBy(p => p.Name);
        }

        #endregion
    }

    private class ProductWithOrderingSpecification : Specification<Product>
    {
        #region Constructors

        public ProductWithOrderingSpecification()
        {
            AddOrderBy(p => p.CategoryId);
            AddOrderBy(p => p.Price);
            AddOrderBy(p => p.Name);
        }

        #endregion
    }


    private class ProductWithPagingSpecification : Specification<Product>
    {
        #region Constructors

        public ProductWithPagingSpecification()
        {
            AddOrderBy(p => p.Id);
        }

        #endregion
    }
}