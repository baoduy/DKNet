using EfCore.Relational.Helpers.Tests.Fixtures;
using System.Data;

namespace EfCore.Relational.Helpers.Tests;

public class DbContextHelpersEdgeCaseTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task GetDbConnection_MultipleCallsConcurrently_ShouldHandleCorrectly()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();
        
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(fixture.GetConnectionString()).Options);

        // Act - Make multiple concurrent calls
        var tasks = Enumerable.Range(0, 10).Select(async _ => await db.GetDbConnection()).ToArray();
        var connections = await Task.WhenAll(tasks);

        // Assert
        connections.ShouldAllBe(conn => conn != null);
        connections.ShouldAllBe(conn => conn.State == ConnectionState.Open);
        connections.Select(c => c.ConnectionString).Distinct().ShouldHaveSingleItem(); // Same connection string
    }

    [Fact]
    public async Task TableExistsAsync_WithInvalidDbContext_ShouldHandleGracefully()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();
        
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(fixture.GetConnectionString()).Options);

        // Dispose the context to make it invalid
        await db.DisposeAsync();

        // Act & Assert
        await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await db.TableExistsAsync<TestEntity>());
    }

    [Fact]
    public async Task CreateTableAsync_WithInvalidConnectionString_ShouldThrow()
    {
        // Arrange
        const string invalidConnectionString = "Server=invalid;Database=invalid;Integrated Security=true;";
        
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(invalidConnectionString).Options);

        // Act & Assert
        await Should.ThrowAsync<Exception>(async () =>
            await db.CreateTableAsync<TestEntity>());
    }

    [Fact]
    public async Task GetTableName_WithComplexEntity_ShouldReturnCorrectNames()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();
        
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(fixture.GetConnectionString()).Options);

        // Act - Test with different entity types
        var (schema1, tableName1) = db.GetTableName<TestEntity>();
        var (schema2, tableName2) = db.GetTableName<NotMappedTestEntity>();

        // Assert
        tableName1.ShouldBe(nameof(TestEntity));
        schema1.ShouldNotBeNullOrEmpty();
        
        tableName2.ShouldBeNullOrEmpty(); // Not mapped entity
        schema2.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task TableExistsAsync_WithLongRunningOperation_ShouldComplete()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();
        
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(fixture.GetConnectionString()).Options);
        
        await db.Database.EnsureCreatedAsync();

        // Act - Use a longer timeout to simulate longer operations
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var exists = await db.TableExistsAsync<TestEntity>(cts.Token);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateTableAsync_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();
        
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(fixture.GetConnectionString()).Options);

        // Act - Call CreateTableAsync multiple times
        await db.CreateTableAsync<TestEntity>();
        await db.CreateTableAsync<TestEntity>();
        await db.CreateTableAsync<TestEntity>();

        // Assert - Should not throw and table should exist
        var exists = await db.TableExistsAsync<TestEntity>();
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task GetDbConnection_AfterDatabaseCreation_ShouldWork()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();
        
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(fixture.GetConnectionString()).Options);

        // Act
        await db.Database.EnsureCreatedAsync();
        var connection = await db.GetDbConnection();

        // Assert
        connection.ShouldNotBeNull();
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public async Task All_Methods_WithShortTimeout_ShouldComplete()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();
        
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(fixture.GetConnectionString()).Options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act & Assert - All operations should complete within reasonable time
        await Should.NotThrowAsync(async () =>
        {
            var connection = await db.GetDbConnection(cts.Token);
            await db.CreateTableAsync<TestEntity>(cts.Token);
            var exists = await db.TableExistsAsync<TestEntity>(cts.Token);
            var (schema, tableName) = db.GetTableName<TestEntity>();
            
            connection.ShouldNotBeNull();
            exists.ShouldBeTrue();
            tableName.ShouldBe(nameof(TestEntity));
        });
    }
}