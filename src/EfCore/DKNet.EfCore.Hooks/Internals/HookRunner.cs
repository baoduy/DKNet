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
/// <param name="hookLoader"></param>
/// <param name="logger"></param>
internal sealed class HookRunner(HookFactory hookLoader, ILogger<HookRunner> logger)
    : ISaveChangesInterceptor, IDisposable, IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _callersQueue = new();
    private IEnumerable<IAfterSaveHookAsync> _afterSaveHooks = [];
    private IEnumerable<IBeforeSaveHookAsync> _beforeSaveHooks = [];
    private bool _initialized;
    private SnapshotContext? _snapshotContext;

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
        //Init Hook
        EnsureHookInitialized(eventData);

        if (_snapshotContext is null || _snapshotContext.SnapshotEntities.Count <= 0) return result;
        //Run Before Save
        await RunHooksAsync(RunningTypes.BeforeSave, cancellationToken);
        return result;
    }

    public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_snapshotContext is null || _snapshotContext.SnapshotEntities.Count <= 0) return result;
            //Run After Events and ignore the result even failed.
            _callersQueue.TryDequeue(out _);
            await RunHooksAsync(RunningTypes.AfterSave, cancellationToken);
        }
        finally
        {
            if (_callersQueue.IsEmpty && _snapshotContext is not null)
            {
                //Dispose the snapshot context if no more callers are left
                logger.LogDebug("Disposing Snapshot Context");
                //Dispose the snapshot context

                await _snapshotContext.DisposeAsync();
                _snapshotContext = null;
            }
        }

        return result;
    }


    /// <summary>
    ///     Initializes the hook by loading the hooks and setting up the context.
    /// </summary>
    /// <param name="eventData"></param>
    private void EnsureHookInitialized(DbContextEventData eventData)
    {
        //Validate and Mark the caller queues
        if (eventData.Context is null) throw new ArgumentNullException(nameof(eventData));

        if (_snapshotContext is null)
        {
            _callersQueue.Enqueue(eventData.EventIdCode);
            //Preparing the hook data and hooks before executing the hook
            _snapshotContext = new SnapshotContext(eventData.Context);
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
    ///     Runs hooks before and after save operations.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task RunHooksAsync(RunningTypes type, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(_snapshotContext);

        logger.LogInformation("Running {Type} hooks. BeforeSaveHooks: {BeforeCount}, AfterSaveHooks: {AfterCount}",
            type, _beforeSaveHooks.Count(), _afterSaveHooks.Count());

        //Run Hooks Async
        if (type == RunningTypes.BeforeSave)
            //foreach (var h in _beforeSaveHookAsync.Where(h => !context.DbContext.IsHookDisabled(h)))
            foreach (var h in _beforeSaveHooks)
                await h.RunBeforeSaveAsync(_snapshotContext, cancellationToken);
        else
            //foreach (var h in _afterSaveHookAsync.Where(h => !context.DbContext.IsHookDisabled(h)))
            foreach (var h in _afterSaveHooks)
                await h.RunAfterSaveAsync(_snapshotContext, cancellationToken);
    }


    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new NotSupportedException($"Please use {nameof(SavingChangesAsync)} version");

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result) =>
        throw new NotSupportedException($"Please use {nameof(SavingChangesAsync)} version");

    public void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        logger.LogError(
            "SaveChanges failed. This method should not be called as only Async recommended to be used in a hook.");
    }

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void Dispose()
    {
        _snapshotContext?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_snapshotContext != null) await _snapshotContext.DisposeAsync();
    }
}