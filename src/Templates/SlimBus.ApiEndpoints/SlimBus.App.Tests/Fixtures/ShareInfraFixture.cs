namespace SlimBus.App.Tests.Fixtures;

public sealed class ShareInfraFixture : IAsyncLifetime
{
    private readonly DistributedApplication _app;
    private readonly IResourceBuilder<RedisResource> _cache;
    private readonly IResourceBuilder<SqlServerDatabaseResource> _db;

    public ShareInfraFixture()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            AssemblyName = typeof(ApiFixture).Assembly.FullName,
            DisableDashboard = true,
            AllowUnsecuredTransport = true
        });

        _cache = builder.AddRedis("Redis");
        var sqlServer = builder.AddSqlServer("sqlServer");
        _db = sqlServer.AddDatabase("TestDb");
        _app = builder.Build();
    }

    public string? CacheConn { get; private set; }
    public string? DbConn { get; private set; }

    public async Task InitializeAsync()
    {
        await _app.StartAsync();
        await _app.WaitForResourcesAsync([KnownResourceStates.Running]);

        CacheConn = await _cache.Resource.GetConnectionStringAsync();
        DbConn = await _db.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None) +
                 ";Connection Timeout=60;TrustServerCertificate=true";
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

[CollectionDefinition(nameof(ShareInfraCollectionFixture))]
public sealed class ShareInfraCollectionFixture : ICollectionFixture<ShareInfraFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}