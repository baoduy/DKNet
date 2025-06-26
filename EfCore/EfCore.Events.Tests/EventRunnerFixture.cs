using Testcontainers.MsSql;

namespace EfCore.Events.Tests;

public sealed class EventRunnerFixture : IDisposable
{
    private readonly MsSqlContainer _sqlContainer;

    public EventRunnerFixture()
    {
        _sqlContainer = SqlServerTestHelper.StartSqlContainerAsync().GetAwaiter().GetResult();
        
        Provider = new ServiceCollection()
            .AddLogging()
            .AddCoreInfraServices<DddContext>(builder => builder.UseSqlServer(_sqlContainer.GetConnectionString()))
            .BuildServiceProvider();

        Context = Provider.GetRequiredService<DddContext>();
        Context.Database.EnsureCreated();

        //Add Root
        Context.Add(new Root("Steven"));
        Context.SaveChangesAsync().GetAwaiter().GetResult();
    }

    public ServiceProvider Provider { get; }
    public DddContext Context { get; }

    public void Dispose()
    {
        Provider?.Dispose();
        Context?.Dispose();
        SqlServerTestHelper.CleanupContainerAsync(_sqlContainer).GetAwaiter().GetResult();
    }
}