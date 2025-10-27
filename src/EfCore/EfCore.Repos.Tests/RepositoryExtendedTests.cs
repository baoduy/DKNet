using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using Mapster;
using MapsterMapper;

namespace EfCore.Repos.Tests;

public class RepositoryExtendedTests : IAsyncLifetime
{
    #region Fields

    private TestDbContext? _dbContext;
    private IReadRepository<TestEntity>? _readRepository;
    private IRepository<TestEntity>? _repository;

    #endregion

    #region Methods

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        // Act
        var count = await _repository!.CountAsync(e => e.Age > 25);

        // Assert
        count.ShouldBe(2);
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.CloseConnectionAsync();
            await _dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task ExistsAsync_WhenExists_ReturnsTrue()
    {
        // Act
        var exists = await _repository!.ExistsAsync(e => e.Name == "John");

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotExists_ReturnsFalse()
    {
        // Act
        var exists = await _repository!.ExistsAsync(e => e.Name == "NonExistent");

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task FindAsync_WithFilter_ReturnsEntity()
    {
        // Act
        var entity = await _repository!.FindAsync(e => e.Name == "Jane");

        // Assert
        entity.ShouldNotBeNull();
        entity.Age.ShouldBe(25);
    }

    [Fact]
    public async Task FindAsync_WithInvalidKey_ReturnsNull()
    {
        // Act
        var entity = await _repository!.FindAsync(999);

        // Assert
        entity.ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_WithKeyValue_ReturnsEntity()
    {
        // Act
        var entity = await _repository!.FindAsync(1);

        // Assert
        entity.ShouldNotBeNull();
        entity.Name.ShouldBe("John");
    }

    [Fact]
    public async Task FindAsync_WithKeyValues_ReturnsEntity()
    {
        // Act
        var entity = await _repository!.FindAsync(new object[] { 1 });

        // Assert
        entity.ShouldNotBeNull();
        entity.Name.ShouldBe("John");
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new TestDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        _repository = new Repository<TestEntity>(_dbContext);
        _readRepository = new ReadRepository<TestEntity>(_dbContext);

        // Seed some data
        _dbContext.TestEntities.AddRange(
            new TestEntity { Id = 1, Name = "John", Age = 30 },
            new TestEntity { Id = 2, Name = "Jane", Age = 25 },
            new TestEntity { Id = 3, Name = "Bob", Age = 35 }
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public void Query_ReturnsQueryable()
    {
        // Act
        var query = _repository!.Query();

        // Assert
        query.ShouldNotBeNull();
        query.ShouldBeAssignableTo<IQueryable<TestEntity>>();
    }

    [Fact]
    public void Query_WithFilter_ReturnsFilteredQueryable()
    {
        // Act
        var query = _repository!.Query(e => e.Age > 25);

        // Assert
        query.ShouldNotBeNull();
        query.Count().ShouldBe(2);
    }

    [Fact]
    public void Query_WithMapper_ReturnsProjectedQueryable()
    {
        // Arrange
        var config = new TypeAdapterConfig();
        config.NewConfig<TestEntity, TestEntityDto>();
        var mapper = new Mapper(config);
        var mappers = new List<IMapper> { mapper };
        var repo = new Repository<TestEntity>(_dbContext!, mappers);

        // Act
        var query = repo.Query<TestEntityDto>(e => e.Age > 25);

        // Assert
        query.ShouldNotBeNull();
        query.Count().ShouldBe(2);
    }

    [Fact]
    public void Query_WithMapperAndNoMapper_ThrowsException()
    {
        // Arrange
        var repo = new Repository<TestEntity>(_dbContext!);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => { repo.Query<TestEntityDto>(e => e.Age > 25); });
    }

    [Fact]
    public async Task ReadRepository_CountAsync_ReturnsCorrectCount()
    {
        // Act
        var count = await _readRepository!.CountAsync(e => e.Age <= 30);

        // Assert
        count.ShouldBe(2);
    }

    [Fact]
    public async Task ReadRepository_ExistsAsync_WhenExists_ReturnsTrue()
    {
        // Act
        var exists = await _readRepository!.ExistsAsync(e => e.Age == 25);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadRepository_ExistsAsync_WhenNotExists_ReturnsFalse()
    {
        // Act
        var exists = await _readRepository!.ExistsAsync(e => e.Age == 100);

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadRepository_FindAsync_WithFilter_ReturnsEntity()
    {
        // Act
        var entity = await _readRepository!.FindAsync(e => e.Name == "Bob");

        // Assert
        entity.ShouldNotBeNull();
        entity.Age.ShouldBe(35);
    }

    [Fact]
    public async Task ReadRepository_FindAsync_WithKeyValue_ReturnsEntity()
    {
        // Act
        var entity = await _readRepository!.FindAsync(1);

        // Assert
        entity.ShouldNotBeNull();
        entity.Name.ShouldBe("John");
    }

    [Fact]
    public async Task ReadRepository_FindAsync_WithKeyValues_ReturnsEntity()
    {
        // Act
        var entity = await _readRepository!.FindAsync(new object[] { 2 });

        // Assert
        entity.ShouldNotBeNull();
        entity.Name.ShouldBe("Jane");
    }

    [Fact]
    public void ReadRepository_Query_ReturnsNoTrackingQueryable()
    {
        // Act
        var query = _readRepository!.Query();

        // Assert
        query.ShouldNotBeNull();
        query.ShouldBeAssignableTo<IQueryable<TestEntity>>();
    }

    [Fact]
    public void ReadRepository_Query_WithFilter_ReturnsFilteredQueryable()
    {
        // Act
        var query = _readRepository!.Query(e => e.Age < 30);

        // Assert
        query.ShouldNotBeNull();
        query.Count().ShouldBe(1);
    }

    [Fact]
    public void ReadRepository_Query_WithMapper_ReturnsProjectedQueryable()
    {
        // Arrange
        var config = new TypeAdapterConfig();
        config.NewConfig<TestEntity, TestEntityDto>();
        var mapper = new Mapper(config);
        var mappers = new List<IMapper> { mapper };
        var repo = new ReadRepository<TestEntity>(_dbContext!, mappers);

        // Act
        var query = repo.Query<TestEntityDto>(e => e.Age > 25);

        // Assert
        query.ShouldNotBeNull();
        query.Count().ShouldBe(2);
    }

    [Fact]
    public void ReadRepository_Query_WithMapperAndNoMapper_ThrowsException()
    {
        // Arrange
        var repo = new ReadRepository<TestEntity>(_dbContext!);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => { repo.Query<TestEntityDto>(e => e.Age > 25); });
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

        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        #endregion
    }

    public class TestEntity
    {
        #region Properties

        public int Age { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        #endregion
    }

    public class TestEntityDto
    {
        #region Properties

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        #endregion
    }
}