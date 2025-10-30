using DKNet.EfCore.Extensions.Extensions;

namespace EfCore.Extensions.Tests;

public class PageAsyncEnumeratorTests
{
    #region Fields

    private readonly DbContextOptions<TestDbContext> _options = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    #endregion

    #region Methods

    private static async Task SeedDataAsync(TestDbContext context, int count)
    {
        var entities = Enumerable.Range(1, count)
            .Select(i => new TestEntity { Id = i, Name = $"Entity {i}" })
            .ToList();

        context.TestEntities.AddRange(entities);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ToPageEnumerable_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        await using var context = new TestDbContext(this._options);
        await SeedDataAsync(context, 100);
        using var cts = new CancellationTokenSource();

        // Act & Assert
        var result = new List<TestEntity>();
        var enumerator = context.TestEntities.ToPageEnumerable(10).GetAsyncEnumerator(cts.Token);

        try
        {
            // Get the first few items
            for (var i = 0; i < 5; i++)
            {
                (await enumerator.MoveNextAsync()).ShouldBeTrue();
                result.Add(enumerator.Current);
            }

            // Cancel and try to get more
            await cts.CancelAsync();

            // The next MoveNextAsync should respect cancellation
            await Should.ThrowAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    [Fact]
    public async Task ToPageEnumerable_WithCustomPageSize_ShouldReturnAllItems()
    {
        // Arrange
        await using var context = new TestDbContext(this._options);
        await SeedDataAsync(context, 25);
        const int pageSize = 10;

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.ToPageEnumerable(pageSize))
        {
            result.Add(item);
        }

        // Assert
        result.Count.ShouldBe(25);
    }

    [Fact]
    public async Task ToPageEnumerable_WithDefaultPageSize_ShouldReturnAllItems()
    {
        // Arrange
        await using var context = new TestDbContext(this._options);
        await SeedDataAsync(context, 150); // More than default page size (100)

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.ToPageEnumerable())
        {
            result.Add(item);
        }

        // Assert
        result.Count.ShouldBe(150);
        result.Select(x => x.Id).Distinct().Count().ShouldBe(150); // All unique
    }

    [Fact]
    public async Task ToPageEnumerable_WithEmptyQuery_ShouldReturnNoItems()
    {
        // Arrange
        await using var context = new TestDbContext(this._options);

        // No data seeded

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.ToPageEnumerable())
        {
            result.Add(item);
        }

        // Assert
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ToPageEnumerable_WithExactPageSize_ShouldReturnAllItems()
    {
        // Arrange
        await using var context = new TestDbContext(this._options);
        await SeedDataAsync(context, 20);
        const int pageSize = 10; // Exactly 2 pages

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.ToPageEnumerable(pageSize))
        {
            result.Add(item);
        }

        // Assert
        result.Count.ShouldBe(20);
    }

    [Fact]
    public async Task ToPageEnumerable_WithFilteredQuery_ShouldReturnFilteredItems()
    {
        // Arrange
        await using var context = new TestDbContext(this._options);
        await SeedDataAsync(context, 50);
        const int pageSize = 10;

        // Act - Only get even IDs
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.Where(x => x.Id % 2 == 0).ToPageEnumerable(pageSize))
        {
            result.Add(item);
        }

        // Assert
        result.Count.ShouldBe(25); // Half of 50
        result.TrueForAll(x => x.Id % 2 == 0).ShouldBeTrue();
    }

    [Fact]
    public async Task ToPageEnumerable_WithOrderedQuery_ShouldMaintainOrder()
    {
        // Arrange
        await using var context = new TestDbContext(this._options);
        await SeedDataAsync(context, 25);
        const int pageSize = 10;

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.OrderBy(x => x.Id).ToPageEnumerable(pageSize))
        {
            result.Add(item);
        }

        // Assert
        result.Count.ShouldBe(25);
        var sortedResult = result.OrderBy(x => x.Id).ToList();
        result.ShouldBe(sortedResult);
    }

    [Fact]
    public async Task ToPageEnumerable_WithSinglePage_ShouldReturnAllItems()
    {
        // Arrange
        await using var context = new TestDbContext(this._options);
        await SeedDataAsync(context, 5);
        const int pageSize = 10; // Larger than data set

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.ToPageEnumerable(pageSize))
        {
            result.Add(item);
        }

        // Assert
        result.Count.ShouldBe(5);
    }

    #endregion
}

public class TestEntity
{
    #region Properties

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    #endregion
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<TestEntity> TestEntities { get; set; }

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>().HasKey(e => e.Id);
    }

    #endregion
}