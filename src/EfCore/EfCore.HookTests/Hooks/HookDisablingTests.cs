using EfCore.HookTests.Hooks;

namespace EfCore.HookTests.Hooks;

public class HookDisablingTests(HookFixture fixture) : IClassFixture<HookFixture>
{
    private readonly ServiceProvider _provider = fixture.Provider;

    [Fact]
    public async Task DisableHooks_Should_Suppress_Hook_Calls()
    {
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        await using (db.DisableHooks())
        {
            db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "NoHooks" });
            await db.SaveChangesAsync();
        }

        HookTest.BeforeCalled.ShouldBeFalse();
        HookTest.AfterCalled.ShouldBeFalse();
        HookTest.BeforeCallCount.ShouldBe(0);
        HookTest.AfterCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Hooks_Should_Resume_After_DisableHooks_Disposed()
    {
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        await using (db.DisableHooks())
        {
            db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Suppressed" });
            await db.SaveChangesAsync();
        }

        // After disposal hooks should run again
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "ActiveAgain" });
        await db.SaveChangesAsync();

        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
        HookTest.BeforeCallCount.ShouldBe(1);
        HookTest.AfterCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Nested_DisableHooks_Should_Keep_Disabled_Until_Last_Disposed()
    {
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        await using (db.DisableHooks())
        {
            await using (db.DisableHooks())
            {
                db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Nested1" });
                await db.SaveChangesAsync();
            }
            // Still inside outer disabling scope
            db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Nested2" });
            await db.SaveChangesAsync();
        }

        // All still suppressed
        HookTest.BeforeCalled.ShouldBeFalse();
        HookTest.AfterCalled.ShouldBeFalse();
        HookTest.BeforeCallCount.ShouldBe(0);
        HookTest.AfterCallCount.ShouldBe(0);

        // After exiting both scopes hooks should work again
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "AfterNested" });
        await db.SaveChangesAsync();

        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
        HookTest.BeforeCallCount.ShouldBe(1);
        HookTest.AfterCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task DisableHooks_Async_Disposal_Should_Work()
    {
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        await using (db.DisableHooks())
        {
            db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "AsyncSuppressed" });
            await db.SaveChangesAsync();
        }

        HookTest.BeforeCalled.ShouldBeFalse();
        HookTest.AfterCalled.ShouldBeFalse();
        HookTest.BeforeCallCount.ShouldBe(0);
        HookTest.AfterCallCount.ShouldBe(0);
    }
}

