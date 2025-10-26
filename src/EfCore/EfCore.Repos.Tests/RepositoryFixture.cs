using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using Testcontainers.PostgreSql;

namespace EfCore.Repos.Tests;

public class RepositoryFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _sqlContainer = new PostgreSqlBuilder()
        .Build();

    public DbContext? DbContext { get; set; }
    public IRepository<User> Repository { get; set; } = null!;
    public IReadRepository<User> ReadRepository { get; set; } = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        DbContext = CreateNewDbContext();
        ReadRepository = new ReadRepository<User>(DbContext);
        Repository = new Repository<User>(DbContext);

        await Task.Delay(TimeSpan.FromSeconds(15));
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (DbContext != null)
            await DbContext.DisposeAsync();

        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }

    public DbContext CreateNewDbContext()
    {
        var dbConn = _sqlContainer.GetConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(dbConn)
            .UseAutoConfigModel();

        return new TestDbContext(optionsBuilder.Options);
    }
}