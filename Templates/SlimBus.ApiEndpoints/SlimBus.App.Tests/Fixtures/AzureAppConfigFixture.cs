using Aspire.Hosting.Azure;

namespace SlimBus.App.Tests.Fixtures;

/// <summary>
/// Fixture for testing Azure App Configuration integration using Aspire host
/// </summary>
public sealed class AzureAppConfigFixture : IAsyncLifetime
{
    private readonly DistributedApplication _app;
    private readonly IResourceBuilder<RedisResource> _cache;
    private readonly IResourceBuilder<SqlServerDatabaseResource> _db;
    private readonly IResourceBuilder<AzureAppConfigurationResource> _appConfig;

    public string? CacheConnectionString { get; private set; }
    public string? DbConnectionString { get; private set; }
    public string? AppConfigConnectionString { get; private set; }

    public AzureAppConfigFixture()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            AssemblyName = typeof(AzureAppConfigFixture).Assembly.FullName,
            DisableDashboard = true,
            AllowUnsecuredTransport = true,
        });

        _cache = builder.AddRedis("Redis");
        var sqlServer = builder.AddSqlServer("SqlServer");
        _db = sqlServer.AddDatabase("TestDb");
        
        // Add Azure App Configuration resource for testing
        // Note: In a real scenario, this would connect to an actual Azure App Configuration instance
        // For testing purposes, we'll use a parameter resource to simulate the connection
        _appConfig = builder.AddAzureAppConfiguration("AzureAppConfig");

        _app = builder.Build();
    }

    public async Task InitializeAsync()
    {
        await _app.StartAsync();
        await _app.WaitForResourcesAsync([KnownResourceStates.Running]);

        CacheConnectionString = await _cache.Resource.GetConnectionStringAsync();
        DbConnectionString = await _db.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None) + ";Connection Timeout=60;TrustServerCertificate=true";
        
        // For testing purposes, we'll use a mock connection string since we can't easily set up real Azure App Configuration in tests
        AppConfigConnectionString = "Endpoint=https://test-appconfig.azconfig.io;Id=test-id;Secret=test-secret";
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

[CollectionDefinition(nameof(AzureAppConfigCollectionFixture))]
public sealed class AzureAppConfigCollectionFixture : ICollectionFixture<AzureAppConfigFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}