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
/// <param name="logger"></param>
internal sealed class HookRunner(IServiceProvider provider, ILogger<HookRunner> logger) : SaveChangesInterceptor
{
    #region Fields

    private readonly ConcurrentDictionary<Guid, HookRunnerContext> _cache = new();

    #endregion

    #region Methods

    private HookRunnerContext GetContext(DbContextEventData eventData) =>
        this._cache.GetOrAdd(
            eventData.Context!.ContextId.InstanceId,
            _ => new HookRunnerContext(provider, eventData.Context!));

    private async Task RemoveContext(DbContextEventData eventData)
    {
        if (this._cache.TryRemove(eventData.Context!.ContextId.InstanceId, out var context))
        {
            await context.DisposeAsync();
        }
    }

    /// <summary>
    ///     Runs hooks before and after save operations.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="type"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task RunHooksAsync(
        HookRunnerContext context,
        RunningTypes type,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Running {Type} hooks. BeforeSaveHooks: {BeforeCount}, AfterSaveHooks: {AfterCount}",
            type,
            context.BeforeSaveHooks.Count,
            context.AfterSaveHooks.Count);

        if (context.Snapshot.Entities.Count == 0)
        {
            return;
        }

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
        await this.RemoveContext(eventData);
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context == null)
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        if (HookDisablingContext.IsHookDisabled(eventData.Context!))
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        try
        {
            var context = this.GetContext(eventData);
            await this.RunHooksAsync(context, RunningTypes.AfterSave, cancellationToken);
        }
        finally
        {
            await this.RemoveContext(eventData);
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
        if (eventData.Context == null)
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        if (HookDisablingContext.IsHookDisabled(eventData.Context!))
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var context = this.GetContext(eventData);
        await this.RunHooksAsync(context, RunningTypes.BeforeSave, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    #endregion
}