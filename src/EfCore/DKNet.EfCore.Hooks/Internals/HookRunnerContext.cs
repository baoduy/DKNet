using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

internal sealed class HookRunnerContext : IAsyncDisposable
{
    #region Fields

    private readonly IServiceScope _scope;

    #endregion

    #region Constructors

    public HookRunnerContext(IServiceProvider provider, DbContext db)
    {
        _scope = provider.CreateScope();
        var factory = _scope.ServiceProvider.GetRequiredService<HookFactory>();
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
        _scope.Dispose();
        await Snapshot.DisposeAsync();
    }

    #endregion
}