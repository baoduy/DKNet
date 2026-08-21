// <copyright file="KeysetQueryExtensionsTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Specifications.Extensions;
using Mapster;
using MapsterMapper;
using MR.EntityFrameworkCore.KeysetPagination;

namespace EfCore.Specifications.Tests.Extensions;

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
    // DRK-628 T1 — caller-owned ordering survives on all four legacy entry points
    // (pins DRK-624 R1: same rows, same order as dev@6f8d41a for the same cursor)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Verifies that AfterKeyset (single key) only adds a WHERE predicate and leaves a caller
    ///     ordering that differs from the keyset column untouched, instead of imposing its own
    ///     ORDER BY over the keyset column.
    /// </summary>
    [Fact]
    public async Task AfterKeyset_SingleKey_CallerOrderingDiffersFromKeysetColumn_PreservesCallerOrder()
    {
        // Arrange
        var query = _context.Products.OrderBy(p => p.Name).AfterKeyset(p => p.Id, 1);
        var expected = await _context.Products
            .Where(p => p.Id > 1)
            .OrderBy(p => p.Name)
            .Select(p => p.Id)
            .ToListAsync();

        // Act
        var sql = query.ToQueryString();
        var actual = await query.Select(p => p.Id).ToListAsync();

        // Assert
        sql.ShouldContain("ORDER BY", Case.Insensitive);
        sql.ShouldContain("\"p\".\"Name\"");
        sql.ShouldNotContain("\"p\".\"Id\" ASC");
        actual.ShouldBe(expected);
    }

    /// <summary>
    ///     Verifies that BeforeKeyset (single key) only adds a WHERE predicate and leaves a caller
    ///     ordering that differs from the keyset column untouched.
    /// </summary>
    [Fact]
    public async Task BeforeKeyset_SingleKey_CallerOrderingDiffersFromKeysetColumn_PreservesCallerOrder()
    {
        // Arrange
        var allIds = await _context.Products.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();
        var cursor = allIds[allIds.Count / 2];
        var query = _context.Products.OrderBy(p => p.Name).BeforeKeyset(p => p.Id, cursor);
        var expected = await _context.Products
            .Where(p => p.Id < cursor)
            .OrderBy(p => p.Name)
            .Select(p => p.Id)
            .ToListAsync();

        // Act
        var sql = query.ToQueryString();
        var actual = await query.Select(p => p.Id).ToListAsync();

        // Assert
        sql.ShouldContain("\"p\".\"Name\"");
        actual.ShouldBe(expected);
    }

    /// <summary>
    ///     Verifies the brief's literal regression example: BeforeKeyset ordered by the same column as
    ///     the keyset returns ids ascending, not the descending order the MR-delegated implementation
    ///     produced.
    /// </summary>
    [Fact]
    public async Task BeforeKeyset_SingleKey_OrderedByKeysetColumn_ReturnsIdsAscending()
    {
        // Arrange
        var maxId = await _context.Products.MaxAsync(p => p.Id);
        var totalCount = await _context.Products.CountAsync();

        // Act
        var ids = await _context.Products
            .OrderBy(p => p.Id)
            .BeforeKeyset(p => p.Id, maxId + 1000)
            .Select(p => p.Id)
            .ToListAsync();

        // Assert
        ids.Count.ShouldBe(totalCount);
        ids.ShouldBe(ids.OrderBy(id => id).ToList());
    }

    /// <summary>
    ///     Verifies that AfterKeyset (composite key) only adds a WHERE predicate and leaves a caller
    ///     ordering that differs from both keyset columns untouched.
    /// </summary>
    [Fact]
    public async Task AfterKeyset_CompositeKey_CallerOrderingDiffersFromKeysetColumns_PreservesCallerOrder()
    {
        // Arrange
        var ordered = await _context.Products
            .OrderBy(p => p.CreatedDate)
            .ThenBy(p => p.Id)
            .Select(p => new { p.CreatedDate, p.Id })
            .ToListAsync();
        var cursorRow = ordered[ordered.Count / 2];

        var query = _context.Products
            .OrderBy(p => p.Name)
            .AfterKeyset(p => p.CreatedDate, p => p.Id, cursorRow.CreatedDate, cursorRow.Id);
        var expected = await _context.Products
            .Where(p => p.CreatedDate > cursorRow.CreatedDate ||
                        (p.CreatedDate == cursorRow.CreatedDate && p.Id > cursorRow.Id))
            .OrderBy(p => p.Name)
            .Select(p => p.Id)
            .ToListAsync();

        // Act
        var sql = query.ToQueryString();
        var actual = await query.Select(p => p.Id).ToListAsync();

        // Assert
        sql.ShouldContain("\"p\".\"Name\"");
        actual.ShouldBe(expected);
    }

    /// <summary>
    ///     Verifies that BeforeKeyset (composite key) only adds a WHERE predicate and leaves a caller
    ///     ordering that differs from both keyset columns untouched.
    /// </summary>
    [Fact]
    public async Task BeforeKeyset_CompositeKey_CallerOrderingDiffersFromKeysetColumns_PreservesCallerOrder()
    {
        // Arrange
        var ordered = await _context.Products
            .OrderBy(p => p.CreatedDate)
            .ThenBy(p => p.Id)
            .Select(p => new { p.CreatedDate, p.Id })
            .ToListAsync();
        var cursorRow = ordered[ordered.Count / 2];

        var query = _context.Products
            .OrderBy(p => p.Name)
            .BeforeKeyset(p => p.CreatedDate, p => p.Id, cursorRow.CreatedDate, cursorRow.Id);
        var expected = await _context.Products
            .Where(p => p.CreatedDate < cursorRow.CreatedDate ||
                        (p.CreatedDate == cursorRow.CreatedDate && p.Id < cursorRow.Id))
            .OrderBy(p => p.Name)
            .Select(p => p.Id)
            .ToListAsync();

        // Act
        var sql = query.ToQueryString();
        var actual = await query.Select(p => p.Id).ToListAsync();

        // Assert
        sql.ShouldContain("\"p\".\"Name\"");
        actual.ShouldBe(expected);
    }

    // ──────────────────────────────────────────────────────────────────────
    // DRK-628 T2 — key-selector contract: computed/non-settable selectors work
    // (dev-backend's ebef832 fix builds the predicate via expression trees, so there is
    // no reference object and no settable-property requirement anymore)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Verifies that AfterKeyset accepts a computed, non-settable key selector
    ///     (<c>p.Name.Length</c>) and translates it to a direct SQL predicate instead of throwing,
    ///     matching the pre-refactor contract on dev@6f8d41a.
    /// </summary>
    [Fact]
    public async Task AfterKeyset_SingleKey_ComputedKeySelector_TranslatesToPredicateInsteadOfThrowing()
    {
        // Arrange
        var query = _context.Products.AfterKeyset(p => p.Name.Length, 3);
        var expected = await _context.Products
            .Where(p => p.Name.Length > 3)
            .Select(p => p.Id)
            .ToListAsync();

        // Act
        var sql = query.ToQueryString();
        var actual = await query.Select(p => p.Id).ToListAsync();

        // Assert – no ArgumentException from a settable-property guard; SQL is a plain length comparison.
        sql.ShouldContain("length(\"p\".\"Name\")", Case.Insensitive);
        actual.ShouldBe(expected, ignoreOrder: true);
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

    // ──────────────────────────────────────────────────────────────────────
    // IQueryable.ToKeysetPageAsync – arbitrary arity, forward/backward, has-previous/has-next
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Verifies the arbitrary-arity ToKeysetPageAsync pages forward over a multi-key ordering
    ///     (category ascending, price descending, id ascending) and reports both boundary flags.
    /// </summary>
    [Fact]
    public async Task ToKeysetPageAsync_MultipleKeysMixedDirections_PagesForwardAndReportsBoundaries()
    {
        // Arrange
        var ordered = await _context.Products
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.Price)
            .ThenBy(p => p.Id)
            .ToListAsync();
        ordered.Count.ShouldBeGreaterThan(5);
        var reference = ordered[1]; // not the first row, so a previous page exists

        // Act
        var page = await _context.Products.ToKeysetPageAsync(
            b => b.Ascending(p => p.CategoryId).Descending(p => p.Price).Ascending(p => p.Id),
            pageSize: 2,
            reference: reference);

        // Assert
        page.Items.Count.ShouldBeLessThanOrEqualTo(2);
        page.Items.Select(p => p.Id).ShouldBe(ordered.Skip(2).Take(page.Items.Count).Select(p => p.Id));
        page.HasPrevious.ShouldBeTrue();
        page.HasNext.ShouldBe(ordered.Count > 2 + page.Items.Count);
    }

    /// <summary>
    ///     Verifies the arbitrary-arity ToKeysetPageAsync pages backward and that the returned page
    ///     precedes the reference row in the declared order.
    /// </summary>
    [Fact]
    public async Task ToKeysetPageAsync_Backward_ReturnsPageBeforeReference()
    {
        // Arrange
        var ordered = await _context.Products.OrderBy(p => p.Id).ToListAsync();
        ordered.Count.ShouldBeGreaterThan(3);
        var reference = ordered[3];

        // Act
        var page = await _context.Products.ToKeysetPageAsync(
            b => b.Ascending(p => p.Id),
            pageSize: 10,
            direction: KeysetPaginationDirection.Backward,
            reference: reference);

        // Assert
        page.Items.Select(p => p.Id).ShouldBe(ordered.Take(3).Select(p => p.Id));
        page.HasPrevious.ShouldBeFalse();
        page.HasNext.ShouldBeTrue();
    }

    /// <summary>
    ///     Verifies that omitting the reference (the default) returns the first page in declared order.
    /// </summary>
    [Fact]
    public async Task ToKeysetPageAsync_NoReference_ReturnsFirstPage()
    {
        // Arrange
        var totalCount = await _context.Products.CountAsync();
        var firstIds = await _context.Products.OrderBy(p => p.Id).Select(p => p.Id).Take(4).ToListAsync();

        // Act
        var page = await _context.Products.ToKeysetPageAsync(b => b.Ascending(p => p.Id), pageSize: 4);

        // Assert
        page.Items.Select(p => p.Id).ShouldBe(firstIds);
        page.HasPrevious.ShouldBeFalse();
        page.HasNext.ShouldBe(totalCount > 4);
    }

    /// <summary>
    ///     Verifies that the arbitrary-arity ToKeysetPageAsync throws when the query argument is null.
    /// </summary>
    [Fact]
    public async Task ToKeysetPageAsync_ArbitraryArity_NullQuery_ThrowsArgumentNullException()
    {
        IQueryable<Product>? nullQuery = null;
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await nullQuery!.ToKeysetPageAsync(b => b.Ascending(p => p.Id), pageSize: 1));
    }

    /// <summary>
    ///     Verifies that the arbitrary-arity ToKeysetPageAsync throws when configureKeyset is null.
    /// </summary>
    [Fact]
    public async Task ToKeysetPageAsync_ArbitraryArity_NullConfigureKeyset_ThrowsArgumentNullException()
    {
        Action<KeysetPaginationBuilder<Product>>? nullConfigure = null;
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _context.Products.ToKeysetPageAsync(nullConfigure!, pageSize: 1));
    }

    /// <summary>
    ///     Verifies that the arbitrary-arity ToKeysetPageAsync throws for zero or negative page sizes.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ToKeysetPageAsync_ArbitraryArity_InvalidPageSize_ThrowsArgumentOutOfRange(int pageSize)
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await _context.Products.ToKeysetPageAsync(b => b.Ascending(p => p.Id), pageSize));
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
