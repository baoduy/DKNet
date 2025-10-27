using System.Diagnostics;
using DKNet.EfCore.Extensions.Snapshots;

namespace EfCore.HookTests.Data;

public class HookTest : IHookAsync
{
    #region Properties

    public static int AfterCallCount { get; private set; }
    public static bool AfterCalled { get; private set; }
    public static int BeforeCallCount { get; private set; }
    public static bool BeforeCalled { get; private set; }
    public bool ShouldThrowException { get; set; }

    #endregion

    #region Methods

    public Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        Trace.WriteLine($"Running RunAfterSaveAsync at {DateTime.Now}");
        AfterCalled = context.Entities.Count > 0;
        AfterCallCount++;
        return Task.CompletedTask;
    }

    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        Trace.WriteLine($"Running BeforeSaveAsync at {DateTime.Now}");
        BeforeCalled = context.Entities.Count > 0;
        BeforeCallCount++;
        return Task.CompletedTask;
    }

    public void Reset()
    {
        BeforeCalled = false;
        AfterCalled = false;
        BeforeCallCount = 0;
        AfterCallCount = 0;
        ShouldThrowException = false;
    }

    #endregion
}