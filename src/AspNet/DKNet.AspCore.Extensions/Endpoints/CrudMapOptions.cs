// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: CrudMapOptions.cs
// Description: Exclusion options consulted by generated Map{Entity}Crud endpoint-registration extensions.

using Microsoft.AspNetCore.Builder;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     The individual HTTP operations a generated <c>Map{Entity}Crud</c> extension registers.
/// </summary>
public enum CrudOp
{
    /// <summary>The GET-by-id endpoint.</summary>
    GetById,

    /// <summary>The paged GET-list endpoint.</summary>
    GetList,

    /// <summary>The POST create endpoint.</summary>
    Create,

    /// <summary>The PUT update endpoint(s).</summary>
    Update,

    /// <summary>The DELETE-by-id endpoint.</summary>
    Delete,

    /// <summary>The generated domain-action endpoint(s).</summary>
    Action
}

/// <summary>
///     Options consulted by a generated <c>Map{Entity}Crud</c> extension to skip individual CRUD
///     operations. Nothing is excluded by default.
/// </summary>
public sealed class CrudMapOptions
{
    private readonly HashSet<CrudOp> _excluded = [];
    private readonly Dictionary<CrudOp, List<Action<RouteHandlerBuilder>>> _opSettings = [];
    private readonly Dictionary<string, List<Action<RouteHandlerBuilder>>> _routeSettings = new(StringComparer.Ordinal);

    /// <summary>
    ///     Excludes the given operations from registration.
    /// </summary>
    /// <param name="operations">The operations to exclude.</param>
    /// <returns>This instance, so calls can be chained.</returns>
    public CrudMapOptions Exclude(params CrudOp[] operations)
    {
        foreach (var operation in operations)
            _excluded.Add(operation);

        return this;
    }

    /// <summary>
    ///     Excludes the one generated route carrying the given member name, leaving the entity's other routes
    ///     of the same kind published (see the package documentation for the naming rule). Excluding a name no
    ///     generated route carries is reported by <see cref="ValidateRouteNames" />, never silently ignored.
    /// </summary>
    /// <param name="routeNames">The route name(s) to exclude.</param>
    /// <returns>This instance, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="routeNames" /> or one of its elements is <see langword="null" />.</exception>
    public CrudMapOptions Exclude(params string[] routeNames) => throw new NotImplementedException();

    /// <summary>
    ///     Determines whether the given operation was excluded.
    /// </summary>
    /// <param name="operation">The operation to check.</param>
    /// <returns><see langword="true" /> when <paramref name="operation" /> was excluded.</returns>
    public bool IsExcluded(CrudOp operation) => _excluded.Contains(operation);

    /// <summary>
    ///     Determines whether the route carrying the given name was excluded.
    /// </summary>
    /// <param name="routeName">The route name to check.</param>
    /// <returns><see langword="true" /> when <paramref name="routeName" /> was excluded.</returns>
    public bool IsExcluded(string routeName) => throw new NotImplementedException();

    /// <summary>
    ///     Registers a setting to run against every generated route of the given operation kind. Additive:
    ///     several calls for the same operation all run, in call order.
    /// </summary>
    /// <param name="operation">The operation kind the setting applies to.</param>
    /// <param name="configure">The setting to apply to each matching route's <see cref="RouteHandlerBuilder" />.</param>
    /// <returns>This instance, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure" /> is <see langword="null" />.</exception>
    public CrudMapOptions Configure(CrudOp operation, Action<RouteHandlerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (!_opSettings.TryGetValue(operation, out var settings))
        {
            settings = [];
            _opSettings[operation] = settings;
        }

        settings.Add(configure);
        return this;
    }

    /// <summary>
    ///     Registers a setting to run against the one generated route carrying the given name. Additive:
    ///     several calls for the same name all run, in call order.
    /// </summary>
    /// <param name="routeName">The route's name (see the package documentation for the naming rule).</param>
    /// <param name="configure">The setting to apply to the named route's <see cref="RouteHandlerBuilder" />.</param>
    /// <returns>This instance, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure" /> is <see langword="null" />.</exception>
    public CrudMapOptions Configure(string routeName, Action<RouteHandlerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (!_routeSettings.TryGetValue(routeName, out var settings))
        {
            settings = [];
            _routeSettings[routeName] = settings;
        }

        settings.Add(configure);
        return this;
    }

    /// <summary>
    ///     Validates that every route name configured via <see cref="Configure(string, Action{RouteHandlerBuilder})" />
    ///     is one of the entity's <paramref name="knownRouteNames" />.
    /// </summary>
    /// <param name="entityName">The entity's name, used in the exception message.</param>
    /// <param name="knownRouteNames">Every route name the entity has, compared ordinally.</param>
    /// <exception cref="ArgumentException">A configured route name is not in <paramref name="knownRouteNames" />.</exception>
    public void ValidateRouteNames(string entityName, params string[] knownRouteNames)
    {
        var known = new HashSet<string>(knownRouteNames, StringComparer.Ordinal);
        // Sorted ordinally rather than left in dictionary-enumeration order: Dictionary<TKey,TValue> key
        // order is unspecified (insertion order in practice, but not a contract), and the message needs to
        // be deterministic.
        var unknown = _routeSettings.Keys.Where(name => !known.Contains(name)).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length == 0) return;

        throw new ArgumentException(
            $"Entity '{entityName}' has no route(s) named {string.Join(", ", unknown.Select(n => $"'{n}'"))}. " +
            $"Known route names: {string.Join(", ", knownRouteNames.Select(n => $"'{n}'"))}.");
    }

    /// <summary>
    ///     Runs every setting configured for <paramref name="operation" /> and for <paramref name="routeName" />,
    ///     operation-kind settings first, against the route's <see cref="RouteHandlerBuilder" />.
    /// </summary>
    /// <param name="operation">The route's operation kind.</param>
    /// <param name="routeName">The route's name.</param>
    /// <param name="builder">The route's <see cref="RouteHandlerBuilder" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    public void Apply(CrudOp operation, string routeName, RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (_opSettings.TryGetValue(operation, out var opSettings))
            foreach (var configure in opSettings)
                configure(builder);

        if (_routeSettings.TryGetValue(routeName, out var routeSettings))
            foreach (var configure in routeSettings)
                configure(builder);
    }
}
