using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Hooks.Internals;

public interface IHookDisablingContext : IDisposable, IAsyncDisposable;

internal sealed class HookDisablingContext : IHookDisablingContext
{
    #region Fields

    private readonly DbContext _context;
    private static readonly ConcurrentDictionary<Type, int> DisabledHooks = [];

    #endregion

    #region Constructors

    public HookDisablingContext(DbContext context)
    {
        this._context = context;
        DisabledHooks.AddOrUpdate(context.GetType(), 1, (_, oldValue) => oldValue + 1);
    }

    #endregion

    #region Methods

    public void Dispose()
    {
        DisabledHooks.AddOrUpdate(this._context.GetType(), 0, (_, oldValue) => oldValue - 1);
    }

    public ValueTask DisposeAsync()
    {
        this.Dispose();
        return ValueTask.CompletedTask;
    }

    public static bool IsHookDisabled(DbContext context) =>
        DisabledHooks.TryGetValue(context.GetType(), out var count) && count > 0;

    #endregion
}