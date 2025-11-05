using System.ComponentModel;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Specification that searches across multiple string fields (currently Name and Description) using a Contains
///     operation and applies ordering based on a provided property name.
/// </summary>
internal sealed class ProductFilterSpecification : Specification<Product>
{
    #region Constructors

    /// <summary>
    ///     Initializes a new <see cref="ProductFilterSpecification" /> with a search string applied to configured fields
    ///     and an ordering column.
    /// </summary>
    /// <param name="searchString">The substring to search for in the configured fields.</param>
    /// <param name="orderBy">The property name to order the results by (defaults to <see cref="Product.Name" />).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="orderBy" /> does not match a valid property.</exception>
    public ProductFilterSpecification(string searchString, string orderBy = nameof(Product.Name))
    {
        // Build dynamic predicate across fields using AND semantics.
        var predicate = PredicateBuilder.New<Product>(true);
        string[] fields = [nameof(Product.Name), nameof(Product.Description)];

        foreach (var f in fields)
            predicate = predicate.DynamicAnd(b => b.With(f, FilterOperations.Contains, searchString));

        WithFilter(predicate);
        AddOrderBy(orderBy, ListSortDirection.Ascending);
    }

    #endregion
}

public class SpecFilterTests : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly IRepositorySpec _repository;

    #endregion

    #region Constructors

    public SpecFilterTests(TestDbFixture fixture)
    {
        var context = fixture.Db!;
        _repository = new RepositorySpec<TestDbContext>(context, []);
    }

    #endregion

    #region Methods

    [Fact]
    public void ProductFilterSpecification_DefaultOrder_GeneratesOrderByName()
    {
        // Arrange
        var spec = new ProductFilterSpecification("test");

        // Act
        var sql = _repository.Query(spec).ToQueryString();

        // Assert
        sql.ShouldContain("ORDER BY [p].[Name]");
    }

    [Fact]
    public void ProductFilterSpecification_InvalidOrderProperty_Throws()
    {
        // Arrange / Act / Assert
        Should.Throw<ArgumentException>(() => new ProductFilterSpecification("x", "NotAProperty"));
    }

    [Fact]
    public void ProductFilterSpecification_WithSearchAndOrder_GeneratesExpectedSql()
    {
        // Arrange
        var search = "a"; // Common letter ensures matches in random data
        var spec = new ProductFilterSpecification(search);

        // Act
        var query = _repository.Query(spec);
        var sql = query.ToQueryString();

        // Assert
        sql.ShouldContain("[p].[Name] LIKE");
        sql.ShouldContain("[p].[Description] LIKE");
        sql.ShouldContain("ORDER BY [p].[Name]");
    }

    #endregion
}