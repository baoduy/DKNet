using System.Runtime.CompilerServices;
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
///     Runs hooks before and after save operations. Hooks run only on <c>SaveChangesAsync</c>; a synchronous
///     <c>SaveChanges()</c> throws <see cref="NotSupportedException" /> unless hooks are disabled for the context
///     via <c>DisableHooks()</c>.
/// </summary>
/// <param name="logger">the logger of HookRunner</param>
internal sealed partial class HookRunnerInterceptor(ILogger<HookRunnerInterceptor> logger)
    : SaveChangesInterceptor, IAsyncDisposable, IDisposable
{
    #region Fields

    // Keyed by the DbContext instance, so an entry left by an exit EF never signals (a later interceptor
    // throwing in SavingChangesAsync) dies with its DbContext instead of living on in this singleton.
    // The value is the top of a stack linked through HookContext.Outer: a hook that saves the same DbContext
    // pushes the nested save's context above its own.
    private readonly ConditionalWeakTable<DbContext, HookContext> _cache = new();

    #endregion

    #region Methods

    public void Dispose()
    {
        foreach (var context in TakeAllContexts()) context.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var context in TakeAllContexts()) await context.DisposeAsync();
    }

    private List<HookContext> TakeAllContexts()
    {
        var contexts = new List<HookContext>();
        foreach (var (_, top) in _cache)
            for (var context = top; context != null; context = context.Outer)
                contexts.Add(context);

        _cache.Clear();
        return contexts;
    }

    private static HookContext CreateContext(DbContext db) => new(GetApplicationServiceProvider(db), db);

    private HookContext GetContext(DbContextEventData eventData) => _cache.GetOrAdd(eventData.Context!, CreateContext);

    /// <summary>
    ///     Starts a save with a fresh hook context on top of its DbContext's stack. Contexts on top whose hooks
    ///     are not running are leftovers of saves EF never signalled the end of (a later interceptor threw), so
    ///     they are discarded. A context whose hooks are running belongs to the save that started this nested
    ///     save, so it stays below the new one.
    /// </summary>
    /// <param name="eventData"></param>
    private async Task PushContextAsync(DbContextEventData eventData)
    {
        var db = eventData.Context!;
        while (PopContext(db) is { } leftover)
            await leftover.DisposeAsync();

        // Unlink the live outer context (the new one keeps it as Outer), then create the new top.
        if (_cache.TryGetValue(db, out var outer)) _cache.Remove(db);
        _cache.GetOrAdd(db, d => new HookContext(GetApplicationServiceProvider(d), d, outer));
    }

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

    /// <summary>
    ///     Pops the top hook context of the DbContext's stack, leaving the outer one (if any) on top. A top whose
    ///     hooks are running is never popped: it belongs to the save whose hook started the exiting one. That
    ///     covers a sync save inside <c>DisableHooks()</c>, which never pushes, and a nested save whose AfterSave
    ///     failed, which EF signals a second time after <see cref="SavedChangesAsync" /> already popped it.
    /// </summary>
    /// <param name="db"></param>
    /// <returns>
    ///     the popped context, for the caller to dispose; <c>null</c> when the stack is empty or its top is running
    ///     hooks.
    /// </returns>
    private HookContext? PopContext(DbContext db)
    {
        if (!_cache.TryGetValue(db, out var context) || context.IsRunningHooks) return null;

        if (context.Outer is null) _cache.Remove(db);
        else _cache.AddOrUpdate(db, context.Outer);
        return context;
    }

    private async Task RemoveContextAsync(DbContextEventData eventData)
    {
        if (PopContext(eventData.Context!) is { } context)
            await context.DisposeAsync();
    }

    private void RemoveContext(DbContextEventData eventData) => PopContext(eventData.Context!)?.Dispose();

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

        // Every BeforeSave pass captures. AfterSave captures only when BeforeSave did not: with
        // acceptAllChangesOnSuccess: false the entries are still pending, so a second capture would
        // hand every entity to the AfterSave hooks twice.
        if (type == RunningTypes.BeforeSave || !context.SnapshotCaptured)
        {
            context.Snapshot.Initialize();
            context.SnapshotCaptured = true;
        }

        if (context.Snapshot.Entities.Count == 0) return;

        context.IsRunningHooks = true;
        try
        {
            if (type == RunningTypes.BeforeSave)
                foreach (var hook in context.BeforeSaveHooks)
                    await hook.BeforeSaveAsync(context.Snapshot, cancellationToken);
            else
                foreach (var hook in context.AfterSaveHooks)
                    await hook.AfterSaveAsync(context.Snapshot, cancellationToken);
        }
        finally
        {
            context.IsRunningHooks = false;

            // A nested save that a later interceptor failed got no end signal, so its context still sits above this
            // one. Drop it, so this save's exits pop this context and not the leftover.
            var db = context.Snapshot.DbContext;
            while (_cache.TryGetValue(db, out var top) && !ReferenceEquals(top, context) && PopContext(db) is { } leftover)
                await leftover.DisposeAsync();
        }
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogSaveChangesFailed(eventData.EventId, eventData.EventIdCode);

        await RemoveContextAsync(eventData);
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override async Task SaveChangesCanceledAsync(
        DbContextEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogSaveChangesCanceled(eventData.EventId, eventData.EventIdCode);

        await RemoveContextAsync(eventData);
        await base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    /// <summary>
    ///     A concurrency failure that no earlier interceptor suppressed ends the save, so its hook context is
    ///     evicted. A suppressed one lets the save continue to <see cref="SavedChangesAsync" />, which still needs
    ///     the BeforeSave snapshot.
    /// </summary>
    /// <param name="eventData"></param>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(
        ConcurrencyExceptionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        LogThrowingConcurrencyException(eventData.EventId, eventData.EventIdCode, result.IsSuppressed);

        // ponytail: an interceptor registered after this one that suppresses the exception finds the context
        // already evicted, so AfterSave gets a fresh context with an empty capture. Accepted as rare (DRK-1951
        // Q1); move eviction to the end callbacks if suppressing interceptors after this one become a real case.
        if (!result.IsSuppressed) await RemoveContextAsync(eventData);
        return await base.ThrowingConcurrencyExceptionAsync(eventData, result, cancellationToken);
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
            await RemoveContextAsync(eventData);
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

        // Never reuse a leftover: its snapshot would hand an earlier save's entries to the hooks again.
        await PushContextAsync(eventData);
        var context = GetContext(eventData);
        try
        {
            await RunHooksAsync(context, RunningTypes.BeforeSave, cancellationToken);
        }
        catch
        {
            // A hook throwing here ends the save before EF's own try, so no end callback will evict for us.
            await RemoveContextAsync(eventData);
            throw;
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    ///     Synchronous failure path: runs no hooks, only removes and disposes any cached hook context.
    /// </summary>
    /// <param name="eventData"></param>
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        LogSyncSaveChangesFailed(eventData.EventId, eventData.EventIdCode);

        RemoveContext(eventData);
        base.SaveChangesFailed(eventData);
    }

    /// <summary>
    ///     Synchronous cancellation path: runs no hooks, only removes and disposes any cached hook context.
    /// </summary>
    /// <param name="eventData"></param>
    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        RemoveContext(eventData);
        base.SaveChangesCanceled(eventData);
    }

    /// <summary>
    ///     Synchronous completion path: runs no hooks, only removes and disposes any cached hook context.
    /// </summary>
    /// <param name="eventData"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        LogSyncSavedChangesCalled(eventData.EventId, eventData.EventIdCode);

        RemoveContext(eventData);
        return base.SavedChanges(eventData, result);
    }

    /// <summary>
    ///     Hooks run only on <c>SaveChangesAsync</c>, so a synchronous save on a hook-enabled context fails closed
    ///     rather than saving with no hooks run. Inside a <c>DisableHooks()</c> scope it passes through.
    /// </summary>
    /// <param name="eventData"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">thrown when hooks are enabled for the DbContext.</exception>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        var db = eventData.Context!;
        if (!HookDisablingContext.IsHookDisabled(db))
            throw new NotSupportedException(
                "DKNet.EfCore.Hooks runs hooks only on SaveChangesAsync. " +
                $"Synchronous SaveChanges() on '{db.GetType().Name}' is not supported; " +
                "use SaveChangesAsync() or wrap the call in DisableHooks().");

        return base.SavingChanges(eventData, result);
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
        Message = "HookRunnerInterceptor:SaveChangesCanceledAsync {EventId}, {EventIdCode}")]
    private partial void LogSaveChangesCanceled(EventId eventId, string? eventIdCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HookRunnerInterceptor:ThrowingConcurrencyExceptionAsync {EventId}, {EventIdCode}, suppressed: {IsSuppressed}")]
    private partial void LogThrowingConcurrencyException(EventId eventId, string? eventIdCode, bool isSuppressed);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HookRunnerInterceptor:SavedChangesAsync called with result: {EventId}, {EventIdCode}")]
    private partial void LogSavedChangesCalled(EventId eventId, string? eventIdCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HookRunnerInterceptor:SavedChangesAsync the event context was removed: {EventId}, {EventIdCode}")]
    private partial void LogSavedChangesContextRemoved(EventId eventId, string? eventIdCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HookRunnerInterceptor:SavingChangesAsync called with result: {EventId}, {EventIdCode}")]
    private partial void LogSavingChangesCalled(EventId eventId, string? eventIdCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HookRunnerInterceptor:SaveChangesFailed {EventId}, {EventIdCode}")]
    private partial void LogSyncSaveChangesFailed(EventId eventId, string? eventIdCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "HookRunnerInterceptor:SavedChanges called with result: {EventId}, {EventIdCode}")]
    private partial void LogSyncSavedChangesCalled(EventId eventId, string? eventIdCode);

    #endregion
}