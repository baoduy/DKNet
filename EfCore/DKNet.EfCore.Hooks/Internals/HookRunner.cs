using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using DKNet.EfCore.Extensions.Snapshots;

namespace DKNet.EfCore.Hooks.Internals;

/// <summary>
/// The hook running types
/// </summary>
public enum RunningTypes
{
    /// <summary>
    /// Before save operation
    /// </summary>
    BeforeSave,

    /// <summary>
    /// After save operation
    /// </summary>
    AfterSave,
}

/// <summary>
/// Runs hooks before and after save operations.
/// </summary>
/// <param name="hookLoader"></param>
/// <param name="logger"></param>
internal sealed class HookRunner(HookFactory hookLoader, ILogger<HookRunner> logger) : ISaveChangesInterceptor
{
    private bool _initialized;
    private readonly ConcurrentQueue<string> _callersQueue = new();
    private SnapshotContext? _snapshotContext;
    private IEnumerable<IBeforeSaveHookAsync> _beforeSaveHooks = [];
    private IEnumerable<IAfterSaveHookAsync> _afterSaveHooks = [];

    #region Not Support SavingChanges

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        => throw new NotSupportedException($"Please use {nameof(SavingChangesAsync)} version");

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        => throw new NotSupportedException($"Please use {nameof(SavingChangesAsync)} version");

    public void SaveChangesFailed(DbContextErrorEventData eventData)
        => logger.LogError("SaveChanges failed. This method should not be called as only Async recommended to be used in a hook.");

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    #endregion


    /// <summary>
    /// Initializes the hook by loading the hooks and setting up the context.
    /// </summary>
    /// <param name="eventData"></param>
    private void InitHook(DbContextEventData eventData)
    {
        //Validate and Mark the caller queues
        if (eventData.Context is null) throw new ArgumentNullException(nameof(eventData));

        if (_snapshotContext is null)
        {
            _callersQueue.Enqueue(eventData.EventIdCode);
            //Preparing the hook data and hooks before executing the hook
            _snapshotContext = eventData.Context.Snapshot();
        }

        if (!_initialized)
        {
            var (beforeSaveHooks, afterSaveHooks) = hookLoader.LoadHooks(eventData.Context);
            _afterSaveHooks = afterSaveHooks;
            _beforeSaveHooks = beforeSaveHooks;
            //mark initialized flag
            _initialized = true;
        }
    }

    /// <summary>
    /// Runs hooks before and after save operations.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task RunHooksAsync(RunningTypes type, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(_snapshotContext);

        //Run Hooks Async
        if (type == RunningTypes.BeforeSave)
        {
            //foreach (var h in _beforeSaveHookAsync.Where(h => !context.DbContext.IsHookDisabled(h)))
            foreach (var h in _beforeSaveHooks)
                await h.RunBeforeSaveAsync(_snapshotContext, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            //foreach (var h in _afterSaveHookAsync.Where(h => !context.DbContext.IsHookDisabled(h)))
            foreach (var h in _afterSaveHooks)
                await h.RunAfterSaveAsync(_snapshotContext, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Run Before Save to prepare the component for the hooks.
    /// </summary>
    /// <param name="eventData"></param>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        //Init Hook
        InitHook(eventData);

        //Run Before Save
        await RunHooksAsync(RunningTypes.BeforeSave, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        try
        {
            //Run After Events and ignore the result even failed.
            _callersQueue.TryDequeue(out _);
            await RunHooksAsync(RunningTypes.AfterSave, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (_callersQueue.IsEmpty)
            {
                _snapshotContext?.Dispose();
                _snapshotContext = null;
            }
        }

        return result;
    }
}