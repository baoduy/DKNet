using DKNet.EfCore.Repos.Abstractions;
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Repos;
using EfCore.Repos.Tests.TestEntities;

namespace EfCore.Repos.Tests;

public class RepositoryFixture : IAsyncLifetime
{
    public TestDbContext DbContext { get; set; } = null!;
    public IRepository<User> Repository { get; set; } = null!;
    public IReadRepository<User> ReadRepository { get; set; } = null!;
    private readonly DistributedApplication _app;
    private readonly IResourceBuilder<SqlServerDatabaseResource> _db;
    private string? _dbConn;

    public RepositoryFixture()
    {
        var options = new DistributedApplicationOptions
        {
            AssemblyName = typeof(RepositoryFixture).Assembly.FullName,
            DisableDashboard = true,
            AllowUnsecuredTransport = true,
        };
        var builder = DistributedApplication.CreateBuilder(options);

        _db = builder.AddSqlServer("sqlServer")
            .PublishAsConnectionString()
            .AddDatabase("RepoTestDb");

        _app = builder.Build();
    }

    public async Task InitializeAsync()
    {
        await _app.StartAsync();
        await _app.WaitForResourcesAsync([KnownResourceStates.Running]);

        _dbConn = await _db.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None)+";TrustServerCertificate=true";

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(_dbConn);

        DbContext = new TestDbContext(optionsBuilder.Options);
        DbContext.Database.SetConnectionString(_dbConn);

        ReadRepository = new ReadRepository<User>(DbContext);
        Repository = new Repository<User>(DbContext);

        await Task.Delay(TimeSpan.FromSeconds(15));
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        //await DbContext.Database.EnsureDeletedAsync();
        await DbContext.DisposeAsync();

        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}