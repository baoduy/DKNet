using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace EfCore.Repos.Tests;

public class RepositoryFactoryTests
{
    #region Test Entity

    public class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        public DbSet<TestEntity> TestEntities => Set<TestEntity>();
    }

    #endregion

    #region Helper Methods

    private static IDbContextFactory<TestDbContext> CreateDbContextFactory()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite($"DataSource=:memory:")
            .Options;

        var factory = new TestDbContextFactory(options);
        
        // Initialize the database
        using var context = factory.CreateDbContext();
        context.Database.EnsureCreated();
        
        return factory;
    }

    private class TestDbContextFactory : IDbContextFactory<TestDbContext>
    {
        private readonly DbContextOptions<TestDbContext> _options;

        public TestDbContextFactory(DbContextOptions<TestDbContext> options)
        {
            _options = options;
        }

        public TestDbContext CreateDbContext()
        {
            return new TestDbContext(_options);
        }
    }

    #endregion

    #region Tests

    [Fact]
    public void Constructor_CreatesInstance()
    {
        // Arrange
        var factory = CreateDbContextFactory();

        // Act
        var repositoryFactory = new RepositoryFactory<TestDbContext>(factory);

        // Assert
        repositoryFactory.ShouldNotBeNull();
    }

    [Fact]
    public void Create_ReturnsRepository()
    {
        // Arrange
        var factory = CreateDbContextFactory();
        var repositoryFactory = new RepositoryFactory<TestDbContext>(factory);

        // Act
        var repository = repositoryFactory.Create<TestEntity>();

        // Assert
        repository.ShouldNotBeNull();
        repository.ShouldBeAssignableTo<IRepository<TestEntity>>();
    }

    [Fact]
    public void CreateRead_ReturnsReadRepository()
    {
        // Arrange
        var factory = CreateDbContextFactory();
        var repositoryFactory = new RepositoryFactory<TestDbContext>(factory);

        // Act
        var repository = repositoryFactory.CreateRead<TestEntity>();

        // Assert
        repository.ShouldNotBeNull();
        repository.ShouldBeAssignableTo<IReadRepository<TestEntity>>();
    }

    [Fact]
    public void CreateWrite_ReturnsWriteRepository()
    {
        // Arrange
        var factory = CreateDbContextFactory();
        var repositoryFactory = new RepositoryFactory<TestDbContext>(factory);

        // Act
        var repository = repositoryFactory.CreateWrite<TestEntity>();

        // Assert
        repository.ShouldNotBeNull();
        repository.ShouldBeAssignableTo<IWriteRepository<TestEntity>>();
    }

    [Fact]
    public void Dispose_DisposesDbContext()
    {
        // Arrange
        var factory = CreateDbContextFactory();
        var repositoryFactory = new RepositoryFactory<TestDbContext>(factory);

        // Act & Assert - Should not throw
        repositoryFactory.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_DisposesDbContext()
    {
        // Arrange
        var factory = CreateDbContextFactory();
        var repositoryFactory = new RepositoryFactory<TestDbContext>(factory);

        // Act & Assert - Should not throw
        await repositoryFactory.DisposeAsync();
    }

    [Fact]
    public void Create_WithMappers_ReturnsRepository()
    {
        // Arrange
        var factory = CreateDbContextFactory();
        var mappers = new List<MapsterMapper.IMapper>();
        var repositoryFactory = new RepositoryFactory<TestDbContext>(factory, mappers);

        // Act
        var repository = repositoryFactory.Create<TestEntity>();

        // Assert
        repository.ShouldNotBeNull();
        repository.ShouldBeAssignableTo<IRepository<TestEntity>>();
    }

    [Fact]
    public void CreateRead_WithMappers_ReturnsReadRepository()
    {
        // Arrange
        var factory = CreateDbContextFactory();
        var mappers = new List<MapsterMapper.IMapper>();
        var repositoryFactory = new RepositoryFactory<TestDbContext>(factory, mappers);

        // Act
        var repository = repositoryFactory.CreateRead<TestEntity>();

        // Assert
        repository.ShouldNotBeNull();
        repository.ShouldBeAssignableTo<IReadRepository<TestEntity>>();
    }

    #endregion
}
