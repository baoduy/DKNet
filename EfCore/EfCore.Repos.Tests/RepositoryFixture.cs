using DKNet.EfCore.Repos.Abstractions;
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Repos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using EfCore.TestDataLayer;
using Aspire.Hosting;
using Aspire.Hosting.SqlServer;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace EfCore.Repos.Tests;

public class RepositoryFixture : IAsyncLifetime
{
    public MyDbContext DbContext { get; set; } = null!;
    public IRepository<User> Repository { get; set; } = null!;
    public IReadRepository<User> ReadRepository { get; set; } = null!;
    private readonly DistributedApplication _app;
    private readonly IResourceBuilder<SqlServerDatabaseResource> _db;
    private string? _dbConn;
    private IHost? _host;

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
            .AddDatabase("TEMPDb");

        _app = builder.Build();
    }

    public async Task InitializeAsync()
    {
        await _app.StartAsync();
        await _app.WaitForResourcesAsync([KnownResourceStates.Running]);

        _dbConn = await _db.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None);

        var hostBuilder = new HostBuilder();
        hostBuilder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
                (StringComparer.OrdinalIgnoreCase)
                {
                    { "ConnectionStrings:TEMPDb", _dbConn },
                });
        });

        _host = hostBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<MyDbContext>()
            .UseSqlServer(_dbConn)
            .UseAutoConfigModel();

        DbContext = new MyDbContext(optionsBuilder.Options);
        ReadRepository = new ReadRepository<User>(DbContext);
        Repository = new Repository<User>(DbContext);

        await Task.Delay(TimeSpan.FromSeconds(5));

        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (DbContext is not null)
        {
            //await DbContext.Database.EnsureDeletedAsync();
            await DbContext.DisposeAsync();
        }

        _host?.Dispose();

        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}