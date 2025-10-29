// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: HookDisablingContext.cs
// Description: Internal helper to temporarily disable hooks for a specific DbContext type using a ref-counted dictionary.

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Hooks.Internals;

/// <summary>
///     Provides a disposable context that temporarily disables hooks for the given DbContext type.
///     The disabling is reference-counted so nested callers can safely create multiple disable scopes.
/// </summary>
public interface IHookDisablingContext : IDisposable, IAsyncDisposable
{
}

/// <summary>
///     Implementation of <see cref="IHookDisablingContext" /> which increments a ref-count for the
///     current DbContext type on construction and decrements it on disposal. When the count is > 0,
///     hooks are considered disabled for that DbContext type.
/// </summary>
internal sealed class HookDisablingContext : IHookDisablingContext
{
    #region Fields

    /// <summary>
    ///     Ref-count dictionary storing how many active disable contexts exist per DbContext CLR type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, int> DisabledHooks = new();

    private readonly DbContext _context;

    #endregion

    #region Constructors

    /// <summary>
    ///     Creates and activates a disabling context for the provided <paramref name="context" />.
    /// </summary>
    /// <param name="context">The DbContext instance whose type will have hooks disabled while this scope is active.</param>
    public HookDisablingContext(DbContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));

        _context = context;

        DisabledHooks.AddOrUpdate(context.GetType(), 1, (_, oldValue) => oldValue + 1);
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Synchronously dispose the disabling context and decrement the reference count. The underlying
    ///     DbContext is not disposed by this operation.
    /// </summary>
    public void Dispose()
    {
        DisabledHooks.AddOrUpdate(_context.GetType(), 0, (_, oldValue) => Math.Max(0, oldValue - 1));
    }

    /// <summary>
    ///     Asynchronously dispose the disabling context. This simply calls the synchronous Dispose implementation
    ///     and returns a completed <see cref="ValueTask" />.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Checks whether hooks are currently disabled for the provided DbContext instance's CLR type.
    /// </summary>
    /// <param name="context">The DbContext to check.</param>
    /// <returns>True when the ref-count for the context's type is greater than zero; otherwise false.</returns>
    public static bool IsHookDisabled(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return DisabledHooks.TryGetValue(context.GetType(), out var count) && count > 0;
    }

    #endregion
}