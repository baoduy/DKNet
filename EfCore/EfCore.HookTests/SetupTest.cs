

namespace EfCore.HookTests;

public class SetupTest
{
    [Fact]
    public void ServiceProviderSetupTest()
    {
        var provider = new ServiceCollection()
            .AddSingleton<Hook>()
            //.AddSingleton<IHook>(p => p.GetService<Hook>())
            .AddSingleton<IHookAsync>(p => p.GetService<Hook>())
            .BuildServiceProvider();

        var instance2 = provider.GetService<IHookAsync>();
        instance2.ShouldNotBeNull();
    }
}