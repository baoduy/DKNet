namespace EfCore.Relational.Helpers.Tests;

public class DbContextHelperTests
{
    [Fact]
    public async Task GetTableName()
    {
        using var sql = await SqlServerTestHelper.StartSqlContainerAsync();
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlServer(sql.GetConnectionString()).Options);
        var (schema, tableName) = db.GetTableName<TestEntity>();
        tableName.ShouldBe(nameof(TestEntity));

        await db.Database.EnsureDeletedAsync();
        await SqlServerTestHelper.CleanupContainerAsync(sql);
    }

    [Fact]
    public async Task GetTableNameNotMapped()
    {
        using var sql = await SqlServerTestHelper.StartSqlContainerAsync();
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlServer(sql.GetConnectionString()).Options);
        var (schema, tableName) = db.GetTableName<NotMappedTestEntity>();
        tableName.ShouldBeNullOrEmpty();

        await db.Database.EnsureDeletedAsync();
        await SqlServerTestHelper.CleanupContainerAsync(sql);
    }

    [Fact]
    public async Task CheckTableExistsFailed()
    {
        using var sql = await SqlServerTestHelper.StartSqlContainerAsync();
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlServer(sql.GetConnectionString()).Options);
        //await db.Database.EnsureCreatedAsync();
        var check = await db.TableExistsAsync<TestEntity>();
        check.ShouldBeFalse();

        await db.Database.EnsureDeletedAsync();
        await SqlServerTestHelper.CleanupContainerAsync(sql);
    }

    [Fact]
    public async Task CheckTableExists()
    {
        using var sql = await SqlServerTestHelper.StartSqlContainerAsync();
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlServer(sql.GetConnectionString()).Options);
        await db.Database.EnsureCreatedAsync();
        var check = await db.TableExistsAsync<TestEntity>();
        check.ShouldBeTrue();

        await db.Database.EnsureDeletedAsync();
        await SqlServerTestHelper.CleanupContainerAsync(sql);
    }

    [Fact]
    public async Task CreateTable()
    {
        using var sql = await SqlServerTestHelper.StartSqlContainerAsync();
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlServer(sql.GetConnectionString()).Options);
        await db.CreateTableAsync<TestEntity>();

        var check = await db.TableExistsAsync<TestEntity>();
        check.ShouldBeTrue();

        await db.Database.EnsureDeletedAsync();
        await SqlServerTestHelper.CleanupContainerAsync(sql);
    }
}