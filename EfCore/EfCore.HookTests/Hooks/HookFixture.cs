namespace EfCore.HookTests.Hooks;

public sealed class HookFixture : IDisposable
{
    public HookFixture()
    {
        Provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<HookContext>(o => o.UseSqliteMemory().UseAutoConfigModel())
            .AddHook<HookContext, Hook>()
            .BuildServiceProvider();

        //Ensure Db Created
        var db = Provider.GetRequiredService<HookContext>();
        db.Database.EnsureCreated();
    }

    public ServiceProvider Provider { get; }

    public void Dispose() => Provider?.Dispose();
}