using System.Collections.Concurrent;
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
/// <param name="provider">the IServiceProvider of HookRunner</param>
/// <param name="logger">the logger of HookRunner</param>
internal sealed class HookRunnerInterceptor(IServiceProvider provider, ILogger<HookRunnerInterceptor> logger)
    : SaveChangesInterceptor
{
    #region Fields

    private readonly ConcurrentDictionary<Guid, HookContext> _cache = new();

    #endregion

    #region Methods

    private HookContext GetContext(DbContextEventData eventData) =>
        _cache.GetOrAdd(
            eventData.Context!.ContextId.InstanceId,
            _ => new HookContext(provider, eventData.Context!));

    private async Task RemoveContext(DbContextEventData eventData)
    {
        if (_cache.TryRemove(eventData.Context!.ContextId.InstanceId, out var context)) await context.DisposeAsync();
    }

    /// <summary>
    ///     Runs hooks before and after save operations.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="type"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task RunHooksAsync(
        HookContext context,
        RunningTypes type,
        CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation(
                "Running {Type} hooks. BeforeSaveHooks: {BeforeCount}, AfterSaveHooks: {AfterCount}",
                type,
                context.BeforeSaveHooks.Count,
                context.AfterSaveHooks.Count);

        if (context.Snapshot.Entities.Count == 0) return;

        var tasks = new List<Task>();
        tasks.AddRange(
            type == RunningTypes.BeforeSave
                ? context.BeforeSaveHooks.Select(h => h.BeforeSaveAsync(context.Snapshot, cancellationToken))
                : context.AfterSaveHooks.Select(h => h.AfterSaveAsync(context.Snapshot, cancellationToken)));

        await Task.WhenAll(tasks);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await RemoveContext(eventData);
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{Name}:SavedChangesAsync called with result: {EventId}",
                nameof(HookRunnerInterceptor), eventData.EventId);

        if (eventData.Context == null || HookDisablingContext.IsHookDisabled(eventData.Context!))
            return await base.SavedChangesAsync(eventData, result, cancellationToken);

        try
        {
            var context = GetContext(eventData);
            await RunHooksAsync(context, RunningTypes.AfterSave, cancellationToken);
        }
        finally
        {
            await RemoveContext(eventData);
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("{Name}:SavedChangesAsync the event context was removed: {EventId}",
                    nameof(HookRunnerInterceptor), eventData.EventId);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    ///     Run Before Save to prepare the component for the hooks.
    /// </summary>
    /// <param name="eventData"></param>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{Name}:SavingChangesAsync called with result: {EventId}",
                nameof(HookRunnerInterceptor), eventData.EventId);

        if (eventData.Context == null || HookDisablingContext.IsHookDisabled(eventData.Context!))
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var context = GetContext(eventData);
        await RunHooksAsync(context, RunningTypes.BeforeSave, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    #endregion
}