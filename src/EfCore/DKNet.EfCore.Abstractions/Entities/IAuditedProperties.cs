// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
///     Defines the basic auditing properties required for tracking entity changes.
/// </summary>
/// <remarks>
///     This interface provides the fundamental properties needed for maintaining
///     audit trails of entity creation and modifications.
/// </remarks>
public interface IAuditedProperties
{
    #region Properties

    /// <summary>
    ///     Gets the timestamp when the entity was created.
    /// </summary>
    [IgnoreAuditLog]
    DateTimeOffset CreatedOn { get; }

    /// <summary>
    ///     Gets the timestamp when the entity was last updated.
    /// </summary>
    [IgnoreAuditLog]
    DateTimeOffset? UpdatedOn { get; }

    /// <summary>
    ///     Gets the identifier of the user who created the entity.
    /// </summary>
    [IgnoreAuditLog]
    [MaxLength(500)]
    string CreatedBy { get; }

    /// <summary>
    ///     Gets the identifier of the user who last updated the entity.
    /// </summary>
    [MaxLength(500)]
    [IgnoreAuditLog]
    string? UpdatedBy { get; }

    #endregion
}