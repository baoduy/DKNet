namespace SlimBus.App.Tests.Fixtures;

public sealed class ShareInfraFixture : IAsyncLifetime
{
    private readonly DistributedApplication _app;
    private readonly IResourceBuilder<RedisResource> _cache;
    private readonly IResourceBuilder<SqlServerDatabaseResource> _db;

    public string? CacheConn { get; private set; }
    public string? DbConn { get; private set; }

    public ShareInfraFixture()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            AssemblyName = typeof(ApiFixture).Assembly.FullName,
            DisableDashboard = true,
            AllowUnsecuredTransport = true,
        });

        _cache = builder.AddRedis("Redis")
            .WithLifetime(ContainerLifetime.Persistent);
        var sqlServer = builder.AddSqlServer("sqlServer")
            .WithLifetime(ContainerLifetime.Persistent);
        _db = sqlServer.AddDatabase("TestDb");

        // _bus = builder.AddServiceBus(sqlServer, "Data/busConfig.json")
        //     .WithLifetime(ContainerLifetime.Persistent);
        _app = builder.Build();
    }

    public async Task InitializeAsync()
    {
        await _app.StartAsync();
        await _app.WaitForResourcesAsync([KnownResourceStates.Running]);

        CacheConn = await _cache.Resource.GetConnectionStringAsync();
        DbConn = await _db.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None)+";Connection Timeout=60;TrustServerCertificate=true";
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

[CollectionDefinition(nameof(ShareInfraCollection))]
public sealed class ShareInfraCollection : ICollectionFixture<ShareInfraFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}