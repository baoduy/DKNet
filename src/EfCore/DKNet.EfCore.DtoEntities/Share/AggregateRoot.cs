// <copyright file="AggregateRoot.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.EfCore.DtoEntities.Share;

/// <summary>
///     Base class for aggregate root entities in the domain model.
/// </summary>
/// <remarks>
///     An aggregate root is the main entity in a domain-driven design aggregate.
///     It ensures consistency and encapsulation of business rules within the aggregate boundary.
/// </remarks>
public abstract class AggregateRoot : DomainEntity
{
    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="AggregateRoot" /> class with a new GUID identifier.
    /// </summary>
    /// <param name="byUser">The user creating the aggregate root.</param>
    protected AggregateRoot(string byUser) : base(byUser)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AggregateRoot" /> class with a specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the aggregate root.</param>
    /// <param name="createdBy">The user creating the aggregate root.</param>
    protected AggregateRoot(Guid id, string createdBy) : base(id, createdBy)
    {
    }

    #endregion
}