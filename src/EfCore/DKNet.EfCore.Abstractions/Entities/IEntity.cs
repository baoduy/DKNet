// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
///     Defines the base contract for all entities in the system.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
///     This interface establishes the fundamental structure for entities,
///     ensuring they have a unique identifier of a specified type.
/// </remarks>
public interface IEntity<out TKey>
{
    #region Properties

    /// <summary>
    ///     Gets the unique identifier for the entity.
    /// </summary>
    /// <value>The entity's primary key value.</value>
    TKey Id { get; }

    #endregion
}