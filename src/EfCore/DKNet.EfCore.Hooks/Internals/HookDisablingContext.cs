// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: HookDisablingContext.cs
// Description: Internal helper to temporarily disable hooks for a specific DbContext type, scoped to the current async flow.

using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Hooks.Internals;

/// <summary>
///     Provides a disposable context that temporarily disables hooks for the given DbContext type.
///     The disabling is reference-counted so nested callers can safely create multiple disable scopes.
/// </summary>
public interface IHookDisablingContext : IDisposable, IAsyncDisposable;

/// <summary>
///     Implementation of <see cref="IHookDisablingContext" /> which increments a ref-count for the
///     current DbContext type on construction and decrements it on disposal. When the count is > 0,
///     hooks are considered disabled for that DbContext type.
///     The ref-counts are held in an <see cref="AsyncLocal{T}" /> so disabling is scoped to the current
///     logical call flow (and its async continuations) instead of the whole process - concurrent,
///     unrelated flows are unaffected.
/// </summary>
internal sealed class HookDisablingContext : IHookDisablingContext
{
    #region Fields

    /// <summary>
    ///     Ref-count map storing how many active disable scopes exist per DbContext CLR type, scoped to the
    ///     current logical call context. <see cref="AsyncLocal{T}.Value" /> is always replaced with a new
    ///     immutable map rather than mutated in place, which is what keeps concurrent flows isolated.
    /// </summary>
    private static readonly AsyncLocal<ImmutableDictionary<string, int>> DisabledHooks = new();

    private readonly string _typeName;
    private bool _disposed;

    #endregion

    #region Constructors

    /// <summary>
    ///     Creates and activates a disabling context for the provided <paramref name="context" />.
    /// </summary>
    /// <param name="context">The DbContext instance whose type will have hooks disabled while this scope is active.</param>
    public HookDisablingContext(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _typeName = context.GetType().FullName!;

        var current = DisabledHooks.Value ?? ImmutableDictionary<string, int>.Empty;
        DisabledHooks.Value = current.SetItem(_typeName, current.GetValueOrDefault(_typeName) + 1);
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Synchronously dispose the disabling context and decrement the reference count. The underlying
    ///     DbContext is not disposed by this operation. Idempotent - a second call is a no-op.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var current = DisabledHooks.Value ?? ImmutableDictionary<string, int>.Empty;
        var newCount = Math.Max(0, current.GetValueOrDefault(_typeName) - 1);
        DisabledHooks.Value = newCount == 0 ? current.Remove(_typeName) : current.SetItem(_typeName, newCount);
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
    ///     Checks whether hooks are currently disabled for the provided DbContext instance's CLR type within
    ///     the current logical call flow.
    /// </summary>
    /// <param name="context">The DbContext to check.</param>
    /// <returns>True when the ref-count for the context's type is greater than zero; otherwise false.</returns>
    public static bool IsHookDisabled(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = DisabledHooks.Value;
        return current is not null && current.TryGetValue(context.GetType().FullName!, out var count) && count > 0;
    }

    #endregion
}
