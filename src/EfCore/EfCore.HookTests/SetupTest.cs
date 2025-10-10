namespace EfCore.HookTests;

public class SetupTest
{
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
}