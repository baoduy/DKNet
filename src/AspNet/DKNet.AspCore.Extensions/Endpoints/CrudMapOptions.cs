// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: CrudMapOptions.cs
// Description: Exclusion options consulted by generated Map{Entity}Crud endpoint-registration extensions.

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
}
