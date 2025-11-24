// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
///     Defines a contract for auditable entities with a specified key type.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
///     This interface combines entity identification, audit properties, and
///     concurrency control capabilities.
/// </remarks>
public interface IAuditedEntity<out TKey> : IEntity<TKey>, IAuditedProperties
{
    // #region Methods
    //
    // /// <summary>
    // ///     Sets the creation audit information for the entity.
    // /// </summary>
    // /// <param name="userName">The identifier of the creating user.</param>
    // /// <param name="createdOn">Optional creation timestamp.</param>
    // protected void SetCreatedBy(string userName, DateTimeOffset? createdOn = null);
    //
    // /// <summary>
    // ///     Sets the update audit information for the entity.
    // /// </summary>
    // /// <param name="userName">The identifier of the updating user.</param>
    // /// <param name="updatedOn">Optional update timestamp.</param>
    // protected void SetUpdatedBy(string userName, DateTimeOffset? updatedOn = null);
    //
    // #endregion
}