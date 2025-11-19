using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Comprehensive tests for the RepositorySpec implementation
/// </summary>
public class RepositorySpecTests : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context;
    private readonly IRepositorySpec _repository;

    #endregion

    #region Constructors

    public RepositorySpecTests(TestDbFixture fixture)
    {
        _context = fixture.Db!;

        var config = new TypeAdapterConfig();
        var mapper = new Mapper(config);
        _repository = new RepositorySpec<TestDbContext>(_context, [mapper]);
    }

    #endregion

    #region Methods

    [Fact]
    public async Task AddAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var product = new Product { Name = "TestProduct", Price = 100m, CategoryId = categoryId };
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await _repository.AddAsync(product, cts.Token);
        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await _repository.SaveChangesAsync(cts.Token);
        });
    }

    [Fact]
    public async Task AddAsync_WithValidEntity_ShouldAddToContext()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var product = new Product { Name = "NewProduct", Price = 99.99m, IsActive = true, CategoryId = categoryId };

        // Act
        await _repository.AddAsync(product);
        await _repository.SaveChangesAsync();

        // Assert
        var added = await _context.Products.Where(p => p.Name == "NewProduct").FirstOrDefaultAsync();
        added.ShouldNotBeNull();
        added.Price.ShouldBe(99.99m);
    }

    [Fact]
    public async Task AddRangeAsync_WithEmptyCollection_ShouldNotThrow()
    {
        // Arrange
        var products = Array.Empty<Product>();

        // Act & Assert
        await Should.NotThrowAsync(async () =>
        {
            await _repository.AddRangeAsync(products);
            await _repository.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task AddRangeAsync_WithMultipleEntities_ShouldAddAll()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var products = new[]
        {
            new Product { Name = "Product1", Price = 10m, IsActive = true, CategoryId = categoryId },
            new Product { Name = "Product2", Price = 20m, IsActive = true, CategoryId = categoryId },
            new Product { Name = "Product3", Price = 30m, IsActive = true, CategoryId = categoryId }
        };

        // Act
        await _repository.AddRangeAsync(products);
        await _repository.SaveChangesAsync();

        // Assert
        var count = await _context.Products.Where(p =>
            p.Name == "Product1" || p.Name == "Product2" || p.Name == "Product3").CountAsync();
        count.ShouldBe(3);
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldReturnTransaction()
    {
        // Act
        var transaction = await _repository.BeginTransactionAsync();

        // Assert
        transaction.ShouldNotBeNull();
        transaction.ShouldBeAssignableTo<IDbContextTransaction>();

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task BeginTransactionAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await _repository.BeginTransactionAsync(cts.Token);
        });
    }

    [Fact]
    public async Task Delete_WithExistingEntity_ShouldMarkForDeletion()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var product = new Product { Name = "ToDelete", Price = 50m, CategoryId = categoryId };
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // Act
        _repository.Delete(product);
        await _repository.SaveChangesAsync();

        // Assert
        var deleted = await _context.Products.Where(p => p.Id == product.Id).FirstOrDefaultAsync();
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteRange_WithMultipleEntities_ShouldDeleteAll()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var products = new[]
        {
            new Product { Name = "Delete1", Price = 10m, CategoryId = categoryId },
            new Product { Name = "Delete2", Price = 20m, CategoryId = categoryId }
        };
        await _context.Products.AddRangeAsync(products);
        await _context.SaveChangesAsync();

        // Act
        var ids = products.Select(u => u.Id);
        await _repository.BulkDeleteAsync<Product>(x => ids.Contains(x.Id));

        // Assert
        var count = await _context.Products.Where(p =>
            p.Name == "Delete1" || p.Name == "Delete2").CountAsync();
        count.ShouldBe(0);
    }

    [Fact]
    public async Task Entry_ModifyState_ShouldChangeEntityState()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var product = new Product { Name = "StateTest", Price = 100m, CategoryId = categoryId };
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // Act
        var entry = _repository.Entry(product);
        entry.State = EntityState.Modified;

        // Assert
        entry.State.ShouldBe(EntityState.Modified);
    }

    [Fact]
    public async Task Entry_WithTrackedEntity_ShouldReturnEntityEntry()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var product = new Product { Name = "EntryTest", Price = 100m, CategoryId = categoryId };
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // Act
        var entry = _repository.Entry(product);

        // Assert
        entry.ShouldNotBeNull();
        entry.Entity.ShouldBe(product);
        entry.State.ShouldBe(EntityState.Unchanged);
    }

    [Fact]
    public void Query_WithProjection_ShouldReturnMappedQueryable()
    {
        // Arrange
        var config = new TypeAdapterConfig();
        config.NewConfig<Product, ProductDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.FullDescription, src => $"{src.Name} - {src.Description}");
        var mapper = new Mapper(config);
        var repo = new RepositorySpec<TestDbContext>(_context, [mapper]);

        var spec = new ActiveProductsSpecification();

        // Act
        var query = repo.Query<Product, ProductDto>(spec);

        // Assert
        query.ShouldNotBeNull();
        var results = query.ToList();
        results.ShouldNotBeEmpty();
        results.ShouldAllBe(dto => !string.IsNullOrEmpty(dto.FullDescription));
    }

    [Fact]
    public void Query_WithSpecification_ShouldReturnFilteredQueryable()
    {
        // Arrange
        var spec = new ActiveProductsSpecification();

        // Act
        var query = _repository.Query(spec);

        // Assert
        query.ShouldNotBeNull();
        var results = query.ToList();
        results.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public async Task SaveChangesAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var product = new Product { Name = "CancelTest", Price = 100m, CategoryId = categoryId };
        await _repository.AddAsync(product);
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await _repository.SaveChangesAsync(cts.Token);
        });
    }

    [Fact]
    public async Task SaveChangesAsync_WithChanges_ShouldReturnNumberOfAffectedEntities()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var products = new[]
        {
            new Product { Name = "Save1", Price = 10m, CategoryId = categoryId },
            new Product { Name = "Save2", Price = 20m, CategoryId = categoryId }
        };
        await _repository.AddRangeAsync(products);

        // Act
        var result = await _repository.SaveChangesAsync();

        // Assert
        result.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoChanges_ShouldReturnZero()
    {
        // Act
        var result = await _repository.SaveChangesAsync();

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public async Task Transaction_CommitAsync_ShouldPersistChanges()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var product = new Product { Name = "TransactionTest", Price = 99m, CategoryId = categoryId };
        var transaction = await _repository.BeginTransactionAsync();

        try
        {
            // Act
            await _repository.AddAsync(product);
            await _repository.SaveChangesAsync();
            await transaction.CommitAsync();

            // Assert
            var saved = await _context.Products.Where(p => p.Name == "TransactionTest").FirstOrDefaultAsync();
            saved.ShouldNotBeNull();
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    [Fact]
    public async Task Transaction_RollbackAsync_ShouldNotPersistChanges()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var product = new Product { Name = "RollbackTest", Price = 99m, CategoryId = categoryId };
        var transaction = await _repository.BeginTransactionAsync();

        try
        {
            // Act
            await _repository.AddAsync(product);
            await _repository.SaveChangesAsync();
            await transaction.RollbackAsync();

            // Assert
            var saved = await _context.Products.Where(p => p.Name == "RollbackTest").FirstOrDefaultAsync();
            saved.ShouldBeNull();
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateAsync_WithModifiedEntity_ShouldUpdateInDatabase()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var product = new Product { Name = "Original", Price = 100m, CategoryId = categoryId };
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        _context.Entry(product).State = EntityState.Detached;

        // Act
        product.Name = "Updated";
        product.Price = 200m;
        await _repository.UpdateAsync(product);
        await _repository.SaveChangesAsync();

        // Assert
        var updated = await _context.Products.Where(p => p.Id == product.Id).FirstOrDefaultAsync();
        updated.ShouldNotBeNull();
        updated.Name.ShouldBe("Updated");
        updated.Price.ShouldBe(200m);
    }

    [Fact]
    public async Task UpdateRangeAsync_WithMultipleEntities_ShouldUpdateAll()
    {
        // Arrange
        var categoryId = _context.Categories.First().Id;
        var products = new[]
        {
            new Product { Name = "Update1", Price = 10m, CategoryId = categoryId },
            new Product { Name = "Update2", Price = 20m, CategoryId = categoryId }
        };
        await _context.Products.AddRangeAsync(products);
        await _context.SaveChangesAsync();

        foreach (var p in products) _context.Entry(p).State = EntityState.Detached;

        // Act
        products[0].Price = 15m;
        products[1].Price = 25m;
        await _repository.UpdateRangeAsync(products);
        await _repository.SaveChangesAsync();

        // Assert
        var updated1 = await _context.Products.Where(p => p.Id == products[0].Id).FirstOrDefaultAsync();
        var updated2 = await _context.Products.Where(p => p.Id == products[1].Id).FirstOrDefaultAsync();
        updated1!.Price.ShouldBe(15m);
        updated2!.Price.ShouldBe(25m);
    }

    #endregion

    private class ActiveProductsSpecification : Specification<Product>
    {
        #region Constructors

        public ActiveProductsSpecification()
        {
            WithFilter(p => p.IsActive);
        }

        #endregion
    }
}