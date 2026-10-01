using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

internal sealed class HookContext : IDisposable, IAsyncDisposable
{
    #region Constructors

    public HookContext(IServiceProvider provider, DbContext db, HookContext? outer = null)
    {
        Outer = outer;
        var factory = provider.GetRequiredService<HookFactory>();
        var (before, afters) = factory.LoadHooks(db);
        BeforeSaveHooks = before;
        AfterSaveHooks = afters;
        Snapshot = new SnapshotContext(db);
    }

    #endregion

    #region Properties

    public IReadOnlyCollection<IAfterSaveHookAsync> AfterSaveHooks { get; }

    public IReadOnlyCollection<IBeforeSaveHookAsync> BeforeSaveHooks { get; }

    /// <summary>
    ///     True while this context's hooks run. A hook may save the same DbContext again; that nested save must
    ///     leave this context alone instead of treating it as a leftover.
    /// </summary>
    public bool IsRunningHooks { get; set; }

    /// <summary>
    ///     The context below this one on its DbContext's stack: the save whose hook started this nested save, or a
    ///     leftover the next save discards. <c>null</c> at the bottom.
    /// </summary>
    public HookContext? Outer { get; }

    public SnapshotContext Snapshot { get; }

    /// <summary>
    ///     Set once the BeforeSave pass captured <see cref="Snapshot" /> for this save, so the AfterSave pass
    ///     reuses that capture instead of appending the still-pending entries again.
    /// </summary>
    public bool SnapshotCaptured { get; set; }

    #endregion

    #region Methods

    public void Dispose() => Snapshot.Dispose();

    public async ValueTask DisposeAsync() => await Snapshot.DisposeAsync();

    #endregion
}