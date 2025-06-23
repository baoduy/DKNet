using DKNet.EfCore.Extensions.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace EfCore.Extensions.Tests;

[TestClass]
public class PageAsyncEnumeratorTests
{
    private DbContextOptions<TestDbContext> _options;

    [TestInitialize]
    public void Setup()
    {
        _options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [TestMethod]
    public async Task ToPageEnumerable_WithDefaultPageSize_ShouldReturnAllItems()
    {
        // Arrange
        using var context = new TestDbContext(_options);
        await SeedDataAsync(context, 150); // More than default page size (100)

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.ToPageEnumerable())
        {
            result.Add(item);
        }

        // Assert
        Assert.AreEqual(150, result.Count);
        Assert.AreEqual(150, result.Select(x => x.Id).Distinct().Count()); // All unique
    }

    [TestMethod]
    public async Task ToPageEnumerable_WithCustomPageSize_ShouldReturnAllItems()
    {
        // Arrange
        using var context = new TestDbContext(_options);
        await SeedDataAsync(context, 25);
        const int pageSize = 10;

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.ToPageEnumerable(pageSize))
        {
            result.Add(item);
        }

        // Assert
        Assert.AreEqual(25, result.Count);
    }

    [TestMethod]
    public async Task ToPageEnumerable_WithEmptyQuery_ShouldReturnNoItems()
    {
        // Arrange
        using var context = new TestDbContext(_options);
        // No data seeded

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.ToPageEnumerable())
        {
            result.Add(item);
        }

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task ToPageEnumerable_WithSinglePage_ShouldReturnAllItems()
    {
        // Arrange
        using var context = new TestDbContext(_options);
        await SeedDataAsync(context, 5);
        const int pageSize = 10; // Larger than data set

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.ToPageEnumerable(pageSize))
        {
            result.Add(item);
        }

        // Assert
        Assert.AreEqual(5, result.Count);
    }

    [TestMethod]
    public async Task ToPageEnumerable_WithExactPageSize_ShouldReturnAllItems()
    {
        // Arrange
        using var context = new TestDbContext(_options);
        await SeedDataAsync(context, 20);
        const int pageSize = 10; // Exactly 2 pages

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.ToPageEnumerable(pageSize))
        {
            result.Add(item);
        }

        // Assert
        Assert.AreEqual(20, result.Count);
    }

    [TestMethod]
    public async Task ToPageEnumerable_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        using var context = new TestDbContext(_options);
        await SeedDataAsync(context, 100);
        using var cts = new CancellationTokenSource();

        // Act & Assert
        var result = new List<TestEntity>();
        var enumerator = context.TestEntities.ToPageEnumerable(10).GetAsyncEnumerator(cts.Token);
        
        try
        {
            // Get first few items
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(await enumerator.MoveNextAsync());
                result.Add(enumerator.Current);
            }

            // Cancel and try to get more
            cts.Cancel();
            
            // The next MoveNextAsync should respect cancellation
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                async () => await enumerator.MoveNextAsync());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task ToPageEnumerable_WithOrderedQuery_ShouldMaintainOrder()
    {
        // Arrange
        using var context = new TestDbContext(_options);
        await SeedDataAsync(context, 25);
        const int pageSize = 10;

        // Act
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.OrderBy(x => x.Id).ToPageEnumerable(pageSize))
        {
            result.Add(item);
        }

        // Assert
        Assert.AreEqual(25, result.Count);
        var sortedResult = result.OrderBy(x => x.Id).ToList();
        CollectionAssert.AreEqual(sortedResult, result);
    }

    [TestMethod]
    public async Task ToPageEnumerable_WithFilteredQuery_ShouldReturnFilteredItems()
    {
        // Arrange
        using var context = new TestDbContext(_options);
        await SeedDataAsync(context, 50);
        const int pageSize = 10;

        // Act - Only get even IDs
        var result = new List<TestEntity>();
        await foreach (var item in context.TestEntities.Where(x => x.Id % 2 == 0).ToPageEnumerable(pageSize))
        {
            result.Add(item);
        }

        // Assert
        Assert.AreEqual(25, result.Count); // Half of 50
        Assert.IsTrue(result.All(x => x.Id % 2 == 0));
    }

    private static async Task SeedDataAsync(TestDbContext context, int count)
    {
        var entities = Enumerable.Range(1, count)
            .Select(i => new TestEntity { Id = i, Name = $"Entity {i}" })
            .ToList();

        context.TestEntities.AddRange(entities);
        await context.SaveChangesAsync();
    }
}

public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<TestEntity> TestEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>().HasKey(e => e.Id);
    }
}