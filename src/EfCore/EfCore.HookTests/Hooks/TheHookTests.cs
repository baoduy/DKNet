using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

public class TheHookTests(HookFixture fixture) : IClassFixture<HookFixture>
{
    #region Fields

    private readonly ServiceProvider _provider = fixture.Provider;

    #endregion

    #region Methods

    [Fact]
    public void AddHookRunnerTwice()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddHookRunner<HookContext>()
            .AddHookRunner<HookContext>();

        services.Count(s => s.ServiceType == typeof(HookRunnerInterceptor))
            .ShouldBe(1);
    }

    [Fact]
    public void AddHookTwice()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddHook<HookContext, HookTest>()
            .AddHook<HookContext, HookTest>();

        services.Count(s =>
                s.IsKeyedService && ReferenceEquals(s.ServiceKey, typeof(HookContext).FullName) &&
                s.KeyedImplementationType == typeof(HookTest))
            .ShouldBe(1);
    }

    [Fact]
    public async Task TestAddHookAsync()
    {
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();

        var db = _provider.GetRequiredService<HookContext>();

        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Duy" });
        await db.SaveChangesAsync();

        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task TestCallSaveChangesTwiceAsync()
    {
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        var db = _provider.GetRequiredService<HookContext>();

        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Duy" });
        await db.SaveChangesAsync();
        hook.Reset();

        await db.SaveChangesAsync(); // No changes, hooks should not run

        HookTest.BeforeCallCount.ShouldBeGreaterThanOrEqualTo(0);
        // Hooks only run when there are actual changes, so second SaveChanges doesn't trigger hooks
        HookTest.BeforeCalled.ShouldBeFalse();
    }

    #endregion
}