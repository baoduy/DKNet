using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

internal sealed class HookRunnerContext : IAsyncDisposable
{
    #region Fields

    //private readonly IServiceScope _scope;

    #endregion

    #region Constructors

    public HookRunnerContext(IServiceProvider provider, DbContext db)
    {
        //this._scope = provider.CreateScope();
        var factory = provider.GetRequiredService<HookFactory>();
        var (before, afters) = factory.LoadHooks(db);
        this.BeforeSaveHooks = [..before];
        this.AfterSaveHooks = [..afters];
        this.Snapshot = new SnapshotContext(db);
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
        await this.Snapshot.DisposeAsync();
    }

    #endregion
}