using Mapster;
using MapsterMapper;

namespace EfCore.Specifications.Tests;

internal sealed class ActiveProductSpecification : Specification<Product>
{
    #region Constructors

    public ActiveProductSpecification()
    {
        WithFilter(p => p.IsActive);
        AddOrderBy(p => p.Name);
    }

    #endregion
}

internal sealed class ProductModelSpecification()
    : ModelSpecification<Product, ProductDto>(new ActiveProductSpecification());

internal sealed class ActiveProductModelSpecification : ModelSpecification<Product, ProductDto>
{
    #region Constructors

    public ActiveProductModelSpecification()
    {
        WithFilter(p => p.IsActive);
        AddOrderBy(p => p.Name);
    }

    #endregion
}

internal sealed class NonExistingProductModelSpecification : ModelSpecification<Product, ProductDto>
{
    #region Constructors

    public NonExistingProductModelSpecification()
    {
        WithFilter(p => p.Name == "__NO_PRODUCT__");
        AddOrderBy(p => p.Name);
    }

    #endregion
}

internal sealed class OrderedProductsModelSpecification : ModelSpecification<Product, ProductDto>
{
    #region Constructors

    public OrderedProductsModelSpecification()
    {
        AddOrderBy(p => p.Name);
        AddOrderByDescending(p => p.Id);
    }

    #endregion
}

/// <summary>
///     Tests for the SpecRepoExtensions methods
/// </summary>
public class ModelSpecRepoExtensionsTests : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly IRepositorySpec _repository;

    #endregion

    #region Constructors

    public ModelSpecRepoExtensionsTests(TestDbFixture fixture)
    {
        var context = fixture.Db!;

        // Setup Mapster
        var config = new TypeAdapterConfig();
        config.NewConfig<Product, ProductDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.FullDescription, src => $"{src.Name} - {src.Description}");
        IMapper mapper = new Mapper(config);

        _repository = new RepositorySpec<TestDbContext>(context, [mapper]);
    }

    #endregion

    #region Methods

    [Fact]
    public async Task FirstAsync_CancellationToken_CancelsOperation()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await _repository.FirstAsync(new ActiveProductModelSpecification(), cts.Token);
        });
    }

    [Fact]
    public async Task FirstAsync_ReturnsProjectedModel_WhenEntityExists()
    {
        // Act
        var result = await _repository.FirstAsync(new ProductModelSpecification());

        // Assert
        result.ShouldNotBeNull();
        result.FullDescription.ShouldNotBeNull();
    }

    [Fact]
    public async Task FirstAsync_Throws_InvalidOperation_WhenNoEntity()
    {
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await _repository.FirstAsync(new NonExistingProductModelSpecification());
        });
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ReturnsModel_WhenEntityExists()
    {
        var spec = new ActiveProductModelSpecification();
        var result = await _repository.FirstOrDefaultAsync(spec);
        result.ShouldNotBeNull();
        result.FullDescription.ShouldNotBeNull();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ReturnsNull_WhenNoEntity()
    {
        var spec = new NonExistingProductModelSpecification();
        var result = await _repository.FirstOrDefaultAsync(spec);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ToListAsync_Cancellation_Throws()
    {
        var spec = new ActiveProductModelSpecification();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await _repository.ToListAsync(spec, cts.Token);
        });
    }

    [Fact]
    public async Task ToListAsync_ReturnsEmptyList_WhenNoMatch()
    {
        var spec = new NonExistingProductModelSpecification();
        var list = await _repository.ToListAsync(spec);
        list.ShouldNotBeNull();
        list.ShouldBeEmpty();
    }

    [Fact]
    public async Task ToListAsync_ReturnsListOfModels_WhenFiltered()
    {
        var spec = new ActiveProductModelSpecification();
        var list = await _repository.ToListAsync(spec);
        list.ShouldNotBeNull();
        list.Count.ShouldBeGreaterThan(0);
        list.ShouldAllBe(p => !string.IsNullOrEmpty(p.FullDescription));
    }

    [Fact]
    public async Task ToPagedListAsync_ReturnsEmptyPagedResult_WhenNoMatch()
    {
        var spec = new NonExistingProductModelSpecification();
        var page = await _repository.ToPagedListAsync(spec, 1, 5);
        page.ShouldNotBeNull();
        page.ShouldBeEmpty();
        page.TotalItemCount.ShouldBe(0);
        page.PageCount.ShouldBe(0);
    }

    [Fact]
    public async Task ToPagedListAsync_ReturnsPagedResult_FirstPage()
    {
        var spec = new ProductModelSpecification();
        var page = await _repository.ToPagedListAsync(spec, 1, 5);
        page.ShouldNotBeNull();
        page.Count.ShouldBe(5);
        page.PageNumber.ShouldBe(1);
        page.PageSize.ShouldBe(5);
        page.TotalItemCount.ShouldBeGreaterThan(5);
        page.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public async Task ToPagedListAsync_SecondPage_ShouldReturnCorrectFlags()
    {
        var spec = new ProductModelSpecification();
        var page = await _repository.ToPagedListAsync(spec, 2, 5);
        page.ShouldNotBeNull();
        page.PageNumber.ShouldBe(2);
        page.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public async Task ToPagedListAsync_WithOrderingSpecification_ShouldApplyOrdering()
    {
        var spec = new OrderedProductsModelSpecification();
        var page = await _repository.ToPagedListAsync(spec, 1, 10);
        page.ShouldNotBeNull();
        page.Count.ShouldBeLessThanOrEqualTo(10);
        // Verify ascending by Name primary ordering
        var names = page.Select(p => p.FullDescription.Split(' ')[0]).ToList();
        names.ShouldBe(names.OrderBy(n => n).ToList());
    }

    [Fact]
    public async Task ToPageEnumerable_ReturnsAsyncEnumerable_WhenFiltered()
    {
        var spec = new ActiveProductModelSpecification();
        var result = new List<ProductDto>();
        await foreach (var dto in _repository.ToPageEnumerable(spec)) result.Add(dto);
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThan(0);
        result.ShouldAllBe(p => !string.IsNullOrEmpty(p.FullDescription));
    }

    [Fact]
    public async Task ToPageEnumerable_ReturnsEmpty_WhenNoMatch()
    {
        var spec = new NonExistingProductModelSpecification();
        var result = new List<ProductDto>();
        await foreach (var dto in _repository.ToPageEnumerable(spec)) result.Add(dto);
        result.ShouldBeEmpty();
    }

    #endregion
}