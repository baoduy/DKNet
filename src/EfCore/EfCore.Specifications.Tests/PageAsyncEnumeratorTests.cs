// <copyright file="PageAsyncEnumeratorTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Specifications.Extensions;
using Mapster;
using MapsterMapper;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Tests for asynchronous page enumeration using <see cref="PageAsyncEnumeratorExtensions" />.
/// </summary>
public class PageAsyncEnumeratorTests : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context;
    private readonly IRepositorySpec _repository;

    #endregion

    #region Constructors

    public PageAsyncEnumeratorTests(TestDbFixture fixture)
    {
        _context = fixture.Db!;

        // Minimal mapper configuration for repository usage
        var config = new TypeAdapterConfig();
        config.NewConfig<Product, ProductDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.FullDescription, src => $"{src.Name} - {src.Description}");
        _repository = new RepositorySpec<TestDbContext>(_context, [new Mapper(config)]);
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Verifies cancellation during repository enumeration stops iteration and throws.
    /// </summary>
    [Fact]
    public async Task RepositoryToPageEnumerable_WithCancellation_ShouldCancelEnumeration()
    {
        // Arrange
        var spec = new OrderedProductsSpec();
        var cts = new CancellationTokenSource();
        var enumerated = new List<Product>();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var p in _repository.ToPageEnumerable(spec).WithCancellation(cts.Token))
            {
                enumerated.Add(p);
                cts.Cancel(); // cancel after first item
            }
        });

        enumerated.Count.ShouldBe(1); // Only first item captured
    }

    /// <summary>
    ///     Uses an ordered specification through repository to confirm ordering and materialization.
    /// </summary>
    [Fact]
    public async Task RepositoryToPageEnumerable_WithOrderingSpecification_ShouldReturnItems()
    {
        // Arrange
        var spec = new OrderedProductsSpec();

        // Act
        var results = new List<Product>();
        await foreach (var p in _repository.ToPageEnumerable(spec)) results.Add(p);

        // Assert
        results.Count.ShouldBeGreaterThan(0);
        results.Select(p => p.Name).ShouldBe(results.Select(p => p.Name).OrderBy(n => n).ToList());
    }

    /// <summary>
    ///     Verifies that lack of ordering in specification results in NotSupportedException for entities.
    /// </summary>
    [Fact]
    public void RepositoryToPageEnumerable_WithoutOrderingSpecification_ShouldThrowNotSupported()
    {
        // Arrange
        var spec = new ActiveProductsUnorderedSpec();

        // Act & Assert
        Should.Throw<NotSupportedException>(() => _repository.ToPageEnumerable(spec));
    }

    /// <summary>
    ///     Enumerates projected DTOs from repository ensuring mapping and paging work together.
    /// </summary>
    [Fact]
    public async Task RepositoryToPageEnumerable_WithProjection_ShouldReturnProjectedDtos()
    {
        // Arrange
        var spec = new OrderedProductsSpec();

        // Act
        var dtos = new List<ProductDto>();
        await foreach (var dto in _repository.ToPageEnumerable<Product, ProductDto>(spec)) dtos.Add(dto);

        // Assert
        dtos.Count.ShouldBeGreaterThan(0);
        dtos.ShouldAllBe(d => !string.IsNullOrEmpty(d.FullDescription));
    }

    /// <summary>
    ///     Verifies unordered specification for projection also throws NotSupportedException.
    /// </summary>
    [Fact]
    public void RepositoryToPageEnumerableProjection_WithoutOrderingSpecification_ShouldThrowNotSupported()
    {
        // Arrange
        var spec = new ActiveProductsUnorderedSpec();

        // Act & Assert
        Should.Throw<NotSupportedException>(() => _repository.ToPageEnumerable<Product, ProductDto>(spec));
    }

    /// <summary>
    ///     Cancels enumeration after a few items mid-stream ensuring partial collection and proper exception.
    /// </summary>
    [Fact]
    public async Task ToPageEnumerable_MidEnumerationCancellation_ShouldStopEarly()
    {
        // Arrange
        var ordered = _context.Products.OrderBy(p => p.Id);
        var cts = new CancellationTokenSource();
        var collected = new List<Product>();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var p in ordered.ToPageEnumerable(1).WithCancellation(cts.Token))
            {
                collected.Add(p);
                if (collected.Count == 10) cts.Cancel();
            }
        });

        collected.Count.ShouldBe(10);
    }

    /// <summary>
    ///     Uses the default page size (100) to enumerate all results in a single batch for seeded dataset (20 items).
    /// </summary>
    [Fact]
    public async Task ToPageEnumerable_WithDefaultPageSize_ShouldReturnAllInSingleBatch()
    {
        // Arrange
        var ordered = _context.Products.OrderBy(p => p.Id);
        var total = await _context.Products.CountAsync();

        // Act
        var results = new List<Product>();
        await foreach (var p in ordered.ToPageEnumerable()) results.Add(p);

        // Assert
        results.Count.ShouldBe(total);
    }

    /// <summary>
    ///     Verifies invalid page sizes throw appropriate argument exceptions.
    /// </summary>
    [Fact]
    public void ToPageEnumerable_WithInvalidPageSize_ShouldThrow()
    {
        // Arrange
        var ordered = _context.Products.OrderBy(p => p.Id);

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => ordered.ToPageEnumerable(0));
        Should.Throw<ArgumentOutOfRangeException>(() => ordered.ToPageEnumerable(-5));
    }

    /// <summary>
    ///     Ensures large page size greater than total count returns a single batch of all items.
    /// </summary>
    [Fact]
    public async Task ToPageEnumerable_WithLargePageSize_ShouldReturnAllInSingleIteration()
    {
        // Arrange
        var total = await _context.Products.CountAsync();
        var ordered = _context.Products.OrderBy(p => p.Id);
        var pageSize = total + 10; // Larger than total ensures single page internally

        // Act
        var results = new List<Product>();
        await foreach (var p in ordered.ToPageEnumerable(pageSize)) results.Add(p);

        // Assert
        results.Count.ShouldBe(total);
    }

    /// <summary>
    ///     Cancels enumeration before starting to verify OperationCanceledException propagation.
    /// </summary>
    [Fact]
    public async Task ToPageEnumerable_WithPreCancelledToken_ShouldThrowOperationCanceled()
    {
        // Arrange
        var ordered = _context.Products.OrderBy(p => p.Id);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var collected = new List<Product>();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var p in ordered.ToPageEnumerable(1).WithCancellation(cts.Token)) collected.Add(p);
        });
        collected.ShouldBeEmpty();
    }

    /// <summary>
    ///     Uses page size of one to ensure each element is yielded exactly once without duplication.
    /// </summary>
    [Fact]
    public async Task ToPageEnumerable_WithSingleItemPageSize_ShouldReturnAllDistinctItems()
    {
        // Arrange
        const int pageSize = 1; // Most granular paging
        var ordered = _context.Products.OrderBy(p => p.Id);
        var total = await _context.Products.CountAsync();

        // Act
        var results = new List<Product>();
        await foreach (var p in ordered.ToPageEnumerable(pageSize)) results.Add(p);

        // Assert
        results.Count.ShouldBe(total);
        results.Select(p => p.Id).Distinct().Count().ShouldBe(total); // No duplicates
    }

    /// <summary>
    ///     Enumerates all products with a small page size and verifies full ordered materialization.
    /// </summary>
    [Fact]
    public async Task ToPageEnumerable_WithSmallPageSize_ShouldReturnAllItemsInOrder()
    {
        // Arrange
        const int pageSize = 5; // Force multiple pages (total seeded = 20)
        var ordered = _context.Products.OrderBy(p => p.Id);
        var total = await _context.Products.CountAsync();

        // Act
        var results = new List<Product>();
        await foreach (var p in ordered.ToPageEnumerable(pageSize)) results.Add(p);

        // Assert
        results.Count.ShouldBe(total);
        results.Select(p => p.Id).ShouldBe(results.Select(p => p.Id).OrderBy(id => id).ToList());
        results.ShouldAllBe(p => p.Id > 0);
    }

    #endregion

    private class ActiveProductsUnorderedSpec : Specification<Product>
    {
        #region Constructors

        public ActiveProductsUnorderedSpec()
        {
            WithFilter(p => p.IsActive);
            // Intentionally no ordering to trigger NotSupportedException
        }

        #endregion
    }

    private class OrderedProductsSpec : Specification<Product>
    {
        #region Constructors

        public OrderedProductsSpec()
        {
            WithFilter(p => p.Id > 0); // All seeded products
            AddOrderBy(p => p.Name); // Required ordering
        }

        #endregion
    }
}