// <copyright file="DomainEntity.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.DtoEntities.Share;

/// <summary>
///     Base class for domain entities that require auditing and concurrency control.
/// </summary>
/// <remarks>
///     This class provides automatic audit tracking and optimistic concurrency control using row versioning.
/// </remarks>
public abstract class DomainEntity : AuditedEntity<Guid>, IConcurrencyEntity<byte[]>
{
    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainEntity" /> class with a new GUID identifier.
    /// </summary>
    /// <param name="byUser">The user creating the entity.</param>
    protected DomainEntity(string byUser)
    {
        this.SetCreatedBy(byUser);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainEntity" /> class with a specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the entity.</param>
    /// <param name="createdBy">The user creating the entity.</param>
    protected DomainEntity(Guid id, string createdBy) : base(id)
    {
        this.SetCreatedBy(createdBy);
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the row version for optimistic concurrency control.
    /// </summary>
    public byte[]? RowVersion { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    ///     Sets the row version for concurrency control.
    /// </summary>
    /// <param name="rowVersion">The row version bytes.</param>
    public void SetRowVersion(byte[] rowVersion)
    {
        this.RowVersion = rowVersion;
    }

    #endregion
}