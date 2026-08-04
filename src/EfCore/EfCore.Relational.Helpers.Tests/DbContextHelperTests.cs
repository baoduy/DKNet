using EfCore.Relational.Helpers.Tests.Fixtures;

namespace EfCore.Relational.Helpers.Tests;

public class DbContextHelperTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    #region Methods

    [Fact]
    public async Task CheckTableExistsFailed()
    {
        await fixture.EnsureReadyAsync();

        var action = async () =>
        {
            await using var db = new TestDbContext(
                new DbContextOptionsBuilder<TestDbContext>()
                    .UseNpgsql(fixture.GetConnectionString()).Options);
            await db.Database.EnsureCreatedAsync();
            await db.TableExistsAsync<NotMappedTestEntity>();
        };
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ConnectionString_ShouldUseIsolatedDatabaseName()
    {
        await fixture.EnsureReadyAsync();

        var connectionString = fixture.GetConnectionString();

        connectionString.ShouldContain("Database=TestDb_");
        connectionString.ShouldNotContain("Database=master");
    }

    [Fact]
    public async Task CreateTable()
    {
        await fixture.EnsureReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(fixture.GetConnectionString()).Options);
        await db.CreateTableAsync<TestEntity>();
        var check = await db.TableExistsAsync<TestEntity>();
        check.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTableName()
    {
        await fixture.EnsureReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(fixture.GetConnectionString()).Options);
        await db.Database.EnsureCreatedAsync();
        var (_, tableName) = db.GetTableName<TestEntity>();
        tableName.ShouldBe(nameof(TestEntity));
    }

    [Fact]
    public async Task GetTableNameNotMapped()
    {
        await fixture.EnsureReadyAsync();

        await using var db = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(fixture.GetConnectionString()).Options);
        await db.Database.EnsureCreatedAsync();
        var (_, tableName) = db.GetTableName<NotMappedTestEntity>();
        tableName.ShouldBeNullOrEmpty();
    }

    #endregion
}