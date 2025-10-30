using System.Data;
using EfCore.Relational.Helpers.Tests.Fixtures;

namespace EfCore.Relational.Helpers.Tests;

public class DbContextHelpersAdvancedTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    #region Methods

    [Fact]
    public async Task CreateTableAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await db.CreateTableAsync<TestEntity>(cts.Token));
    }

    [Fact]
    public async Task CreateTableAsync_WithExistingTable_ShouldNotThrow()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);

        await db.Database.EnsureCreatedAsync();

        // Act & Assert - Should not throw even if table already exists
        await Should.NotThrowAsync(async () => await db.CreateTableAsync<TestEntity>());

        var exists = await db.TableExistsAsync<TestEntity>();
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateTableAsync_WithNonExistingDatabase_ShouldCreateDatabaseAndTable()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();

        // Create a unique database name to ensure it doesn't exist
        var uniqueConnectionString = fixture.GetConnectionString()
            .Replace("master", $"TestDb_{Guid.NewGuid():N}", StringComparison.OrdinalIgnoreCase);

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(uniqueConnectionString).Options);

        // Act
        await db.CreateTableAsync<TestEntity>();

        // Assert
        var exists = await db.TableExistsAsync<TestEntity>();
        exists.ShouldBeTrue();

        // Cleanup
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task GetDbConnection_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await db.GetDbConnection(cts.Token));
    }

    [Fact]
    public async Task GetDbConnection_WithClosedConnection_ShouldOpenConnection()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);

        // Ensure connection is closed first
        if (db.Database.GetDbConnection().State != ConnectionState.Closed)
        {
            await db.Database.CloseConnectionAsync();
        }

        // Act
        var connection = await db.GetDbConnection();

        // Assert
        connection.ShouldNotBeNull();
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public async Task GetDbConnection_WithOpenConnection_ShouldReturnOpenConnection()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);

        // Ensure connection is open first
        await db.Database.OpenConnectionAsync();

        // Act
        var connection = await db.GetDbConnection();

        // Assert
        connection.ShouldNotBeNull();
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public async Task GetTableName_WithEntityHavingSchema_ShouldReturnSchemaAndTableName()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);

        await db.Database.EnsureCreatedAsync();

        // Act
        var (schema, tableName) = db.GetTableName<TestEntity>();

        // Assert
        tableName.ShouldBe(nameof(TestEntity));
        schema.ShouldNotBeNullOrEmpty(); // Should have a schema (likely "dbo" for SQL Server)
    }

    [Fact]
    public async Task TableExistsAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await db.TableExistsAsync<TestEntity>(cts.Token));
    }

    [Fact]
    public async Task TableExistsAsync_WithExistingTable_ShouldReturnTrue()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);

        await db.Database.EnsureCreatedAsync();

        // Act
        var exists = await db.TableExistsAsync<TestEntity>();

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task TableExistsAsync_WithNonExistingTable_ShouldReturnTrue()
    {
        // Arrange
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);

        // Don't create the database - table won't exist

        // Act
        var exists = await db.TableExistsAsync<TestEntity>();

        // Assert
        exists.ShouldBeTrue("As Db will be auto created, the table should exist after EnsureCreatedAsync");
    }

    #endregion
}