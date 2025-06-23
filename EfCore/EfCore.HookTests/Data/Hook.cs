using System.Threading;
using DKNet.EfCore.Extensions.Snapshots;

namespace EfCore.HookTests.Data;

public class Hook : /*IHook,*/ IHookAsync
{
    public bool BeforeCalled { get; private set; }
    public bool AfterCalled { get; private set; }

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
        BeforeCalled = context != null;
        return Task.CompletedTask;
    }

    public Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        AfterCalled = context != null;
        return Task.CompletedTask;
    }

    public void Reset()
    {
        BeforeCalled = false;
        AfterCalled = false;
    }
}