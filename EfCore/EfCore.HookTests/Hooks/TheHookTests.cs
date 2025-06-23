namespace EfCore.HookTests.Hooks;

public class TheHookTests(HookFixture fixture) : IClassFixture<HookFixture>
{
    private readonly ServiceProvider _provider = fixture.Provider;

    // [Fact]
    // public void TestAddHook()
    // {
    //     var hook = _provider.GetRequiredService<Hook>();
    //     var db = _provider.GetRequiredService<HookContext>();
    //
    //     db.Set<Profile>().Add(new Profile { Name = "Duy" });
    //     db.SaveChanges();
    //
    //     hook.BeforeCalled.ShouldBeTrue();
    //     hook.AfterCalled.ShouldBeTrue();
    // }

    [Fact]
    public async Task TestAddHookAsync()
    {
        var hook = _provider.GetRequiredKeyedService<Hook>(typeof(HookContext).FullName);
        hook.Reset();

        var db = _provider.GetRequiredService<HookContext>();

        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Duy" });
        await db.SaveChangesAsync();

        hook.BeforeCalled.ShouldBeTrue();
        hook.AfterCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task TestCallSaveChangesTwiceAsync()
    {
        var hook = _provider.GetRequiredKeyedService<Hook>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Duy" });
        await db.SaveChangesAsync();
        await db.SaveChangesAsync();

        hook.BeforeCalled.ShouldBeTrue();
        hook.AfterCalled.ShouldBeTrue();
    }

    // [Fact]
    // public async Task HookShouldNotRaise()
    // {
    //     var hook = _provider.GetRequiredKeyedService<Hook>(typeof(HookContext).FullName);
    //     hook.Reset();
    //     var db = _provider.GetRequiredService<HookContext>();
    //     var disabled = db.DisposableHooks(hook);
    //
    //     //Check global flag
    //     HookExtensions.DisabledHooks.Count.ShouldBe(1);
    //
    //     db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Duy" });
    //     await db.SaveChangesAsync();
    //
    //     hook.BeforeCalled.ShouldBeFalse();
    //     hook.AfterCalled.ShouldBeFalse();
    //
    //     //Check global flag after disposed.
    //     disabled.Dispose();
    //     HookExtensions.DisabledHooks.Count.ShouldBe(0);
    // }

    [Fact]
    public void AddHookRunnerTwice()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddHookRunner<HookContext>()
            .AddHookRunner<HookContext>();

        services.Count(s => s.ServiceType == typeof(HookRunner))
            .ShouldBe(1);
    }

    [Fact]
    public void AddHookTwice()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddHook<HookContext, Hook>()
            .AddHook<HookContext, Hook>();

        services.Count(s =>
                s.IsKeyedService && ReferenceEquals(s.ServiceKey, typeof(HookContext).FullName) &&
                s.KeyedImplementationType == typeof(Hook))
            .ShouldBe(1);
    }
}