using Testcontainers.MsSql;

namespace EfCore.HookTests.Hooks;

public sealed class HookFixture : IDisposable
{
    private readonly MsSqlContainer _sqlContainer;

    public HookFixture()
    {
        _sqlContainer = SqlServerTestHelper.StartSqlContainerAsync().GetAwaiter().GetResult();
        
        Provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<HookContext>(o => o.UseSqlServer(_sqlContainer.GetConnectionString()).UseAutoConfigModel())
            .AddHook<HookContext, Hook>()
            .BuildServiceProvider();

        //Ensure Db Created
        var db = Provider.GetRequiredService<HookContext>();
        db.Database.EnsureCreated();
    }

    public ServiceProvider Provider { get; }

    public void Dispose()
    {
        Provider?.Dispose();
        SqlServerTestHelper.CleanupContainerAsync(_sqlContainer).GetAwaiter().GetResult();
    }
}