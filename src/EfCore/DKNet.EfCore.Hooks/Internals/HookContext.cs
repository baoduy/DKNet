using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

internal sealed class HookContext : IAsyncDisposable
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

    public async ValueTask DisposeAsync()
    {
        //this._scope.Dispose();
        await Snapshot.DisposeAsync();
    }

    #endregion

    //private readonly IServiceScope _scope;
}