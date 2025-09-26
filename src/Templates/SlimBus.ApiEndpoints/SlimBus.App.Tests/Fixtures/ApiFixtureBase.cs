using SlimBus.Infra.Contexts;

namespace SlimBus.App.Tests.Fixtures;

public abstract class ApiFixtureBase(ShareInfraFixture infra) : WebApplicationFactory<SlimBus.Api.Program>, IAsyncLifetime
{
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
    }

    public async Task InitializeAsync()
    {
        SetEnvironmentVariables();

        await Task.Delay(TimeSpan.FromSeconds(15));

        //run Db migration here
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        db.Database.SetConnectionString(infra.DbConn);
        await db.Database.MigrateAsync();
    }

    protected abstract void SetEnvironmentVariables();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        return base.CreateHost(builder);
    }
}