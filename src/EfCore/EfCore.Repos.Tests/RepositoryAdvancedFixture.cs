using DKNet.EfCore.Repos.Abstractions;
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Repos;
using EfCore.Repos.Tests.TestEntities;
using Mapster;
using MapsterMapper;

namespace EfCore.Repos.Tests;

public class RepositoryAdvancedFixture : IAsyncLifetime
{
    public TestDbContext DbContext { get; set; } = null!;
    public IRepository<User> RepositoryWithMapper { get; set; } = null!;
    public IRepository<User> RepositoryWithoutMapper { get; set; } = null!;
    public IReadRepository<User> ReadRepositoryWithMapper { get; set; } = null!;
    public IReadRepository<User> ReadRepositoryWithoutMapper { get; set; } = null!;
    private readonly DistributedApplication _app;
    private readonly IResourceBuilder<SqlServerDatabaseResource> _db;
    private string? _dbConn;

    public RepositoryAdvancedFixture()
    {
        var options = new DistributedApplicationOptions
        {
            AssemblyName = typeof(RepositoryAdvancedFixture).Assembly.FullName,
            DisableDashboard = true,
            AllowUnsecuredTransport = true,
        };
        var builder = DistributedApplication.CreateBuilder(options);

        _db = builder.AddSqlServer("sqlServer")
            .PublishAsConnectionString()
            .AddDatabase("RepoAdvancedTestDb");

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

        // Configure Mapster
        var config = new TypeAdapterConfig();
        config.NewConfig<User, UserDto>()
            .Map(dest => dest.FirstName, src => src.FirstName)
            .Map(dest => dest.LastName, src => src.LastName);

        var mapper = new Mapper(config);
        var mappers = new[] { mapper };

        // Create repositories with and without mappers
        ReadRepositoryWithMapper = new ReadRepository<User>(DbContext, mappers);
        ReadRepositoryWithoutMapper = new ReadRepository<User>(DbContext);
        RepositoryWithMapper = new Repository<User>(DbContext, mappers);
        RepositoryWithoutMapper = new Repository<User>(DbContext);

        await Task.Delay(TimeSpan.FromSeconds(15));
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}