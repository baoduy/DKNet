using System.Collections.Concurrent;
using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DKNet.EfCore.Hooks.Internals;

/// <summary>
///     The hook running types
/// </summary>
public enum RunningTypes
{
    /// <summary>
    ///     Before save operation
    /// </summary>
    BeforeSave,

    /// <summary>
    ///     After save operation
    /// </summary>
    AfterSave
}

/// <summary>
///     Runs hooks before and after save operations.
/// </summary>
/// <param name="logger"></param>
internal sealed class HookRunner(IServiceProvider provider, ILogger<HookRunner> logger) : ISaveChangesInterceptor
{
    //private readonly ConcurrentQueue<string> _callersQueue = new();
    private readonly ConcurrentDictionary<Guid, HookRunnerContext> _cache = new();

    private HookRunnerContext GetContext(DbContextEventData eventData) =>
        _cache.GetOrAdd(eventData.Context!.ContextId.InstanceId,
            _ => new HookRunnerContext(provider, eventData.Context!));

    private async Task RemoveContext(DbContextEventData eventData)
    {
        if (_cache.TryRemove(eventData.Context!.ContextId.InstanceId, out var context))
            await context.DisposeAsync();
    }

    /// <summary>
    ///     Run Before Save to prepare the component for the hooks.
    /// </summary>
    /// <param name="eventData"></param>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = GetContext(eventData);
        await RunHooksAsync(context, RunningTypes.BeforeSave, cancellationToken);
        return result;
    }

    public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = GetContext(eventData);
            await RunHooksAsync(context, RunningTypes.AfterSave, cancellationToken);
        }
        finally
        {
            await RemoveContext(eventData);
        }

        return result;
    }


    /// <summary>
    ///     Runs hooks before and after save operations.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="type"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task RunHooksAsync(HookRunnerContext context, RunningTypes type,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Running {Type} hooks. BeforeSaveHooks: {BeforeCount}, AfterSaveHooks: {AfterCount}",
            type, context.BeforeSaveHooks.Count, context.AfterSaveHooks.Count);

        if (context.Snapshot.Entities.Count == 0) return;

        var tasks = new List<Task>();
        tasks.AddRange(type == RunningTypes.BeforeSave
            ? context.BeforeSaveHooks.Select(h => h.RunBeforeSaveAsync(context.Snapshot, cancellationToken))
            : context.AfterSaveHooks.Select(h => h.RunAfterSaveAsync(context.Snapshot, cancellationToken)));

        await Task.WhenAll(tasks);
    }


    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        logger.LogInformation("Hook only supports Async version of SaveChanges.");
        return result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        logger.LogInformation("Hook only supports Async version of SaveChanges.");
        return result;
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        logger.LogError(
            "SaveChanges failed. This method should not be called as only Async recommended to be used in a hook.");
    }

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}