// <copyright file="KeysetQueryExtensionsTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Specifications.Extensions;
using Mapster;
using MapsterMapper;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Integration tests for <see cref="KeysetQueryExtensions" /> keyset (cursor-based) pagination.
/// </summary>
public class KeysetQueryExtensionsTests : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context;
    private readonly IRepositorySpec _repository;

    #endregion

    #region Constructors

    public KeysetQueryExtensionsTests(TestDbFixture fixture)
    {
        _context = fixture.Db!;

        var config = new TypeAdapterConfig();
        config.NewConfig<Product, ProductDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.FullDescription, src => $"{src.Name} - {src.Description}");
        _repository = new RepositorySpec<TestDbContext>(_context, new Mapper(config));
    }

    #endregion

    #region Methods

    // ──────────────────────────────────────────────────────────────────────
    // AfterKeyset – single key
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Verifies that AfterKeyset with a single key returns only entities whose key is strictly
    ///     greater than the cursor.
    /// </summary>
    [Fact]
    public async Task AfterKeyset_SingleKey_ReturnsEntitiesAfterCursor()
    {
        // Arrange
        var allIds = await _context.Products.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();
        allIds.Count.ShouldBeGreaterThan(2);
        var cursor = allIds[allIds.Count / 2]; // pick a middle cursor

        // Act
        var results = await _context.Products
            .OrderBy(p => p.Id)
            .AfterKeyset(p => p.Id, cursor)
            .ToListAsync();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.Id > cursor);
    }

    /// <summary>
    ///     Verifies that AfterKeyset with the maximum Id returns an empty result set.
    /// </summary>
    [Fact]
    public async Task AfterKeyset_SingleKey_WhenCursorIsMax_ReturnsEmpty()
    {
        // Arrange
        var maxId = await _context.Products.MaxAsync(p => p.Id);

        // Act
        var results = await _context.Products
            .OrderBy(p => p.Id)
            .AfterKeyset(p => p.Id, maxId)
            .ToListAsync();

        // Assert
        results.ShouldBeEmpty();
    }

    /// <summary>
    ///     Verifies that AfterKeyset with the minimum Id returns all entities except the first one.
    /// </summary>
    [Fact]
    public async Task AfterKeyset_SingleKey_WhenCursorIsMin_ReturnsAllButFirst()
    {
        // Arrange
        var minId = await _context.Products.MinAsync(p => p.Id);
        var totalCount = await _context.Products.CountAsync();

        // Act
        var results = await _context.Products
            .OrderBy(p => p.Id)
            .AfterKeyset(p => p.Id, minId)
            .ToListAsync();

        // Assert
        results.Count.ShouldBe(totalCount - 1);
        results.ShouldAllBe(p => p.Id > minId);
    }

    /// <summary>
    ///     Verifies that paginating through all products using AfterKeyset yields all records exactly once.
    /// </summary>
    [Fact]
    public async Task AfterKeyset_SingleKey_PaginatingForward_YieldsAllRecordsOnce()
    {
        // Arrange
        const int pageSize = 5;
        var totalCount = await _context.Products.CountAsync();
        var cursor = 0; // before all Ids (assuming Ids start at 1)
        var collected = new List<int>();

        // Act – paginate forward until no more results
        while (true)
        {
            var page = await _context.Products
                .OrderBy(p => p.Id)
                .AfterKeyset(p => p.Id, cursor)
                .Take(pageSize)
                .Select(p => p.Id)
                .ToListAsync();

            if (page.Count == 0) break;

            collected.AddRange(page);
            cursor = page[^1]; // advance cursor to last Id on this page
        }

        // Assert – each product appears exactly once and they are in order
        collected.Count.ShouldBe(totalCount);
        collected.ShouldBe(collected.OrderBy(id => id).ToList());
        collected.Distinct().Count().ShouldBe(totalCount);
    }

    // ──────────────────────────────────────────────────────────────────────
    // BeforeKeyset – single key
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Verifies that BeforeKeyset with a single key returns only entities whose key is strictly
    ///     less than the cursor.
    /// </summary>
    [Fact]
    public async Task BeforeKeyset_SingleKey_ReturnsEntitiesBeforeCursor()
    {
        // Arrange
        var allIds = await _context.Products.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();
        allIds.Count.ShouldBeGreaterThan(2);
        var cursor = allIds[allIds.Count / 2];

        // Act
        var results = await _context.Products
            .OrderByDescending(p => p.Id)
            .BeforeKeyset(p => p.Id, cursor)
            .ToListAsync();

        // Assert
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(p => p.Id < cursor);
    }

    /// <summary>
    ///     Verifies that BeforeKeyset with the minimum Id returns an empty result set.
    /// </summary>
    [Fact]
    public async Task BeforeKeyset_SingleKey_WhenCursorIsMin_ReturnsEmpty()
    {
        // Arrange
        var minId = await _context.Products.MinAsync(p => p.Id);

        // Act
        var results = await _context.Products
            .OrderByDescending(p => p.Id)
            .BeforeKeyset(p => p.Id, minId)
            .ToListAsync();

        // Assert
        results.ShouldBeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────
    // AfterKeyset – composite key (CreatedDate, Id)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Verifies composite-key AfterKeyset returns only entities strictly after the cursor pair.
    /// </summary>
    [Fact]
    public async Task AfterKeyset_CompositeKey_ReturnsEntitiesAfterCursor()
    {
        // Arrange
        var ordered = await _context.Products
            .OrderBy(p => p.CreatedDate)
            .ThenBy(p => p.Id)
            .Select(p => new { p.CreatedDate, p.Id })
            .ToListAsync();

        ordered.Count.ShouldBeGreaterThan(2);
        var cursorRow = ordered[ordered.Count / 2];

        // Act
        var results = await _context.Products
            .OrderBy(p => p.CreatedDate)
            .ThenBy(p => p.Id)
            .AfterKeyset(
                p => p.CreatedDate,
                p => p.Id,
                cursorRow.CreatedDate,
                cursorRow.Id)
            .ToListAsync();

        // Assert
        results.ShouldNotBeEmpty();
        foreach (var r in results)
        {
            (r.CreatedDate > cursorRow.CreatedDate ||
             (r.CreatedDate == cursorRow.CreatedDate && r.Id > cursorRow.Id))
                .ShouldBeTrue();
        }
    }

    /// <summary>
    ///     Verifies that paginating through all products using composite AfterKeyset yields all records exactly once.
    /// </summary>
    [Fact]
    public async Task AfterKeyset_CompositeKey_PaginatingForward_YieldsAllRecordsOnce()
    {
        // Arrange
        const int pageSize = 5;
        var totalCount = await _context.Products.CountAsync();

        var firstRow = await _context.Products
            .OrderBy(p => p.CreatedDate)
            .ThenBy(p => p.Id)
            .Select(p => new { p.CreatedDate, p.Id })
            .FirstAsync();

        // seed cursor to one step before the first row
        var cursorDate = firstRow.CreatedDate.AddTicks(-1);
        var cursorId = 0;
        var collected = new List<int>();

        // Act – paginate forward until no more results
        while (true)
        {
            var page = await _context.Products
                .OrderBy(p => p.CreatedDate)
                .ThenBy(p => p.Id)
                .AfterKeyset(p => p.CreatedDate, p => p.Id, cursorDate, cursorId)
                .Take(pageSize)
                .Select(p => new { p.CreatedDate, p.Id })
                .ToListAsync();

            if (page.Count == 0) break;

            collected.AddRange(page.Select(r => r.Id));
            cursorDate = page[^1].CreatedDate;
            cursorId = page[^1].Id;
        }

        // Assert – each product appears exactly once
        collected.Count.ShouldBe(totalCount);
        collected.Distinct().Count().ShouldBe(totalCount);
    }

    // ──────────────────────────────────────────────────────────────────────
    // BeforeKeyset – composite key
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Verifies composite-key BeforeKeyset returns only entities strictly before the cursor pair.
    /// </summary>
    [Fact]
    public async Task BeforeKeyset_CompositeKey_ReturnsEntitiesBeforeCursor()
    {
        // Arrange
        var ordered = await _context.Products
            .OrderBy(p => p.CreatedDate)
            .ThenBy(p => p.Id)
            .Select(p => new { p.CreatedDate, p.Id })
            .ToListAsync();

        ordered.Count.ShouldBeGreaterThan(2);
        var cursorRow = ordered[ordered.Count / 2];

        // Act
        var results = await _context.Products
            .OrderByDescending(p => p.CreatedDate)
            .ThenByDescending(p => p.Id)
            .BeforeKeyset(
                p => p.CreatedDate,
                p => p.Id,
                cursorRow.CreatedDate,
                cursorRow.Id)
            .ToListAsync();

        // Assert
        results.ShouldNotBeEmpty();
        foreach (var r in results)
        {
            (r.CreatedDate < cursorRow.CreatedDate ||
             (r.CreatedDate == cursorRow.CreatedDate && r.Id < cursorRow.Id))
                .ShouldBeTrue();
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Argument validation
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Verifies that AfterKeyset throws when the query argument is null.
    /// </summary>
    [Fact]
    public void AfterKeyset_SingleKey_NullQuery_ThrowsArgumentNullException()
    {
        IQueryable<Product>? nullQuery = null;
        Should.Throw<ArgumentNullException>(() =>
            nullQuery!.AfterKeyset(p => p.Id, 1));
    }

    /// <summary>
    ///     Verifies that AfterKeyset throws when the key selector is null.
    /// </summary>
    [Fact]
    public void AfterKeyset_SingleKey_NullKeySelector_ThrowsArgumentNullException()
    {
        var query = _context.Products.OrderBy(p => p.Id);
        Expression<Func<Product, int>>? nullSelector = null;
        Should.Throw<ArgumentNullException>(() =>
            query.AfterKeyset(nullSelector!, 1));
    }

    /// <summary>
    ///     Verifies that BeforeKeyset throws when the query argument is null.
    /// </summary>
    [Fact]
    public void BeforeKeyset_SingleKey_NullQuery_ThrowsArgumentNullException()
    {
        IQueryable<Product>? nullQuery = null;
        Should.Throw<ArgumentNullException>(() =>
            nullQuery!.BeforeKeyset(p => p.Id, 1));
    }

    /// <summary>
    ///     Verifies that AfterKeyset (composite) throws when any argument is null.
    /// </summary>
    [Fact]
    public void AfterKeyset_CompositeKey_NullQuery_ThrowsArgumentNullException()
    {
        IQueryable<Product>? nullQuery = null;
        Should.Throw<ArgumentNullException>(() =>
            nullQuery!.AfterKeyset(p => p.CreatedDate, p => p.Id, DateTime.UtcNow, 1));
    }

    // ──────────────────────────────────────────────────────────────────────
    // ToKeysetPageAsync via IRepositorySpec
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Verifies ToKeysetPageAsync (single key) returns expected page from repository.
    /// </summary>
    [Fact]
    public async Task ToKeysetPageAsync_SingleKey_ReturnsNextPage()
    {
        // Arrange
        const int pageSize = 3;
        var spec = new AllProductsOrderedByIdSpec();
        var allIds = await _context.Products.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();
        var cursor = allIds[0]; // cursor on first element → expect elements 2+

        // Act
        var page = await _repository.ToKeysetPageAsync(spec, p => p.Id, cursor, pageSize);

        // Assert
        page.Count.ShouldBeLessThanOrEqualTo(pageSize);
        page.ShouldAllBe(p => p.Id > cursor);
    }

    /// <summary>
    ///     Verifies ToKeysetPageAsync (composite key) returns expected page from repository.
    /// </summary>
    [Fact]
    public async Task ToKeysetPageAsync_CompositeKey_ReturnsNextPage()
    {
        // Arrange
        const int pageSize = 3;
        var spec = new AllProductsOrderedByDateAndIdSpec();

        var firstRow = await _context.Products
            .OrderBy(p => p.CreatedDate)
            .ThenBy(p => p.Id)
            .Select(p => new { p.CreatedDate, p.Id })
            .FirstAsync();

        // Act
        var page = await _repository.ToKeysetPageAsync(
            spec,
            p => p.CreatedDate,
            p => p.Id,
            firstRow.CreatedDate,
            firstRow.Id,
            pageSize);

        // Assert
        page.Count.ShouldBeLessThanOrEqualTo(pageSize);
        foreach (var r in page)
        {
            (r.CreatedDate > firstRow.CreatedDate ||
             (r.CreatedDate == firstRow.CreatedDate && r.Id > firstRow.Id))
                .ShouldBeTrue();
        }
    }

    /// <summary>
    ///     Verifies that ToKeysetPageAsync throws for an invalid page size.
    /// </summary>
    [Fact]
    public async Task ToKeysetPageAsync_InvalidPageSize_ThrowsArgumentOutOfRange()
    {
        var spec = new AllProductsOrderedByIdSpec();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await _repository.ToKeysetPageAsync(spec, p => p.Id, 0, pageSize: 0));
    }

    #endregion

    #region Nested Specifications

    private class AllProductsOrderedByIdSpec : Specification<Product>
    {
        public AllProductsOrderedByIdSpec()
        {
            AddOrderBy(p => p.Id);
        }
    }

    private class AllProductsOrderedByDateAndIdSpec : Specification<Product>
    {
        public AllProductsOrderedByDateAndIdSpec()
        {
            AddOrderBy(p => p.CreatedDate);
            AddOrderBy(p => p.Id);
        }
    }

    #endregion
}
