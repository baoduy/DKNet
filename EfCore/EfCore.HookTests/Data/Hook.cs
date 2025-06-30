using System.Diagnostics;
using DKNet.EfCore.Extensions.Snapshots;

namespace EfCore.HookTests.Data;

public class Hook : /*IHook,*/ IHookAsync
{
    public bool BeforeCalled { get; private set; }
    public bool AfterCalled { get; private set; }
    public int BeforeCallCount { get; private set; }
    public int AfterCallCount { get; private set; }
    public SnapshotContext? LastBeforeContext { get; private set; }
    public SnapshotContext? LastAfterContext { get; private set; }
    public DateTime BeforeCallTime { get; private set; }
    public DateTime AfterCallTime { get; private set; }
    public bool ShouldThrowException { get; set; }

    // public void RunBeforeSave(SnapshotContext context)
    // {
    //     BeforeCalled = context != null;
    // }
    //
    // public void RunAfterSave(SnapshotContext context)
    // {
    //     AfterCalled = context != null;
    // }

    public Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        Trace.WriteLine($"Running BeforeSaveAsync at {DateTime.Now}");
        BeforeCalled = context.SnapshotEntities.Count > 0;
        BeforeCallCount++;
        LastBeforeContext = context;
        BeforeCallTime = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        Trace.WriteLine($"Running RunAfterSaveAsync at {DateTime.Now}");
        AfterCalled = context.SnapshotEntities.Count > 0;
        AfterCallCount++;
        LastAfterContext = context;
        AfterCallTime = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public void Reset()
    {
        BeforeCalled = false;
        AfterCalled = false;
        BeforeCallCount = 0;
        AfterCallCount = 0;
        LastBeforeContext = null;
        LastAfterContext = null;
        BeforeCallTime = default;
        AfterCallTime = default;
        ShouldThrowException = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (LastAfterContext is not null)
            await LastAfterContext.DisposeAsync();
        if (LastBeforeContext is not null)
            await LastBeforeContext.DisposeAsync();
    }

    public void Dispose()
    {
        if (LastAfterContext is not null)
             LastAfterContext.Dispose();
        if (LastBeforeContext is not null)
             LastBeforeContext.Dispose();
    }
}