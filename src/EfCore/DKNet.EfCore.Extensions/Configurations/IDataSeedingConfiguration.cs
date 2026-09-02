// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IDataSeedingConfiguration.cs
// Description: Abstractions and base implementation for EF Core data seeding configurations.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DKNet.EfCore.Extensions.Configurations;

/// <summary>
///     Describes a data seeding configuration for an entity type. Implementations may provide a synchronous
///     list of data asynchronous seeding callback via <see cref="SeedAsync" />.
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
    ///     The CLR <see cref="Type" /> of the entity that this seeding configuration targets.
    /// </summary>
    Type EntityType { get; }

    #endregion
}

/// <summary>
///     Generic base class for data seeding configurations. Implementers can provide model-managed seed data via
///     <see cref="GetDataAsync" /> or an asynchronous seed routine via <see cref="SeedAsync" />.
/// </summary>
/// <typeparam name="TEntity">The entity type to seed.</typeparam>
public abstract class DataSeedingConfiguration<TEntity> : IDataSeedingConfiguration where TEntity : class
{
    #region Properties

    /// <inheritdoc />
    public Type EntityType => typeof(TEntity);

    /// <inheritdoc />
    public virtual int Order => 0;

    /// <inheritdoc />
    public virtual Func<DbContext, bool, CancellationToken, Task> SeedAsync =>
        async (context, isMigration, cancellation) =>
        {
            var data = await GetDataAsync(cancellation).ConfigureAwait(false);
            if (data.Count == 0)
                return;

            var dbSet = context.Set<TEntity>();
            var toAdd = await GetMissingAsync(context, dbSet, data, cancellation).ConfigureAwait(false);
            if (toAdd.Count == 0)
                return;

            await dbSet.AddRangeAsync(toAdd, cancellation).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellation).ConfigureAwait(false);
        };

    #endregion

    #region Methods

    /// <summary>
    ///     Gets the collection of seed data for the target entity type./>
    /// </summary>
    /// <returns></returns>
    protected abstract ValueTask<ICollection<TEntity>> GetDataAsync(CancellationToken cancellation = default);

    /// <summary>
    ///     Filters <paramref name="candidates" /> down to the ones not already present in <paramref name="dbSet" />,
    ///     comparing by primary key rather than by <typeparamref name="TEntity" /> equality (which, for a plain
    ///     reference type without an <c>Equals</c> override, would never match the freshly materialised database rows and
    ///     would re-insert every candidate on every run). Only the primary key columns are read from the database.
    /// </summary>
    private static async Task<List<TEntity>> GetMissingAsync(
        DbContext context,
        DbSet<TEntity> dbSet,
        ICollection<TEntity> candidates,
        CancellationToken cancellation)
    {
        var primaryKey = context.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count == 0)
            // No primary key metadata to compare by; treat every candidate as new.
            return candidates.ToList();

        var keyProperties = primaryKey.Properties;
        var keyDefaults = keyProperties
            .Select(p => p.ClrType.IsValueType ? Activator.CreateInstance(p.ClrType) : null)
            .ToArray();

        var existingKeys = await dbSet.AsNoTracking()
            .Select(BuildKeySelector(keyProperties))
            .ToListAsync(cancellation)
            .ConfigureAwait(false);
        var existingSet = new HashSet<object?[]>(existingKeys, KeyArrayComparer.Instance);

        var toAdd = new List<TEntity>();
        foreach (var item in candidates)
        {
            var entry = context.Entry(item);
            var keyValues = keyProperties.Select(p => entry.Property(p.Name).CurrentValue).ToArray();

            // An entity whose key still has its CLR default value has never been assigned one, so it is new.
            var isUnset = keyValues.SequenceEqual(keyDefaults);
            if (isUnset || !existingSet.Contains(keyValues))
                toAdd.Add(item);
        }

        return toAdd;
    }

    /// <summary>
    ///     Builds a projection expression that reads only the given primary key properties from an entity,
    ///     via <see cref="EF.Property{TProperty}" /> so shadow key properties are supported too.
    /// </summary>
    private static Expression<Func<TEntity, object?[]>> BuildKeySelector(IReadOnlyList<IProperty> keyProperties)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var propertyMethod = typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(object));

        var accessors = keyProperties
            .Select(p => (Expression)Expression.Call(propertyMethod, parameter, Expression.Constant(p.Name)));

        return Expression.Lambda<Func<TEntity, object?[]>>(Expression.NewArrayInit(typeof(object), accessors), parameter);
    }

    #endregion

    #region KeyArrayComparer

    /// <summary>
    ///     Compares primary key value arrays element-by-element, so composite keys are supported.
    /// </summary>
    private sealed class KeyArrayComparer : IEqualityComparer<object?[]>
    {
        public static readonly KeyArrayComparer Instance = new();

        public bool Equals(object?[]? x, object?[]? y) =>
            x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(object?[] obj)
        {
            var hash = default(HashCode);
            foreach (var value in obj)
                hash.Add(value);
            return hash.ToHashCode();
        }
    }

    #endregion
}
