namespace EfCore.Relational.Helpers.Tests;

public class DbContextHelperTests
{
    [Fact]
    public async Task GetTableName()
    {
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite($"Data Source={nameof(TestDbContext)}.db;").Options);
        var (schema, tableName) = db.GetTableName<TestEntity>();
        tableName.ShouldBe(nameof(TestEntity));

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task GetTableNameNotMapped()
    {
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite($"Data Source={nameof(TestDbContext)}.db;").Options);
        var (schema, tableName) = db.GetTableName<NotMappedTestEntity>();
        tableName.ShouldBeNullOrEmpty();

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task CheckTableExistsFailed()
    {
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite($"Data Source={nameof(TestDbContext)}.db;").Options);
        //await db.Database.EnsureCreatedAsync();
        var check = await db.TableExistsAsync<TestEntity>();
        check.ShouldBeFalse();

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task CheckTableExists()
    {
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite($"Data Source={nameof(TestDbContext)}.db;").Options);
        await db.Database.EnsureCreatedAsync();
        var check = await db.TableExistsAsync<TestEntity>();
        check.ShouldBeTrue();

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task CreateTable()
    {
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite($"Data Source={nameof(TestDbContext)}.db;").Options);
        await db.CreateTableAsync<TestEntity>();

        var check = await db.TableExistsAsync<TestEntity>();
        check.ShouldBeTrue();

        await db.Database.EnsureDeletedAsync();
    }
}