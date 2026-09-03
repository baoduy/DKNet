using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
/// <param name="logger">the logger of HookRunner</param>
internal sealed partial class HookRunnerInterceptor(ILogger<HookRunnerInterceptor> logger)
    : SaveChangesInterceptor, IAsyncDisposable, IDisposable
{
    #region Fields

    private readonly ConcurrentDictionary<Guid, HookContext> _cache = new();

    #endregion

    #region Methods

    public void Dispose()
    {
        var contexts = _cache.Values;
        _cache.Clear();
        foreach (var context in contexts) context.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        var contexts = _cache.Values;
        _cache.Clear();
        foreach (var context in contexts) await context.DisposeAsync();
    }

    private HookContext GetContext(DbContextEventData eventData) =>
        _cache.GetOrAdd(
            eventData.Context!.ContextId.InstanceId,
            _ => new HookContext(GetApplicationServiceProvider(eventData.Context!), eventData.Context!));

    /// <summary>
    ///     Resolves the DbContext's own application service provider, so hooks are loaded from the same DI
    ///     scope as the DbContext itself instead of a detached scope off the root provider.
    /// </summary>
    /// <param name="db">the DbContext to resolve the application service provider from</param>
    /// <exception cref="InvalidOperationException">
    ///     thrown when <paramref name="db" /> was not registered via <c>AddDbContextWithHook</c>/<c>AddDbContext</c>,
    ///     so it has no application service provider to resolve hooks from.
    /// </exception>
    private static IServiceProvider GetApplicationServiceProvider(DbContext db) =>
        db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider
        ?? throw new InvalidOperationException(
            $"The DbContext '{db.GetType().Name}' has no application service provider. " +
            "It must be registered via AddDbContextWithHook or AddDbContext.");

    private async Task RemoveContext(DbContextEventData eventData)
    {
        if (_cache.TryRemove(eventData.Context!.ContextId.InstanceId, out var context))
            await context.DisposeAsync();
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
        if (HookDisablingContext.IsHookDisabled(context.Snapshot.DbContext))
        {
            LogHooksDisabled(type, context.Snapshot.DbContext.ContextId);
            return;
        }

        LogRunningHooks(type, context.BeforeSaveHooks.Count, context.AfterSaveHooks.Count);

        context.Snapshot.Initialize();
        if (context.Snapshot.Entities.Count == 0) return;

        if (type == RunningTypes.BeforeSave)
            foreach (var hook in context.BeforeSaveHooks)
                await hook.BeforeSaveAsync(context.Snapshot, cancellationToken);
        else
            foreach (var hook in context.AfterSaveHooks)
                await hook.AfterSaveAsync(context.Snapshot, cancellationToken);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogSaveChangesFailed(eventData.EventId, eventData.EventIdCode);

        await RemoveContext(eventData);
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogSavedChangesCalled(eventData.EventId, eventData.EventIdCode);

        try
        {
            var context = GetContext(eventData);
            await RunHooksAsync(context, RunningTypes.AfterSave, cancellationToken);
        }
        finally
        {
            await RemoveContext(eventData);
            LogSavedChangesContextRemoved(eventData.EventId, eventData.EventIdCode);
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
        LogSavingChangesCalled(eventData.EventId, eventData.EventIdCode);

        var context = GetContext(eventData);
        await RunHooksAsync(context, RunningTypes.BeforeSave, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    #endregion

    #region Logging

    // Source-generated: the level check compiles in first and arguments are passed through a strongly
    // typed struct, so a disabled level allocates nothing — unlike the hand-written LogInformation calls
    // this replaces, some of which had no IsEnabled guard at all.

    [LoggerMessage(Level = LogLevel.Information, Message = "The {Type} hooks is disabled for DbContext {ContextId}")]
    private partial void LogHooksDisabled(RunningTypes type, DbContextId contextId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Running {Type} hooks. BeforeSaveHooks: {BeforeCount}, AfterSaveHooks: {AfterCount}")]
    private partial void LogRunningHooks(RunningTypes type, int beforeCount, int afterCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HookRunnerInterceptor:SaveChangesFailedAsync {EventId}, {EventIdCode}")]
    private partial void LogSaveChangesFailed(EventId eventId, string? eventIdCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HookRunnerInterceptor:SavedChangesAsync called with result: {EventId}, {EventIdCode}")]
    private partial void LogSavedChangesCalled(EventId eventId, string? eventIdCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HookRunnerInterceptor:SavedChangesAsync the event context was removed: {EventId}, {EventIdCode}")]
    private partial void LogSavedChangesContextRemoved(EventId eventId, string? eventIdCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HookRunnerInterceptor:SavingChangesAsync called with result: {EventId}, {EventIdCode}")]
    private partial void LogSavingChangesCalled(EventId eventId, string? eventIdCode);

    #endregion
}