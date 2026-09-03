// <copyright file="OrderingWindowTrackingTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.ComponentModel;
using DKNet.EfCore.Specifications.Extensions;
using Microsoft.Data.Sqlite;

namespace EfCore.Specifications.Tests.Extensions;

/// <summary>
///     Tests for DRK-582: declared-sequence mixed-direction ordering, result-window (Skip/Take), and
///     read-only (AsNoTracking) specification behaviour, plus the Skip/Take guard clauses.
/// </summary>
public class OrderingWindowTrackingTests : IAsyncLifetime
{
    #region Fields

    private SqliteConnection _connection = null!;
    private TestDbContext _context = null!;
    private IRepositorySpec _repository = null!;
    private int _categoryOneId;
    private int _categoryTwoId;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection).Options;
        _context = new TestDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new RepositorySpec<TestDbContext>(_context, (IServiceProvider?)null);

        var catA = new Category { Name = "Cat-A" };
        var catB = new Category { Name = "Cat-B" };
        _context.Categories.AddRange(catA, catB);
        await _context.SaveChangesAsync();
        _categoryOneId = catA.Id;
        _categoryTwoId = catB.Id;
    }

    [Fact]
    public async Task ApplySpecs_MixedDirectionOrdering_AppliesClausesInDeclaredSequence()
    {
        // Arrange: two products per category so the middle (descending "revenue") clause has ties to
        // break within each category. If the descending clause were demoted to last (the old
        // two-phase, direction-segregated behaviour) then Name — declared after Price — would decide
        // the order within each category instead, giving the opposite result asserted below.
        var highRevenueA = new Product { Name = "B-High", Price = 50m, CategoryId = _categoryOneId };
        var lowRevenueA = new Product { Name = "A-Low", Price = 30m, CategoryId = _categoryOneId };
        var highRevenueB = new Product { Name = "D-High", Price = 80m, CategoryId = _categoryTwoId };
        var lowRevenueB = new Product { Name = "C-Low", Price = 20m, CategoryId = _categoryTwoId };
        _context.Products.AddRange(lowRevenueA, highRevenueA, lowRevenueB, highRevenueB);
        await _context.SaveChangesAsync();

        var spec = new MixedOrderSpecification();

        // Act
        var query = _repository.Query(spec);
        var sql = query.ToQueryString();
        var results = await query.ToListAsync();

        // Assert: within the ORDER BY clause itself, columns appear as CategoryId, then Price DESC,
        // then Name — in that declared sequence, not grouped by direction. (Column order in the
        // SELECT list follows property-declaration order, so the check must be scoped to ORDER BY.)
        var orderByIndex = sql.IndexOf("ORDER BY", StringComparison.Ordinal);
        orderByIndex.ShouldBeGreaterThanOrEqualTo(0);
        var orderByClause = sql[orderByIndex..];
        var categoryPos = orderByClause.IndexOf("CategoryId", StringComparison.Ordinal);
        var pricePos = orderByClause.IndexOf("Price", StringComparison.Ordinal);
        var namePos = orderByClause.IndexOf("Name", StringComparison.Ordinal);
        categoryPos.ShouldBeGreaterThanOrEqualTo(0);
        categoryPos.ShouldBeLessThan(pricePos);
        pricePos.ShouldBeLessThan(namePos);

        // Assert: materialised rows follow the same declared sequence — each category's products
        // ordered by descending price, not by name.
        results.Select(p => p.Name).ShouldBe(["B-High", "A-Low", "D-High", "C-Low"]);
    }

    [Fact]
    public async Task ApplySpecs_ResultWindow_SkipTenTakeFive_ReturnsFiveStartingAtEleventh()
    {
        // Arrange: 20 products ordered by Id, so the window is deterministic.
        var products = Enumerable.Range(1, 20)
            .Select(i => new Product { Name = $"Product-{i:00}", Price = i, CategoryId = _categoryOneId })
            .ToList();
        _context.Products.AddRange(products);
        await _context.SaveChangesAsync();

        var spec = new WindowedOrderSpecification();

        // Act
        var results = await _repository.Query(spec).ToListAsync();

        // Assert: 5 rows, matching the 11th through 15th of the 20 seeded (index 10..14, 0-based).
        results.Count.ShouldBe(5);
        results.Select(p => p.Name).ShouldBe(products.Skip(10).Take(5).Select(p => p.Name));
    }

    [Fact]
    public async Task ApplySpecs_ReadOnlySpecification_MutatingReturnedEntity_ProducesNoPendingChanges()
    {
        // Arrange
        var product = new Product { Name = "Untracked", Price = 10m, CategoryId = _categoryOneId };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var spec = new ReadOnlySpecification();

        // Act: load via the read-only (AsNoTracking) specification, then mutate the returned instance.
        var loaded = await _repository.Query(spec).SingleAsync(p => p.Id == product.Id);
        loaded.Price = 999m;

        // Assert: the entity was never attached to the change tracker, so mutating it in memory
        // produces no pending changes, and SaveChanges persists nothing.
        _context.ChangeTracker.HasChanges().ShouldBeFalse();
        var affected = await _context.SaveChangesAsync();
        affected.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Skip_WithNonPositiveCount_ThrowsArgumentOutOfRangeException(int count)
    {
        var spec = new WindowGuardTestSpecification();

        Should.Throw<ArgumentOutOfRangeException>(() => spec.SkipPublic(count));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Take_WithNonPositiveCount_ThrowsArgumentOutOfRangeException(int count)
    {
        var spec = new WindowGuardTestSpecification();

        Should.Throw<ArgumentOutOfRangeException>(() => spec.TakePublic(count));
    }

    [Fact]
    public void Constructor_FromSameKindSpecification_CarriesDeclaredSequenceAndWindowState()
    {
        // Arrange
        var source = new SourceSpecification();

        // Act
        var copy = new CopySpecification(source);

        // Assert: declared order, skip/take, and read-only state all carry over unchanged for a
        // same-kind copy.
        copy.OrderByClauses.Count.ShouldBe(2);
        copy.OrderByClauses[0].Direction.ShouldBe(ListSortDirection.Ascending);
        copy.OrderByClauses[1].Direction.ShouldBe(ListSortDirection.Descending);
        copy.SkipCount.ShouldBe(5);
        copy.TakeCount.ShouldBe(10);
        copy.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_FromForeignSpecification_CarriesNoOrdering()
    {
        // Arrange: foreign ISpecification implementations (not deriving from Specification<TEntity>) are
        // not supported. The copy constructor no longer synthesizes a declared sequence for them — a
        // foreign source simply contributes no ordering, skip/take, or read-only state to the copy.
        var foreign = new ForeignSpecification();

        // Act
        var copy = new CopySpecification(foreign);

        // Assert
        copy.OrderByClauses.ShouldBeEmpty();
    }

    #endregion

    /// <summary>A foreign <see cref="ISpecification{TEntity}" /> that is not a <see cref="Specification{TEntity}" />.</summary>
    private sealed class ForeignSpecification : ISpecification<Product>
    {
        public bool IsIgnoreQueryFilters => false;
        public Expression<Func<Product, bool>>? FilterQuery => null;
        public IReadOnlyCollection<Expression<Func<Product, object?>>> IncludeQueries => [];
        public IReadOnlyCollection<Func<IQueryable<Product>, IQueryable<Product>>> IncludeBuilders => [];
    }

    /// <summary>Copies whatever <see cref="ISpecification{Product}" /> it is given.</summary>
    private sealed class CopySpecification(ISpecification<Product> source) : Specification<Product>(source);

    /// <summary>A same-kind source specification declaring a mixed-direction sequence, a window, and read-only.</summary>
    private sealed class SourceSpecification : Specification<Product>
    {
        public SourceSpecification()
        {
            AddOrderBy(p => p.CategoryId);
            AddOrderByDescending(p => p.Price);
            Skip(5);
            Take(10);
            AsNoTracking();
        }
    }

    private sealed class MixedOrderSpecification : Specification<Product>
    {
        public MixedOrderSpecification()
        {
            AddOrderBy(p => p.CategoryId);
            AddOrderByDescending(p => p.Price);
            AddOrderBy(p => p.Name);
        }
    }

    private sealed class WindowedOrderSpecification : Specification<Product>
    {
        public WindowedOrderSpecification()
        {
            AddOrderBy(p => p.Id);
            Skip(10);
            Take(5);
        }
    }

    private sealed class ReadOnlySpecification : Specification<Product>
    {
        public ReadOnlySpecification()
        {
            AsNoTracking();
        }
    }

    /// <summary>Exposes the protected Skip/Take guard clauses for direct testing.</summary>
    private sealed class WindowGuardTestSpecification : Specification<Product>
    {
        public void SkipPublic(int count) => Skip(count);
        public void TakePublic(int count) => Take(count);
    }
}
