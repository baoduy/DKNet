using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;

namespace EfCore.Repos.Tests;

public class RepoSpecExtensionsTests : IAsyncLifetime
{
    #region Fields

    private TestDbContext? _dbContext;
    private IReadRepository<TestEntity>? _repository;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.CloseConnectionAsync();
            await _dbContext.DisposeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new TestDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        _repository = new ReadRepository<TestEntity>(_dbContext);

        // Seed data
        _dbContext.TestEntities.AddRange(
            new TestEntity { Id = 1, Name = "Active Item 1", IsActive = true },
            new TestEntity { Id = 2, Name = "Inactive Item", IsActive = false },
            new TestEntity { Id = 3, Name = "Active Item 2", IsActive = true },
            new TestEntity { Id = 4, Name = "Another Active", IsActive = true });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public void QuerySpecs_AppliesSpecification()
    {
        // Arrange
        var spec = new ActiveEntitySpec();

        // Act
        var query = _repository!.QuerySpecs(spec);

        // Assert
        query.ShouldNotBeNull();
        query.Count().ShouldBe(3);
    }

    [Fact]
    public async Task SpecsAnyAsync_WhenExists_ReturnsTrue()
    {
        // Arrange
        var spec = new ActiveEntitySpec();

        // Act
        var result = await _repository!.SpecsAnyAsync(spec);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task SpecsAnyAsync_WhenNotExists_ReturnsFalse()
    {
        // Arrange
        var spec = new NameContainsSpec("NonExistent");

        // Act
        var result = await _repository!.SpecsAnyAsync(spec);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task SpecsCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var spec = new ActiveEntitySpec();

        // Act
        var count = await _repository!.SpecsCountAsync(spec);

        // Assert
        count.ShouldBe(3);
    }

    [Fact]
    public async Task SpecsFirstAsync_ReturnsFirstEntity()
    {
        // Arrange
        var spec = new ActiveEntitySpec();

        // Act
        var entity = await _repository!.SpecsFirstAsync(spec);

        // Assert
        entity.ShouldNotBeNull();
        entity.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task SpecsFirstOrDefaultAsync_WhenExists_ReturnsEntity()
    {
        // Arrange
        var spec = new NameContainsSpec("Item 1");

        // Act
        var entity = await _repository!.SpecsFirstOrDefaultAsync(spec);

        // Assert
        entity.ShouldNotBeNull();
        entity.Name.ShouldBe("Active Item 1");
    }

    [Fact]
    public async Task SpecsFirstOrDefaultAsync_WhenNotExists_ReturnsNull()
    {
        // Arrange
        var spec = new NameContainsSpec("DoesNotExist");

        // Act
        var entity = await _repository!.SpecsFirstOrDefaultAsync(spec);

        // Assert
        entity.ShouldBeNull();
    }

    [Fact]
    public async Task SpecsListAsync_ReturnsMatchingEntities()
    {
        // Arrange
        var spec = new ActiveEntitySpec();

        // Act
        var list = await _repository!.SpecsListAsync(spec);

        // Assert
        list.ShouldNotBeNull();
        list.Count.ShouldBe(3);
        list.ShouldAllBe(e => e.IsActive);
    }

    [Fact]
    public async Task SpecsToPageEnumerable_ReturnsAsyncEnumerable()
    {
        // Arrange
        var spec = new ActiveEntitySpec();

        // Act
        var enumerable = _repository!.SpecsToPageEnumerable(spec);

        // Assert
        enumerable.ShouldNotBeNull();
        var list = await enumerable.ToListAsync();
        list.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SpecsToPageListAsync_ReturnsPagedList()
    {
        // Arrange
        var spec = new ActiveEntitySpec();

        // Act
        var pagedList = await _repository!.SpecsToPageListAsync(spec, 1, 2);

        // Assert
        pagedList.ShouldNotBeNull();
        pagedList.PageCount.ShouldBe(2);
        pagedList.PageSize.ShouldBe(2);
        pagedList.TotalItemCount.ShouldBe(3);
    }

    #endregion

    public class ActiveEntitySpec : Specification<TestEntity>
    {
        public ActiveEntitySpec() : base(e => e.IsActive)
        {
            AddOrderBy(e => e.Id);
        }
    }

    public class NameContainsSpec(string name) : Specification<TestEntity>(e => e.Name.Contains(name))
    {
    }

    public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        #region Properties

        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        #endregion
    }

    public class TestEntity
    {
        #region Properties

        public int Id { get; set; }

        public bool IsActive { get; set; }

        public string Name { get; set; } = string.Empty;

        #endregion
    }
}