using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using EfCore.Repos.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;

namespace EfCore.Repos.Tests;

public class RepositoryFixture : IAsyncLifetime
{
    private readonly DistributedApplication _app;
    private readonly IResourceBuilder<PostgresServerResource> _db;
    private string? _dbConn;

    public RepositoryFixture()
    {
        var options = new DistributedApplicationOptions
        {
            AssemblyName = typeof(RepositoryFixture).Assembly.FullName,
            DisableDashboard = true,
            AllowUnsecuredTransport = true
        };
        var builder = DistributedApplication.CreateBuilder(options);

        _db = builder.AddPostgres("sqlServer");

        _app = builder.Build();
    }

    public DbContext DbContext { get; set; } = null!;
    public IRepository<User> Repository { get; set; } = null!;
    public IReadRepository<User> ReadRepository { get; set; } = null!;

    public async Task InitializeAsync()
    {
        await _app.StartAsync();
        await _app.WaitForResourcesAsync([KnownResourceStates.Running]);

        DbContext = await CreateNewDbContext();
        ReadRepository = new ReadRepository<User>(DbContext);
        Repository = new Repository<User>(DbContext);

        await Task.Delay(TimeSpan.FromSeconds(15));
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();

        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    public async Task<DbContext> CreateNewDbContext()
    {
        _dbConn = await _db.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None);

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_dbConn)
            .UseAutoConfigModel(c => c.ScanFrom(typeof(RepositoryFixture).Assembly));

        return new TestDbContext(optionsBuilder.Options);
    }
}