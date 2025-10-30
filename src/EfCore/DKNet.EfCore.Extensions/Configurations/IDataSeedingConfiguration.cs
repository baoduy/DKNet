// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IDataSeedingConfiguration.cs
// Description: Abstractions and base implementation for EF Core data seeding configurations.

namespace DKNet.EfCore.Extensions.Configurations;

/// <summary>
///     Describes a data seeding configuration for an entity type. Implementations may provide a synchronous
///     list of data via <see cref="HasData" />, and/or an asynchronous seeding callback via <see cref="SeedAsync" />.
/// </summary>
public interface IDataSeedingConfiguration
{
    #region Properties

    /// <summary>
    ///     Optional asynchronous seeding callback. The function receives the current <see cref="DbContext" />, a boolean
    ///     indicating whether the seeding call should run as part of migrations/initialization, a
    ///     <see cref="CancellationToken" />,
    ///     and returns a <see cref="Task" /> that completes when seeding is finished. Return <c>false</c> from the callback to
    ///     indicate the seed was not applied (implementation-specific semantics may vary).
    /// </summary>
    Func<DbContext, bool, CancellationToken, Task>? SeedAsync { get; }

    /// <summary>
    ///     Model-managed seed data (collection of anonymous/dynamic objects) that will be used by EF Core's model seeding
    ///     support.
    ///     Implementations may expose strongly typed collections via the generic base class and map them to this property.
    /// </summary>
    IEnumerable<dynamic> HasData { get; }

    /// <summary>
    ///     The CLR <see cref="Type" /> of the entity that this seeding configuration targets.
    /// </summary>
    Type EntityType { get; }

    #endregion
}

/// <summary>
///     Generic base class for data seeding configurations. Implementers can provide model-managed seed data via
///     <see cref="HasData" /> or an asynchronous seed routine via <see cref="SeedAsync" />.
/// </summary>
/// <typeparam name="TEntity">The entity type to seed.</typeparam>
public abstract class DataSeedingConfiguration<TEntity> : IDataSeedingConfiguration where TEntity : class
{
    #region Properties

    /// <inheritdoc />
    public Type EntityType => typeof(TEntity);

    /// <summary>
    ///     Strongly typed collection of seed data for the entity. Implementations may populate this collection with
    ///     instances of <typeparamref name="TEntity" /> to be used as model-managed seed data.
    /// </summary>
    protected virtual ICollection<TEntity> HasData { get; } = new List<TEntity>();

    /// <inheritdoc />
    IEnumerable<dynamic> IDataSeedingConfiguration.HasData => HasData;

    /// <summary>
    ///     Optional asynchronous seed callback. By defaults this is <c>null</c> which means no runtime seeding action is
    ///     provided.
    ///     Override to supply an async seeding routine.
    /// </summary>
    public virtual Func<DbContext, bool, CancellationToken, Task>? SeedAsync => null;

    #endregion
}