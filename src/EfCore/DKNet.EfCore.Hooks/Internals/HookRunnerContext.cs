using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

internal sealed class HookRunnerContext : IAsyncDisposable
{
    private readonly IServiceScope _scope;

    public HookRunnerContext(IServiceProvider provider, DbContext db)
    {
        _scope = provider.CreateScope();
        var factory = _scope.ServiceProvider.GetRequiredService<HookFactory>();
        var (before, afters) = factory.LoadHooks(db);
        BeforeSaveHooks = [..before];
        AfterSaveHooks = [..afters];
        Snapshot = new SnapshotContext(db);
    }

    public SnapshotContext Snapshot { get; }
    public IReadOnlyCollection<IAfterSaveHookAsync> AfterSaveHooks { get; }
    public IReadOnlyCollection<IBeforeSaveHookAsync> BeforeSaveHooks { get; }

    public async ValueTask DisposeAsync()
    {
        _scope.Dispose();
        await Snapshot.DisposeAsync();
    }
}