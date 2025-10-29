using DKNet.EfCore.Repos.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Repos.Tests;

public class SetupRepositoryTests
{
    #region Methods

    [Fact]
    public void AddGenericRepositories_MultipleCalls_DoesNotDuplicateDbContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        // Act
        services.AddGenericRepositories<TestDbContext>();
        services.AddGenericRepositories<TestDbContext>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetService<DbContext>();
        dbContext.ShouldNotBeNull();
    }

    [Fact]
    public void AddGenericRepositories_RegistersRepositories()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        // Act
        services.AddGenericRepositories<TestDbContext>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var dbContext = serviceProvider.GetService<DbContext>();
        dbContext.ShouldNotBeNull();
        dbContext.ShouldBeOfType<TestDbContext>();

        var readRepo = serviceProvider.GetService<IReadRepository<TestEntity>>();
        readRepo.ShouldNotBeNull();

        var writeRepo = serviceProvider.GetService<IWriteRepository<TestEntity>>();
        writeRepo.ShouldNotBeNull();

        var repo = serviceProvider.GetService<IRepository<TestEntity>>();
        repo.ShouldNotBeNull();
    }

    [Fact]
    public void AddGenericRepositories_WhenDbContextAlreadyRegistered_DoesNotRegisterAgain()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));
        services.AddScoped<DbContext, TestDbContext>();

        // Act
        services.AddGenericRepositories<TestDbContext>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetService<DbContext>();
        dbContext.ShouldNotBeNull();
        dbContext.ShouldBeOfType<TestDbContext>();
    }

    [Fact]
    public void AddRepoFactory_CanCreateRepositories()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContextFactory<TestDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));
        services.AddRepoFactory<TestDbContext>();

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetService<IRepositoryFactory>();

        // Assert
        factory.ShouldNotBeNull();
        var repo = factory.Create<TestEntity>();
        repo.ShouldNotBeNull();

        var readRepo = factory.CreateRead<TestEntity>();
        readRepo.ShouldNotBeNull();

        var writeRepo = factory.CreateWrite<TestEntity>();
        writeRepo.ShouldNotBeNull();
    }

    [Fact]
    public void AddRepoFactory_RegistersRepositoryFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContextFactory<TestDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        // Act
        services.AddRepoFactory<TestDbContext>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetService<IRepositoryFactory>();
        factory.ShouldNotBeNull();
    }

    #endregion

    public class TestDbContext : DbContext
    {
        #region Constructors

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        #endregion

        #region Properties

        public DbSet<TestEntity> TestEntities => this.Set<TestEntity>();

        #endregion
    }

    public class TestEntity
    {
        #region Properties

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        #endregion
    }
}