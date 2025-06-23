

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

        //var instance1 = provider.GetService<IHook>();
        var instance2 = provider.GetService<IHookAsync>();

        //instance1.Should().NotBeNull();
        instance2.ShouldNotBeNull();

        //instance1.Should().Be(instance2, "2 instances should be the same");
    }
}