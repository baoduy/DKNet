using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SlimBus.App.Tests.Extensions;
using SlimBus.Infra;
using SlimBus.Share;
using SlimBus.Share.Options;

namespace SlimBus.App.Tests.Fixtures;

public sealed class RateLimitFixture : WebApplicationFactory<Api.Program>, IAsyncLifetime
{
    private readonly DistributedApplication _app;
    private readonly IResourceBuilder<RedisResource> _cache;
    private readonly IResourceBuilder<SqlServerDatabaseResource> _db;
    //private readonly IResourceBuilder<ServiceBusResource> _bus;

    public RateLimitFixture()
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

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        Environment.SetEnvironmentVariable("RateLimit:DefaultRequestLimit", "1");
        Environment.SetEnvironmentVariable("FeatureManagement:TimeWindowInSeconds", "1");
        Environment.SetEnvironmentVariable("FeatureManagement:QueueLimit", "1");

        Environment.SetEnvironmentVariable($"FeatureManagement:{nameof(FeatureOptions.EnableRateLimit)}", "true");
        Environment.SetEnvironmentVariable($"FeatureManagement:{nameof(FeatureOptions.EnableServiceBus)}", "false");
        Environment.SetEnvironmentVariable($"FeatureManagement:{nameof(FeatureOptions.RequireAuthorization)}", "false");
        return base.CreateHost(builder);
    }

    /**
     * Disposes the resources used by the fixture asynchronously.
     * Stops the application host and disposes of it.
     */
    public new async Task DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await db.Database.EnsureDeletedAsync();

        await base.DisposeAsync();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        await _app.StartAsync();
        await _app.WaitForResourcesAsync([KnownResourceStates.Running]);

        var cacheConn = await _cache.Resource.GetConnectionStringAsync();
        var dbConn = await _db.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None)+";Connection Timeout=60;TrustServerCertificate=true";
        //var busConn = await _bus.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None);
        //busConn = busConn?.Replace("localhost", "127.0.0.1", StringComparison.CurrentCultureIgnoreCase);

        Environment.SetEnvironmentVariable($"ConnectionStrings:{SharedConsts.RedisConnectionString}", cacheConn);
        Environment.SetEnvironmentVariable($"ConnectionStrings:{SharedConsts.DbConnectionString}", dbConn);
        //Environment.SetEnvironmentVariable($"ConnectionStrings:{SharedConsts.AzureBusConnectionString}", busConn);

        await Task.Delay(TimeSpan.FromSeconds(15));

        //run Db migration here
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        db.Database.SetConnectionString(dbConn);
        await db.Database.MigrateAsync();
    }
}