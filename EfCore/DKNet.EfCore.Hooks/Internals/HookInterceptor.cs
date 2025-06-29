using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

/// <summary>
/// A factory-based interceptor that resolves the appropriate HookRunner for each DbContext type.
/// This avoids the issue of resolving instances during configuration time.
/// </summary>
internal sealed class HookInterceptor(IServiceProvider serviceProvider) : ISaveChangesInterceptor
{
    private readonly ConcurrentDictionary<Type, HookRunnerInstance> _hookRunners = new();

    private HookRunnerInstance GetHookRunner(DbContext context)
    {
        var contextType = context.GetType();
        return _hookRunners.GetOrAdd(contextType, type =>
        {
            var fullName = type.FullName!;
            // Try to get the keyed service, if not found, return null
            var runner = serviceProvider.GetKeyedService<HookRunner>(fullName);
            return new HookRunnerInstance(runner);
        });
    }

    #region ISaveChangesInterceptor Implementation

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var hookRunner = GetHookRunner(eventData.Context!);
        return hookRunner.Runner?.SavingChanges(eventData, result) ?? result;
    }

    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var hookRunner = GetHookRunner(eventData.Context!);
        return hookRunner.Runner != null 
            ? await hookRunner.Runner.SavingChangesAsync(eventData, result, cancellationToken)
            : result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        var hookRunner = GetHookRunner(eventData.Context!);
        return hookRunner.Runner?.SavedChanges(eventData, result) ?? result;
    }

    public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        var hookRunner = GetHookRunner(eventData.Context!);
        return hookRunner.Runner != null
            ? await hookRunner.Runner.SavedChangesAsync(eventData, result, cancellationToken)
            : result;
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        var hookRunner = GetHookRunner(eventData.Context!);
        hookRunner.Runner?.SaveChangesFailed(eventData);
    }

    public async Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var hookRunner = GetHookRunner(eventData.Context!);
        if (hookRunner.Runner != null)
            await hookRunner.Runner.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    #endregion

    /// <summary>
    /// Wrapper to hold the HookRunner instance (which might be null if not registered)
    /// </summary>
    private sealed class HookRunnerInstance(HookRunner? runner)
    {
        public HookRunner? Runner { get; } = runner;
    }
}