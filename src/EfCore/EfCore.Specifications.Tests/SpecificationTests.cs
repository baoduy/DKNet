namespace EfCore.Specifications.Tests;

/// <summary>
///     Tests for the core Specification class functionality
/// </summary>
public class SpecificationTests
{
    #region Methods

    [Fact]
    public void AddInclude_ShouldAddToIncludeQueries()
    {
        // Arrange
        var spec = new TestProductSpecification();

        // Act
        spec.AddTestInclude(p => p.Category);

        // Assert
        spec.IncludeQueries.Count.ShouldBe(1);
        spec.IncludeQueries.First().ShouldNotBeNull();
    }

    [Fact]
    public void AddMultipleIncludes_ShouldAddAllToCollection()
    {
        // Arrange
        var spec = new TestProductSpecification();

        // Act
        spec.AddTestInclude(p => p.Category);
        spec.AddTestInclude(p => p.OrderItems);

        // Assert
        spec.IncludeQueries.Count.ShouldBe(2);
    }

    [Fact]
    public void AddMultipleOrderBy_ShouldAddAllToCollection()
    {
        // Arrange
        var spec = new TestProductSpecification();

        // Act
        spec.AddTestOrderBy(p => p.Name);
        spec.AddTestOrderBy(p => p.Price);

        // Assert
        spec.OrderByQueries.Count.ShouldBe(2);
    }

    [Fact]
    public void AddMultipleOrderByDescending_ShouldAddAllToCollection()
    {
        // Arrange
        var spec = new TestProductSpecification();

        // Act
        spec.AddTestOrderByDescending(p => p.Price);
        spec.AddTestOrderByDescending(p => p.Name);

        // Assert
        spec.OrderByDescendingQueries.Count.ShouldBe(2);
    }

    [Fact]
    public void AddOrderBy_ShouldAddToOrderByQueries()
    {
        // Arrange
        var spec = new TestProductSpecification();

        // Act
        spec.AddTestOrderBy(p => p.Name);

        // Assert
        spec.OrderByQueries.Count.ShouldBe(1);
        spec.OrderByQueries.First().ShouldNotBeNull();
    }

    [Fact]
    public void AddOrderByDescending_ShouldAddToOrderByDescendingQueries()
    {
        // Arrange
        var spec = new TestProductSpecification();

        // Act
        spec.AddTestOrderByDescending(p => p.Price);

        // Assert
        spec.OrderByDescendingQueries.Count.ShouldBe(1);
        spec.OrderByDescendingQueries.First().ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithExpression_ShouldSetFilterQuery()
    {
        // Arrange
        Expression<Func<Product, bool>> filter = p => p.Name == "TestProduct";

        // Act
        var spec = new TestProductSpecification(filter);

        // Assert
        spec.FilterQuery.ShouldBe(filter);
        spec.IncludeQueries.ShouldBeEmpty();
        spec.OrderByQueries.ShouldBeEmpty();
        spec.OrderByDescendingQueries.ShouldBeEmpty();
        spec.IsIgnoreQueryFilters.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithISpecification_ShouldCopyAllProperties()
    {
        // Arrange
        var originalSpec = new TestProductSpecification();
        originalSpec.AddTestFilter(p => p.Name == "TestProduct");
        originalSpec.AddTestInclude(p => p.Category);
        originalSpec.AddTestOrderBy(p => p.Name);
        originalSpec.AddTestOrderByDescending(p => p.Price);
        originalSpec.EnableIgnoreQueryFilters();

        // Act
        var copiedSpec = new TestProductSpecification(originalSpec);

        // Assert
        copiedSpec.FilterQuery.ShouldNotBeNull();
        copiedSpec.IncludeQueries.Count.ShouldBe(1);
        copiedSpec.OrderByQueries.Count.ShouldBe(1);
        copiedSpec.OrderByDescendingQueries.Count.ShouldBe(1);
        copiedSpec.IsIgnoreQueryFilters.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNoParameters_ShouldCreateEmptySpecification()
    {
        // Arrange & Act
        var spec = new TestProductSpecification();

        // Assert
        spec.FilterQuery.ShouldBeNull();
        spec.IncludeQueries.ShouldBeEmpty();
        spec.OrderByQueries.ShouldBeEmpty();
        spec.OrderByDescendingQueries.ShouldBeEmpty();
        spec.IsIgnoreQueryFilters.ShouldBeFalse();
    }

    [Fact]
    public void IgnoreQueryFiltersEnabled_ShouldSetIgnoreQueryFiltersToTrue()
    {
        // Arrange
        var spec = new TestProductSpecification();

        // Act
        spec.EnableIgnoreQueryFilters();

        // Assert
        spec.IsIgnoreQueryFilters.ShouldBeTrue();
    }

    // [Fact]
    // public void Match_WithInvalidFilter_ShouldReturnFalse()
    // {
    //     // Arrange
    //     var spec = new TestProductSpecification();
    //     spec.AddTestFilter(p => p.Name == "Product1");
    //     var product = new Product { Name = "Product2", Price = 100m };
    //
    //     // Act
    //     var result = spec.Match(product);
    //
    //     // Assert
    //     result.ShouldBeFalse();
    // }

    // [Fact]
    // public void Match_WithNullFilter_ShouldReturnFalse()
    // {
    //     // Arrange
    //     var spec = new TestProductSpecification();
    //     var product = new Product { Name = "TestProduct", Price = 100m };
    //
    //     // Act
    //     var result = spec.Match(product);
    //
    //     // Assert
    //     result.ShouldBeFalse();
    // }

    // [Fact]
    // public void Match_WithValidFilter_ShouldReturnTrue()
    // {
    //     // Arrange
    //     var spec = new TestProductSpecification();
    //     spec.AddTestFilter(p => p.Name == "TestProduct");
    //     var product = new Product { Name = "TestProduct", Price = 100m };
    //
    //     // Act
    //     var result = spec.Match(product);
    //
    //     // Assert
    //     result.ShouldBeTrue();
    // }

    [Fact]
    public void WithFilter_ShouldSetFilterQuery()
    {
        // Arrange
        var spec = new TestProductSpecification();
        Expression<Func<Product, bool>> filter = p => p.Name == "TestProduct";

        // Act
        spec.AddTestFilter(filter);

        // Assert
        spec.FilterQuery.ShouldBe(filter);
    }

    #endregion
}

/// <summary>
///     Test implementation of Specification for testing purposes
/// </summary>
public class TestProductSpecification : Specification<Product>
{
    #region Constructors

    public TestProductSpecification()
    {
    }

    public TestProductSpecification(Expression<Func<Product, bool>> filter) : base(filter)
    {
    }

    public TestProductSpecification(ISpecification<Product> specification) : base(specification)
    {
    }

    #endregion

    #region Methods

    // Expose protected methods for testing
    public void AddTestFilter(Expression<Func<Product, bool>> filter) => WithFilter(filter);

    public void AddTestInclude(Expression<Func<Product, object?>> include) => AddInclude(include);

    public void AddTestOrderBy(Expression<Func<Product, object>> orderBy) => AddOrderBy(orderBy);

    public void AddTestOrderByDescending(Expression<Func<Product, object>> orderByDesc) =>
        AddOrderByDescending(orderByDesc);

    public void EnableIgnoreQueryFilters() => IgnoreQueryFilters();

    #endregion
}