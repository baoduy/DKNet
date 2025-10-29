// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: AuditLogEntry.cs
// Description: Lightweight audit log models used to record entity changes (keys, fields, who and when).

using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.AuditLogs;

/// <summary>
///     Represents a single changed field on an audited entity, containing the previous and new values.
/// </summary>
public sealed record AuditFieldChange
{
    #region Properties

    /// <summary>
    ///     The new value for the field after the change, or <c>null</c> when not applicable.
    /// </summary>
    public object? NewValue { get; init; }

    /// <summary>
    ///     The previous value for the field before the change, or <c>null</c> when not applicable.
    /// </summary>
    public object? OldValue { get; init; }

    /// <summary>
    ///     The name of the field that changed.
    /// </summary>
    public required string FieldName { get; init; }

    #endregion
}

/// <summary>
///     The action performed on the audited entity.
/// </summary>
public enum AuditLogAction
{
    /// <summary>Entity was created.</summary>
    Created,

    /// <summary>Entity was updated.</summary>
    Updated,

    /// <summary>Entity was deleted.</summary>
    Deleted
}

/// <summary>
///     Audit log entry capturing a single entity-level operation. Implements <see cref="IAuditedProperties" />
///     so it carries basic audit metadata (who/when created/updated).
/// </summary>
public sealed record AuditLogEntry : IAuditedProperties
{
    #region Properties

    /// <summary>
    ///     The high-level action performed (Created, Updated, Deleted).
    /// </summary>
    public required AuditLogAction Action { get; init; }

    /// <summary>
    ///     The time the audit entry was created.
    /// </summary>
    public required DateTimeOffset CreatedOn { get; init; }

    /// <summary>
    ///     Optional last update timestamp for the audit entry.
    /// </summary>
    public DateTimeOffset? UpdatedOn { get; init; }

    /// <summary>
    ///     The primary key values that identify the affected entity. Keys are stored as a dictionary
    ///     (property name => value) to support composite keys and generic entity shapes.
    /// </summary>
    public required IDictionary<string, object?> Keys { get; init; }

    /// <summary>
    ///     The list of individual field changes that were part of the operation. For creates/deletes this
    ///     list may contain the full set of fields or be empty depending on how the caller records changes.
    /// </summary>
    public required IReadOnlyList<AuditFieldChange> Changes { get; init; }

    /// <summary>
    ///     Identifier of the principal (user/service) that created the audit entry.
    /// </summary>
    public required string CreatedBy { get; init; }

    /// <summary>
    ///     The CLR type name or logical entity name for the audited entity (for example "Order").
    /// </summary>
    public required string EntityName { get; init; }

    /// <summary>
    ///     Identifier of the principal that last updated the audit entry, if applicable.
    /// </summary>
    public string? UpdatedBy { get; init; }

    #endregion
}