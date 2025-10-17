using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Hooks.Internals;

public interface IHookDisablingContext : IDisposable, IAsyncDisposable;

internal sealed class HookDisablingContext : IHookDisablingContext
{
    private readonly DbContext _context;
    private static readonly ConcurrentDictionary<Type, int> DisabledHooks = [];

    public static bool IsHookDisabled(DbContext context) =>
        DisabledHooks.TryGetValue(context.GetType(), out var count) && count > 0;

    public HookDisablingContext(DbContext context)
    {
        _context = context;
        DisabledHooks.AddOrUpdate(context.GetType(), 1, (_, oldValue) => oldValue + 1);
    }

    public void Dispose()
    {
        DisabledHooks.AddOrUpdate(_context.GetType(), 0, (_, oldValue) => oldValue - 1);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}