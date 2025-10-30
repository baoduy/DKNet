// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IOwnedBy.cs
// Description: Marker interface used to indicate an entity is owned by a specific principal (for data authorization).

namespace DKNet.EfCore.DataAuthorization;

/// <summary>
///     Indicates that an entity instance is owned by a specific principal (for example a user or tenant) via
///     the <see cref="OwnedBy" /> property. Implement this interface on entities that should participate
///     in data-ownership based authorization checks.
/// </summary>
public interface IOwnedBy
{
    #region Properties

    /// <summary>
    ///     The identifier of the owning principal (for example a user id or tenant id).
    ///     This value is used by data-authorization filters to restrict access to owner-specific records.
    /// </summary>
    string OwnedBy { get; }

    #endregion
}