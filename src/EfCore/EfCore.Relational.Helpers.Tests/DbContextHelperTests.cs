using EfCore.Relational.Helpers.Tests.Fixtures;

namespace EfCore.Relational.Helpers.Tests;

public class DbContextHelperTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    #region Methods

    [Fact]
    public async Task CheckTableExistsFailed()
    {
        await fixture.EnsureSqlReadyAsync();

        var action = async () =>
        {
            await using var db = new TestDbContext(
                new DbContextOptionsBuilder<TestDbContext>()
                    .UseSqlServer(fixture.GetConnectionString()).Options);
            await db.Database.EnsureCreatedAsync();
            await db.TableExistsAsync<NotMappedTestEntity>();
        };
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateTable()
    {
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);
        await db.CreateTableAsync<TestEntity>();
        var check = await db.TableExistsAsync<TestEntity>();
        check.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTableName()
    {
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);
        await db.Database.EnsureCreatedAsync();
        var (_, tableName) = db.GetTableName<TestEntity>();
        tableName.ShouldBe(nameof(TestEntity));
    }

    [Fact]
    public async Task GetTableNameNotMapped()
    {
        await fixture.EnsureSqlReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(fixture.GetConnectionString()).Options);
        await db.Database.EnsureCreatedAsync();
        var (_, tableName) = db.GetTableName<NotMappedTestEntity>();
        tableName.ShouldBeNullOrEmpty();
    }

    #endregion
}