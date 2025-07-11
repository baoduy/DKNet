using DKNet.EfCore.Extensions.Snapshots;

namespace DKNet.EfCore.Hooks;

public interface IHookBaseAsync;

/// <summary>
///     The interface Hook allows to handle the Before Save Async actions of DbContext
/// </summary>
public interface IBeforeSaveHookAsync : IHookBaseAsync
{
    Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default);
}

/// <summary>
///     The interface Hook allows handling the After Save Async actions of DbContext
/// </summary>
public interface IAfterSaveHookAsync : IHookBaseAsync
{
    Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default);
}

/// <summary>
///     The interface HookAsync allows to handle the Before Save and After Save async actions of DbContext
/// </summary>
public interface IHookAsync : IBeforeSaveHookAsync, IAfterSaveHookAsync;