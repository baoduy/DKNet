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
    ///     The order in which this seeding configuration should be applied relative to other configurations. Configurations
    ///     with
    /// </summary>
    int Order { get; }

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
///     <see cref="GetData" /> or an asynchronous seed routine via <see cref="SeedAsync" />.
/// </summary>
/// <typeparam name="TEntity">The entity type to seed.</typeparam>
public abstract class DataSeedingConfiguration<TEntity> : IDataSeedingConfiguration where TEntity : class
{
    #region Properties

    /// <inheritdoc />
    public Type EntityType => typeof(TEntity);

    /// <inheritdoc />
    public IEnumerable<dynamic> HasData => GetData();

    /// <inheritdoc />
    public virtual int Order => 0;


    /// <inheritdoc />
    public virtual Func<DbContext, bool, CancellationToken, Task>? SeedAsync => null;

    #endregion

    #region Methods

    /// <summary>
    ///     Gets the collection of seed data for the target entity type./>
    /// </summary>
    /// <returns></returns>
    protected abstract ICollection<TEntity> GetData();

    #endregion
}