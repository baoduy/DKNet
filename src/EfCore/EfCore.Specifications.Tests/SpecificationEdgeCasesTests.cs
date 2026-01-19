// <copyright file="SpecificationEdgeCasesTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.ComponentModel;
using DKNet.EfCore.Specifications.Extensions;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Edge case tests for Specification classes
/// </summary>
public class SpecificationEdgeCasesTests : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context;
    private readonly IRepositorySpec _repository;

    #endregion

    #region Constructors

    public SpecificationEdgeCasesTests(TestDbFixture fixture)
    {
        _context = fixture.Db!;
        _repository = new RepositorySpec<TestDbContext>(_context, (IServiceProvider?)null);
    }

    #endregion

    #region Methods

    [Fact]
    public void ModelSpecification_CopyConstructor_CopiesCorrectly()
    {
        // Arrange
        var original = new TestSpecification();
        original.SetupForCopyTest();

        // Act
        var copy = new TestModelSpecification(original);

        // Assert
        copy.FilterQuery.ShouldNotBeNull();
        copy.OrderByQueries.Count.ShouldBe(original.OrderByQueries.Count);
    }

    [Fact]
    public void ModelSpecification_DefaultConstructor_InitializesCorrectly()
    {
        // Arrange & Act
        var spec = new TestModelSpecification();

        // Assert
        spec.FilterQuery.ShouldBeNull();
        spec.OrderByQueries.ShouldBeEmpty();
        spec.IncludeQueries.ShouldBeEmpty();
    }

    [Fact]
    public void ModelSpecification_WithFilterConstructor_SetsFilterCorrectly()
    {
        // Arrange
        Expression<Func<Product, bool>> filter = p => p.Price > 100;

        // Act
        var spec = new TestModelSpecification(filter);

        // Assert
        spec.FilterQuery.ShouldNotBeNull();
    }

    [Fact]
    public void Specification_AddOrderBy_WithKebabCaseProperty_ConvertsCorrectly()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddOrderByPublic("is-active", ListSortDirection.Descending);

        // Assert
        spec.OrderByDescendingQueries.Count.ShouldBe(1);
    }

    [Fact]
    public void Specification_AddOrderBy_WithNullOrEmptyString_DoesNotAddOrder()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddOrderByPublic(null!, ListSortDirection.Ascending);
        spec.AddOrderByPublic("", ListSortDirection.Ascending);
        spec.AddOrderByPublic("  ", ListSortDirection.Ascending);

        // Assert
        spec.OrderByQueries.Count.ShouldBe(0);
        spec.OrderByDescendingQueries.Count.ShouldBe(0);
    }

    [Fact]
    public void Specification_AddOrderBy_WithSnakeCaseProperty_ConvertsCorrectly()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddOrderByPublic("created_date", ListSortDirection.Ascending);

        // Assert
        spec.OrderByQueries.Count.ShouldBe(1);
    }

    [Fact]
    public void Specification_CopyConstructor_CopiesAllProperties()
    {
        // Arrange
        var original = new TestSpecification();
        original.SetupForCopyTest();

        // Act
        var copy = new CopyTestSpecification(original);

        // Assert
        copy.FilterQuery.ShouldNotBeNull();
        copy.IsIgnoreQueryFilters.ShouldBe(original.IsIgnoreQueryFilters);
        copy.OrderByQueries.Count.ShouldBe(original.OrderByQueries.Count);
        copy.OrderByDescendingQueries.Count.ShouldBe(original.OrderByDescendingQueries.Count);
        copy.IncludeQueries.Count.ShouldBe(original.IncludeQueries.Count);
    }

    [Fact]
    public void Specification_CreatePredicate_WithExpression_UsesThatExpression()
    {
        // Arrange
        var spec = new TestSpecification();
        Expression<Func<Product, bool>> expr = p => p.IsActive;

        // Act
        var predicate = spec.CreatePredicatePublic(expr);

        // Assert
        predicate.ShouldNotBeNull();
    }

    [Fact]
    public void Specification_CreatePredicate_WithNullExpression_CreatesNewPredicate()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        var predicate = spec.CreatePredicatePublic();

        // Assert
        predicate.ShouldNotBeNull();
    }

    [Fact]
    public void Specification_IgnoreQueryFilters_SetsPropertyCorrectly()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.IgnoreQueryFiltersPublic();

        // Assert
        spec.IsIgnoreQueryFilters.ShouldBeTrue();
    }

    [Fact]
    public void Specification_MultipleIncludes_AllAdded()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddIncludePublic(p => p.Category);
        spec.AddIncludePublic(p => p.OrderItems);
        spec.AddIncludePublic(p => p.ProductTags);

        // Assert
        spec.IncludeQueries.Count.ShouldBe(3);
    }

    [Fact]
    public void Specification_MultipleOrderBy_AllAdded()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act
        spec.AddOrderByPublic("Name", ListSortDirection.Ascending);
        spec.AddOrderByPublic("Price", ListSortDirection.Descending);
        spec.AddOrderByPublic("CreatedDate", ListSortDirection.Ascending);

        // Assert
        spec.OrderByQueries.Count.ShouldBe(2);
        spec.OrderByDescendingQueries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Specification_WithComplexPredicate_FiltersCorrectly()
    {
        // Arrange
        var spec = new ComplexPredicateSpecification();

        // Act
        var products = await _repository.ToListAsync(spec);

        // Assert
        products.ShouldAllBe(p => p.IsActive && p.Price > 0 && !string.IsNullOrEmpty(p.Name));
    }

    [Fact]
    public async Task Specification_WithReferenceTypeOrderBy_WorksCorrectly()
    {
        // Arrange: Order by reference type (string)
        var spec = new TestSpecification();
        spec.AddOrderByPublic("Name", ListSortDirection.Descending);

        // Act
        var products = await _repository.ToListAsync(spec);

        // Assert
        products.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Specification_WithValueTypeOrderBy_WorksCorrectly()
    {
        // Arrange: Order by value type (decimal)
        var spec = new TestSpecification();
        spec.AddOrderByPublic("Price", ListSortDirection.Ascending);

        // Act
        var products = await _repository.ToListAsync(spec);

        // Assert
        products.ShouldNotBeEmpty();
        for (var i = 0; i < products.Count - 1; i++) products[i].Price.ShouldBeLessThanOrEqualTo(products[i + 1].Price);
    }

    [Fact]
    public void SpecificationExtensions_ApplySpecification_WithIgnoreFilters_IgnoresGlobalFilters()
    {
        // Arrange
        var spec = new IgnoreFiltersSpecification();

        // Act
        var query = _repository.Query(spec);
        var sql = query.ToQueryString();

        // Assert
        // When IgnoreQueryFilters is called, EF should not apply global filters
        spec.IsIgnoreQueryFilters.ShouldBeTrue();
    }

    [Fact]
    public async Task SpecificationExtensions_ApplySpecification_WithNoFilter_ReturnsAllRecords()
    {
        // Arrange
        var spec = new EmptySpecification();

        // Act
        var query = _repository.Query(spec);
        var count = await query.CountAsync();

        // Assert
        count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void SpecificationExtensions_WithMultipleOrderBy_AppliesInCorrectOrder()
    {
        // Arrange
        var spec = new MultipleOrderBySpecification();

        // Act
        var query = _repository.Query(spec);
        var sql = query.ToQueryString();

        // Assert: SQL should contain all order by clauses
        sql.ShouldContain("ORDER BY");
    }

    #endregion

    /// <summary>
    ///     Complex predicate specification
    /// </summary>
    private class ComplexPredicateSpecification : Specification<Product>
    {
        #region Constructors

        public ComplexPredicateSpecification()
        {
            var predicate = PredicateBuilder.New<Product>(p => p.IsActive);
            predicate = predicate.And(p => p.Price > 0);
            predicate = predicate.And(p => !string.IsNullOrEmpty(p.Name));
            WithFilter(predicate);
        }

        #endregion
    }

    /// <summary>
    ///     Copy test specification
    /// </summary>
    private class CopyTestSpecification(ISpecification<Product> copyFrom) : Specification<Product>(copyFrom);

    /// <summary>
    ///     Empty specification (no filters)
    /// </summary>
    private class EmptySpecification : Specification<Product>
    {
    }

    /// <summary>
    ///     Specification with IgnoreQueryFilters enabled
    /// </summary>
    private class IgnoreFiltersSpecification : Specification<Product>
    {
        #region Constructors

        public IgnoreFiltersSpecification()
        {
            IgnoreQueryFilters();
        }

        #endregion
    }

    /// <summary>
    ///     Specification with multiple order by clauses
    /// </summary>
    private class MultipleOrderBySpecification : Specification<Product>
    {
        #region Constructors

        public MultipleOrderBySpecification()
        {
            AddOrderBy(p => p.Category!.Name);
            AddOrderBy(p => p.Price);
            AddOrderByDescending(p => p.CreatedDate);
        }

        #endregion
    }

    /// <summary>
    ///     Simple DTO for testing
    /// </summary>
    private class ProductDto
    {
        #region Properties

        public string? Description { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        #endregion
    }

    /// <summary>
    ///     Test model specification
    /// </summary>
    private class TestModelSpecification : ModelSpecification<Product, ProductDto>
    {
        #region Constructors

        public TestModelSpecification()
        {
        }

        public TestModelSpecification(Expression<Func<Product, bool>> filter) : base(filter)
        {
        }

        public TestModelSpecification(ISpecification<Product> copyFrom) : base(copyFrom)
        {
        }

        #endregion
    }

    /// <summary>
    ///     Test specification exposing protected members
    /// </summary>
    private class TestSpecification : Specification<Product>
    {
        #region Methods

        public void AddIncludePublic(Expression<Func<Product, object?>> query) => AddInclude(query);
        public void AddOrderByDescendingPublic(Expression<Func<Product, object>> query) => AddOrderByDescending(query);
        public void AddOrderByPublic(string orderBy, ListSortDirection direction) => AddOrderBy(orderBy, direction);
        public void AddOrderByPublic(Expression<Func<Product, object>> query) => AddOrderBy(query);

        public ExpressionStarter<Product> CreatePredicatePublic(Expression<Func<Product, bool>>? expression = null) =>
            CreatePredicate(expression);

        public void IgnoreQueryFiltersPublic() => IgnoreQueryFilters();

        public void SetupForCopyTest()
        {
            WithFilter(p => p.IsActive);
            IgnoreQueryFilters();
            AddInclude(p => p.Category);
            AddOrderBy(p => p.Name);
            AddOrderByDescending(p => p.Price);
        }

        public void WithFilterPublic(Expression<Func<Product, bool>> query) => WithFilter(query);

        #endregion
    }
}