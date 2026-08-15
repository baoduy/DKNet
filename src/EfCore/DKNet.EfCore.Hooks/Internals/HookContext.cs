using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

internal sealed class HookContext : IDisposable, IAsyncDisposable
{
    #region Constructors

    public HookContext(IServiceProvider provider, DbContext db)
    {
        var factory = provider.GetRequiredService<HookFactory>();
        var (before, afters) = factory.LoadHooks(db);
        BeforeSaveHooks = [..before];
        AfterSaveHooks = [..afters];
        Snapshot = new SnapshotContext(db);
    }

    #endregion

    #region Properties

    public IReadOnlyCollection<IAfterSaveHookAsync> AfterSaveHooks { get; }

    public IReadOnlyCollection<IBeforeSaveHookAsync> BeforeSaveHooks { get; }

    public SnapshotContext Snapshot { get; }

    #endregion

    #region Methods

    public void Dispose() => Snapshot.Dispose();

    public async ValueTask DisposeAsync() => await Snapshot.DisposeAsync();

    #endregion
}