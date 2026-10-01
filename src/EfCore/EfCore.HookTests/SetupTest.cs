using Microsoft.Data.Sqlite;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests;

public class SetupTest
{
    #region Methods

    [Fact]
    public void ServiceProviderSetupTest()
    {
        var provider = new ServiceCollection()
            .AddSingleton<HookTest>()

            //.AddSingleton<IHook>(p => p.GetService<Hook>())
            .AddSingleton<IHookAsync>(p => p.GetRequiredService<HookTest>())
            .BuildServiceProvider();

        var instance2 = provider.GetService<IHookAsync>();
        instance2.ShouldNotBeNull();
    }

    [Fact]
    public async Task AddDbContextWithHook_ProviderOverload_RunsHooksOnAsyncSaveAndRejectsSyncSave()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<HookContext>((_, o) => o.UseSqlite(connection).UseAutoConfigModel([typeof(HookContext).Assembly]))
            .AddHook<HookContext, HookTest>()
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HookContext>();
        await db.Database.EnsureCreatedAsync();
        provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName).Reset();

        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Async saved" });
        (await db.SaveChangesAsync()).ShouldBe(1);
        HookTest.BeforeCallCount.ShouldBe(1);
        HookTest.AfterCallCount.ShouldBe(1);

        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Sync rejected" });
        Should.Throw<NotSupportedException>(() => db.SaveChanges());
    }

    #endregion
}