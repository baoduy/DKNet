using Mapster;
using MapsterMapper;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Tests for the SpecRepoExtensions methods
/// </summary>
public class SpecRepoExtensionsTests : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context;
    private readonly IRepositorySpec _repository;

    #endregion

    #region Constructors

    public SpecRepoExtensionsTests(TestDbFixture fixture)
    {
        _context = fixture.Db!;

        // Setup Mapster
        var config = new TypeAdapterConfig();
        config.NewConfig<Product, ProductDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.FullDescription, src => $"{src.Name} - {src.Description}");
        IMapper mapper = new Mapper(config);

        _repository = new RepositorySpec<TestDbContext>(_context, [mapper]);
    }

    #endregion

    #region Methods

    [Fact]
    public async Task AnyAsync_WithMatchingEntities_ShouldReturnTrue()
    {
        // Arrange
        var spec = new ActiveProductsSpecification();

        // Act
        var result = await _repository.AnyAsync(spec);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AnyAsync_WithNoMatches_ShouldReturnFalse()
    {
        // Arrange
        var spec = new ProductByNameSpecification("NonExistentProduct12345");

        // Act
        var result = await _repository.AnyAsync(spec);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AnyAsync_WithSpecificCondition_ShouldReturnCorrectResult()
    {
        // Arrange
        var spec = new ExpensiveProductsSpecification(500m);

        // Act
        var result = await _repository.AnyAsync(spec);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CountAsync_WithAllEntities_ShouldReturnTotalCount()
    {
        // Arrange
        var spec = new AllProductsSpecification();

        // Act
        var result = await _repository.CountAsync(spec);

        // Assert
        result.ShouldBe(20); // From fixture
    }

    [Fact]
    public async Task CountAsync_WithMatchingEntities_ShouldReturnCount()
    {
        // Arrange
        var spec = new ActiveProductsSpecification();

        // Act
        var result = await _repository.CountAsync(spec);

        // Assert
        result.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CountAsync_WithNoMatches_ShouldReturnZero()
    {
        // Arrange
        var spec = new ProductByNameSpecification("NonExistentProduct12345");

        // Act
        var result = await _repository.CountAsync(spec);

        // Assert
        result.ShouldBe(0);
    }


    [Fact]
    public async Task FirstAsync_WithMatchingEntity_ShouldReturnEntity()
    {
        // Arrange
        var spec = new ActiveProductsSpecification();

        // Act
        var result = await _repository.FirstAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task FirstAsync_WithNoMatch_ShouldThrowException()
    {
        // Arrange
        var spec = new ProductByNameSpecification("NonExistentProduct12345");

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () => { await _repository.FirstAsync(spec); });
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithMatchingEntity_ShouldReturnEntity()
    {
        // Arrange
        var firstProduct = _context.Products.First();
        var spec = new ProductByNameSpecification(firstProduct.Name);

        // Act
        var result = await _repository.FirstOrDefaultAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(firstProduct.Name);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithNoMatch_ShouldReturnNull()
    {
        // Arrange
        var spec = new ProductByNameSpecification("NonExistentProduct12345");

        // Act
        var result = await _repository.FirstOrDefaultAsync(spec);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithProjection_ShouldReturnProjectedModel()
    {
        // Arrange
        var firstProduct = _context.Products.First();
        var spec = new ProductByNameSpecification(firstProduct.Name);

        // Act
        var result = await _repository.FirstOrDefaultAsync<Product, ProductDto>(spec);

        // Assert
        result.ShouldNotBeNull();
        result.FullDescription.ShouldContain(firstProduct.Name);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithProjectionNoMatch_ShouldReturnNull()
    {
        // Arrange
        var spec = new ProductByNameSpecification("NonExistentProduct12345");

        // Act
        var result = await _repository.FirstOrDefaultAsync<Product, ProductDto>(spec);

        // Assert
        result.ShouldBeNull();
    }


    [Fact]
    public async Task ToListAsync_WithCancellation_ShouldRespectToken()
    {
        // Arrange
        var spec = new ActiveProductsSpecification();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await _repository.ToListAsync(spec, cts.Token);
        });
    }

    [Fact]
    public async Task ToListAsync_WithEmptyResult_ShouldReturnEmptyList()
    {
        // Arrange
        var spec = new ProductByNameSpecification("NonExistentProduct12345");

        // Act
        var result = await _repository.ToListAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ToListAsync_WithProjection_ShouldReturnListOfModels()
    {
        // Arrange
        var spec = new ActiveProductsSpecification();

        // Act
        var result = await _repository.ToListAsync<Product, ProductDto>(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThan(0);
        result.ShouldAllBe(dto => !string.IsNullOrEmpty(dto.FullDescription));
    }

    [Fact]
    public async Task ToListAsync_WithSpecification_ShouldReturnListOfEntities()
    {
        // Arrange
        var spec = new ActiveProductsSpecification();

        // Act
        var result = await _repository.ToListAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThan(0);
        result.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public async Task ToPagedListAsync_WithEmptyResult_ShouldReturnEmptyPagedList()
    {
        // Arrange
        var spec = new ProductByNameSpecification("NonExistentProduct12345");

        // Act
        var result = await _repository.ToPagedListAsync(spec, 1, 10);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        result.PageCount.ShouldBe(0);
        result.TotalItemCount.ShouldBe(0);
    }

    [Fact]
    public async Task ToPagedListAsync_WithEntities_ShouldReturnPagedList()
    {
        // Arrange
        var spec = new AllProductsSpecification();

        // Act
        var result = await _repository.ToPagedListAsync(spec, 1, 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        result.PageCount.ShouldBe(4); // 20 products / 5 per page
        result.PageNumber.ShouldBe(1);
        result.PageSize.ShouldBe(5);
        result.TotalItemCount.ShouldBe(20);
        result.HasNextPage.ShouldBeTrue();
        result.HasPreviousPage.ShouldBeFalse();
    }

    [Fact]
    public async Task ToPagedListAsync_WithFilteredSpec_ShouldReturnFilteredPagedList()
    {
        // Arrange
        var spec = new ActiveProductsSpecification();

        // Act
        var result = await _repository.ToPagedListAsync(spec, 1, 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeLessThanOrEqualTo(5);
        result.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public async Task ToPagedListAsync_WithLastPage_ShouldReturnPartialPage()
    {
        // Arrange
        var spec = new AllProductsSpecification();

        // Act
        var result = await _repository.ToPagedListAsync(spec, 4, 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        result.PageNumber.ShouldBe(4);
        result.HasNextPage.ShouldBeFalse();
        result.HasPreviousPage.ShouldBeTrue();
        result.IsLastPage.ShouldBeTrue();
    }

    [Fact]
    public async Task ToPagedListAsync_WithProjection_ShouldReturnPagedListOfModels()
    {
        // Arrange
        var spec = new AllProductsSpecification();

        // Act
        var result = await _repository.ToPagedListAsync<Product, ProductDto>(spec, 1, 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        result.PageCount.ShouldBe(4);
        result.TotalItemCount.ShouldBe(20);
        result.ShouldAllBe(dto => !string.IsNullOrEmpty(dto.FullDescription));
    }

    [Fact]
    public async Task ToPagedListAsync_WithSecondPage_ShouldReturnCorrectPage()
    {
        // Arrange
        var spec = new AllProductsSpecification();

        // Act
        var result = await _repository.ToPagedListAsync(spec, 2, 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        result.PageNumber.ShouldBe(2);
        result.HasNextPage.ShouldBeTrue();
        result.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public async Task ToPageEnumerable_WithEmptyResult_ShouldReturnEmptyEnumerable()
    {
        // Arrange
        var spec = new ProductByNameSpecification("NonExistentProduct12345");

        // Act
        var enumerable = _repository.ToPageEnumerable(spec);
        var result = new List<Product>();
        await foreach (var product in enumerable) result.Add(product);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ToPageEnumerable_WithEntities_ShouldReturnAsyncEnumerable()
    {
        // Arrange
        var spec = new ActiveProductsSpecification();

        // Act
        var enumerable = _repository.ToPageEnumerable(spec);
        var result = new List<Product>();
        await foreach (var product in enumerable) result.Add(product);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThan(0);
        result.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public async Task ToPageEnumerable_WithProjection_ShouldReturnAsyncEnumerableOfModels()
    {
        // Arrange
        var spec = new ActiveProductsSpecification();

        // Act
        var enumerable = _repository.ToPageEnumerable<Product, ProductDto>(spec);
        var result = new List<ProductDto>();
        await foreach (var dto in enumerable) result.Add(dto);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThan(0);
        result.ShouldAllBe(dto => !string.IsNullOrEmpty(dto.FullDescription));
    }

    #endregion

    private class ActiveProductsSpecification : Specification<Product>
    {
        #region Constructors

        public ActiveProductsSpecification()
        {
            WithFilter(p => p.IsActive);
            AddOrderBy(p => p.Name);
        }

        #endregion
    }

    private class AllProductsSpecification : Specification<Product>
    {
        #region Constructors

        public AllProductsSpecification()
        {
            AddOrderBy(p => p.Id);
        }

        #endregion
    }

    private class ExpensiveProductsSpecification : Specification<Product>
    {
        #region Constructors

        public ExpensiveProductsSpecification(decimal minPrice)
        {
            WithFilter(p => p.Price >= minPrice);
        }

        #endregion
    }

    private class ProductByNameSpecification : Specification<Product>
    {
        #region Constructors

        public ProductByNameSpecification(string name)
        {
            WithFilter(p => p.Name == name);
        }

        #endregion
    }
}