using EfCore.HookTests;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

public class HookDisablingTests(HookFixture fixture) : IClassFixture<HookFixture>
{
    #region Fields

    private readonly ServiceProvider _provider = fixture.Provider;

    #endregion

    #region Methods

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

        HookTest.BeforeCalled.ShouldBeFalse();
        HookTest.AfterCalled.ShouldBeFalse();

        db.ChangeTracker.Clear();
        hook.Reset();

        // After disposal hooks should run again
        db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "ActiveAgain" });
        await db.SaveChangesAsync();

        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
        HookTest.BeforeCallCount.ShouldBeGreaterThan(0);
        HookTest.AfterCallCount.ShouldBeGreaterThan(0);
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
        HookTest.BeforeCallCount.ShouldBeGreaterThan(0);
        HookTest.AfterCallCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void DisableHooks_WhenNested_RequiresMatchingDisposeCount()
    {
        using var db = new HookContext(
            new DbContextOptionsBuilder<HookContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        HookDisablingContext.IsHookDisabled(db).ShouldBeFalse();

        var outer = db.DisableHooks();
        HookDisablingContext.IsHookDisabled(db).ShouldBeTrue();

        var inner = db.DisableHooks();
        HookDisablingContext.IsHookDisabled(db).ShouldBeTrue();

        inner.Dispose();
        HookDisablingContext.IsHookDisabled(db).ShouldBeTrue(); // outer scope still active

        outer.Dispose();
        HookDisablingContext.IsHookDisabled(db).ShouldBeFalse();
    }

    [Fact]
    public void DisableHooks_WhenDisposedTwice_DoesNotUnderflow()
    {
        using var db = new HookContext(
            new DbContextOptionsBuilder<HookContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var outer = db.DisableHooks();
        var inner = db.DisableHooks();

        outer.Dispose();
        outer.Dispose(); // must be a no-op, not a second decrement

        // If the double dispose above had under-counted, this would already be false.
        HookDisablingContext.IsHookDisabled(db).ShouldBeTrue();

        inner.Dispose();
        HookDisablingContext.IsHookDisabled(db).ShouldBeFalse();
    }

    [Fact]
    public void DisableHooks_OnDifferentDbContextType_DoesNotAffectOther()
    {
        using var db1 = new HookContext(
            new DbContextOptionsBuilder<HookContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        using var db2 = new PlainHookContext(
            new DbContextOptionsBuilder<PlainHookContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        using (db1.DisableHooks())
        {
            HookDisablingContext.IsHookDisabled(db1).ShouldBeTrue();
            HookDisablingContext.IsHookDisabled(db2).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task DisableHooks_ConcurrentFlow_DoesNotAffectUnrelatedFlow()
    {
        // Same DbContext type on both sides on purpose: the old implementation keyed suppression
        // by type name in a process-wide static dictionary, so a second, unrelated flow using the
        // same DbContext type would incorrectly observe hooks as disabled.
        using var db1 = new HookContext(
            new DbContextOptionsBuilder<HookContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        using var db2 = new HookContext(
            new DbContextOptionsBuilder<HookContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var scopeActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseScope = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Task A: disables hooks for db1's type and holds the scope open until told to release.
        var disablingFlow = Task.Run(async () =>
        {
            using (db1.DisableHooks())
            {
                scopeActive.SetResult();
                await releaseScope.Task;
            }
        });

        await scopeActive.Task;

        // Task B: started from THIS method's flow - a sibling of the disabling flow above, not a
        // child of it - so it must not inherit the suppression even though db2 is the same type as db1.
        var observedDisabledOnUnrelatedFlow = await Task.Run(() => HookDisablingContext.IsHookDisabled(db2));

        releaseScope.SetResult();
        await disablingFlow;

        observedDisabledOnUnrelatedFlow.ShouldBeFalse();
    }

    #endregion
}