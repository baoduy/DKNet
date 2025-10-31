#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IHook.cs
// Description: Hook interfaces and base implementation used to run actions before and after DbContext.Save operations.

using DKNet.EfCore.Extensions.Snapshots;

namespace DKNet.EfCore.Hooks;

/// <summary>
///     Marker interface for asynchronous hook types. Implement specific hook interfaces to participate
///     in the DbContext save pipeline (for example, <see cref="IBeforeSaveHookAsync" />).
/// </summary>
public interface IHookBaseAsync;

/// <summary>
///     Allows handling actions that must run before the DbContext Save operation completes.
///     Implementers receive a <see cref="SnapshotContext" /> capturing the tracked entries at the time of invocation.
/// </summary>
public interface IBeforeSaveHookAsync : IHookBaseAsync
{
    #region Methods

    /// <summary>
    ///     Called before the DbContext persists changes. Use the provided snapshot to inspect entries.
    /// </summary>
    /// <param name="context">Captured snapshot context containing entries and metadata.</param>
    /// <param name="cancellationToken">Cancellation token to observe while performing asynchronous work.</param>
    Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
///     Allows handling actions that must run after the DbContext Save operation completes.
///     Implementers receive a <see cref="SnapshotContext" /> capturing the tracked entries at the time of invocation.
/// </summary>
public interface IAfterSaveHookAsync : IHookBaseAsync
{
    #region Methods

    /// <summary>
    ///     Called after the DbContext has persisted changes. Use the provided snapshot to inspect entries.
    /// </summary>
    /// <param name="context">Captured snapshot context containing entries and metadata.</param>
    /// <param name="cancellationToken">Cancellation token to observe while performing asynchronous work.</param>
    Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
///     Combined hook interface that includes both before- and after-save callbacks.
///     Implement this interface when you need to participate in both phases.
/// </summary>
public interface IHookAsync : IBeforeSaveHookAsync, IAfterSaveHookAsync;

/// <summary>
///     A convenience base class that implements <see cref="IHookAsync" /> with no-op virtual methods.
///     Inherit from this class and override the methods you need for your hook implementation.
/// </summary>
public abstract class HookAsync : IHookAsync
{
    #region Methods

    /// <summary>
    ///     Called after save operation; default implementation does nothing.
    /// </summary>
    /// <param name="context">Captured snapshot context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    ///     Called before save operation; default implementation does nothing.
    /// </summary>
    /// <param name="context">Captured snapshot context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    #endregion
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member