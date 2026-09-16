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
    ///     Determines whether the given operation was excluded.
    /// </summary>
    /// <param name="operation">The operation to check.</param>
    /// <returns><see langword="true" /> when <paramref name="operation" /> was excluded.</returns>
    public bool IsExcluded(CrudOp operation) => _excluded.Contains(operation);

    /// <summary>
    ///     Registers a setting to run against every generated route of the given operation kind. Additive:
    ///     several calls for the same operation all run, in call order.
    /// </summary>
    /// <param name="operation">The operation kind the setting applies to.</param>
    /// <param name="configure">The setting to apply to each matching route's <see cref="RouteHandlerBuilder" />.</param>
    /// <returns>This instance, so calls can be chained.</returns>
    /// <exception cref="NotImplementedException">Always — DRK-1327 Build stage implements this member.</exception>
    public CrudMapOptions Configure(CrudOp operation, Action<RouteHandlerBuilder> configure) =>
        throw new NotImplementedException();

    /// <summary>
    ///     Registers a setting to run against the one generated route carrying the given name. Additive:
    ///     several calls for the same name all run, in call order.
    /// </summary>
    /// <param name="routeName">The route's name (see the package documentation for the naming rule).</param>
    /// <param name="configure">The setting to apply to the named route's <see cref="RouteHandlerBuilder" />.</param>
    /// <returns>This instance, so calls can be chained.</returns>
    /// <exception cref="NotImplementedException">Always — DRK-1327 Build stage implements this member.</exception>
    public CrudMapOptions Configure(string routeName, Action<RouteHandlerBuilder> configure) =>
        throw new NotImplementedException();

    /// <summary>
    ///     Validates that every route name configured via <see cref="Configure(string, Action{RouteHandlerBuilder})" />
    ///     is one of the entity's <paramref name="knownRouteNames" />.
    /// </summary>
    /// <param name="entityName">The entity's name, used in the exception message.</param>
    /// <param name="knownRouteNames">Every route name the entity has, compared ordinally.</param>
    /// <exception cref="ArgumentException">A configured route name is not in <paramref name="knownRouteNames" />.</exception>
    /// <exception cref="NotImplementedException">Always — DRK-1327 Build stage implements this member.</exception>
    public void ValidateRouteNames(string entityName, params string[] knownRouteNames) =>
        throw new NotImplementedException();

    /// <summary>
    ///     Runs every setting configured for <paramref name="operation" /> and for <paramref name="routeName" />,
    ///     operation-kind settings first, against the route's <see cref="RouteHandlerBuilder" />.
    /// </summary>
    /// <param name="operation">The route's operation kind.</param>
    /// <param name="routeName">The route's name.</param>
    /// <param name="builder">The route's <see cref="RouteHandlerBuilder" />.</param>
    /// <exception cref="NotImplementedException">Always — DRK-1327 Build stage implements this member.</exception>
    public void Apply(CrudOp operation, string routeName, RouteHandlerBuilder builder) =>
        throw new NotImplementedException();
}
