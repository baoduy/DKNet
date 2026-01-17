using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

internal sealed class HookContext : IDisposable, IAsyncDisposable
{
    #region Fields

    private readonly IServiceScope _scope;

    #endregion

    #region Constructors

    public HookContext(IServiceProvider provider, DbContext db)
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

    public void Dispose()
    {
        _scope.Dispose();
        Snapshot.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_scope is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else _scope.Dispose();
        await Snapshot.DisposeAsync();
    }

    #endregion
}