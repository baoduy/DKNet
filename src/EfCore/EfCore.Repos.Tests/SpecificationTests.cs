using System.Linq.Expressions;

namespace EfCore.Repos.Tests;

/// <summary>
/// Tests for the core Specification class functionality
/// </summary>
public class SpecificationTests
{
    [Fact]
    public void Constructor_WithNoParameters_ShouldCreateEmptySpecification()
    {
        // Arrange & Act
        var spec = new TestSpecification();

        // Assert
        spec.FilterQuery.ShouldBeNull();
        spec.IncludeQueries.ShouldBeEmpty();
        spec.OrderByQueries.ShouldBeEmpty();
        spec.OrderByDescendingQueries.ShouldBeEmpty();
        spec.IgnoreQueryFilters.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithExpression_ShouldSetFilterQuery()
    {
        // Arrange
        Expression<Func<User, bool>> filter = u => u.FirstName == "John";

        // Act
        var spec = new TestSpecification(filter);

        // Assert
        spec.FilterQuery.ShouldBe(filter);
        spec.IncludeQueries.ShouldBeEmpty();
        spec.OrderByQueries.ShouldBeEmpty();
        spec.OrderByDescendingQueries.ShouldBeEmpty();
        spec.IgnoreQueryFilters.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithISpecification_ShouldCopyAllProperties()
    {
        // Arrange
        var originalSpec = new TestSpecification();
        originalSpec.AddTestFilter(u => u.FirstName == "John");
        originalSpec.AddTestInclude(u => u.Addresses);
        originalSpec.AddTestOrderBy(u => u.LastName);
        originalSpec.AddTestOrderByDescending(u => u.FirstName);
        originalSpec.EnableIgnoreQueryFilters();

        // Act
        var copiedSpec = new TestSpecification(originalSpec);

        // Assert
        copiedSpec.FilterQuery.ShouldNotBeNull();
        copiedSpec.IncludeQueries.Count.ShouldBe(1);
        copiedSpec.OrderByQueries.Count.ShouldBe(1);
        copiedSpec.OrderByDescendingQueries.Count.ShouldBe(1);
        copiedSpec.IgnoreQueryFilters.ShouldBeTrue();
    }

    [Fact]
    public void WithFilter_ShouldSetFilterQuery()
    {
        // Arrange
        var spec = new TestSpecification();
        Expression<Func<User, bool>> filter = u => u.FirstName == "John";

        // Act
        spec.AddTestFilter(filter);

        // Assert
        spec.FilterQuery.ShouldBe(filter);
    }

    [Fact]
    public void AddInclude_ShouldAddToIncludeQueries()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddTestInclude(u => u.Addresses);

        // Assert
        spec.IncludeQueries.Count.ShouldBe(1);
        spec.IncludeQueries.First().ShouldNotBeNull();
    }

    [Fact]
    public void AddOrderBy_ShouldAddToOrderByQueries()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddTestOrderBy(u => u.LastName);

        // Assert
        spec.OrderByQueries.Count.ShouldBe(1);
        spec.OrderByQueries.First().ShouldNotBeNull();
    }

    [Fact]
    public void AddOrderByDescending_ShouldAddToOrderByDescendingQueries()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddTestOrderByDescending(u => u.FirstName);

        // Assert
        spec.OrderByDescendingQueries.Count.ShouldBe(1);
        spec.OrderByDescendingQueries.First().ShouldNotBeNull();
    }

    [Fact]
    public void IgnoreQueryFiltersEnabled_ShouldSetIgnoreQueryFiltersToTrue()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.EnableIgnoreQueryFilters();

        // Assert
        spec.IgnoreQueryFilters.ShouldBeTrue();
    }

    [Fact]
    public void Match_WithValidFilter_ShouldReturnTrue()
    {
        // Arrange
        var spec = new TestSpecification();
        spec.AddTestFilter(u => u.FirstName == "John");
        var user = new User("testUser") { FirstName = "John", LastName = "Doe" };

        // Act
        var result = spec.Match(user);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_WithInvalidFilter_ShouldReturnFalse()
    {
        // Arrange
        var spec = new TestSpecification();
        spec.AddTestFilter(u => u.FirstName == "John");
        var user = new User("testUser") { FirstName = "Jane", LastName = "Doe" };

        // Act
        var result = spec.Match(user);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Match_WithNullFilter_ShouldReturnFalse()
    {
        // Arrange
        var spec = new TestSpecification();
        var user = new User("testUser") { FirstName = "John", LastName = "Doe" };

        // Act
        var result = spec.Match(user);

        // Assert
        result.ShouldBeFalse();
    }


    [Fact]
    public void AddMultipleIncludes_ShouldAddAllToCollection()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddTestInclude(u => u.Addresses);
        spec.AddTestInclude(u => u.FirstName);

        // Assert
        spec.IncludeQueries.Count.ShouldBe(2);
    }

    [Fact]
    public void AddMultipleOrderBy_ShouldAddAllToCollection()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddTestOrderBy(u => u.LastName);
        spec.AddTestOrderBy(u => u.FirstName);

        // Assert
        spec.OrderByQueries.Count.ShouldBe(2);
    }

    [Fact]
    public void AddMultipleOrderByDescending_ShouldAddAllToCollection()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddTestOrderByDescending(u => u.LastName);
        spec.AddTestOrderByDescending(u => u.FirstName);

        // Assert
        spec.OrderByDescendingQueries.Count.ShouldBe(2);
    }
}

/// <summary>
/// Test implementation of Specification for testing purposes
/// </summary>
public class TestSpecification : Specification<User>
{
    public TestSpecification()
    {
    }

    public TestSpecification(Expression<Func<User, bool>> filter) : base(filter)
    {
    }

    public TestSpecification(ISpecification<User> specification) : base(specification)
    {
    }

    // Expose protected methods for testing
    public void AddTestFilter(Expression<Func<User, bool>> filter) => WithFilter(filter);
    public void AddTestInclude(Expression<Func<User, object?>> include) => AddInclude(include);
    public void AddTestOrderBy(Expression<Func<User, object>> orderBy) => AddOrderBy(orderBy);

    public void AddTestOrderByDescending(Expression<Func<User, object>> orderByDesc) =>
        AddOrderByDescending(orderByDesc);

    public void EnableIgnoreQueryFilters() => IgnoreQueryFiltersEnabled();
}