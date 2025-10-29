// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: GlobalQueryFilter.cs
// Description: Base class to apply global query filters to entity types at model build time.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DKNet.EfCore.Extensions.Configurations;

/// <summary>
///     Base helper to register global query filters for a selection of entity types when the EF Core model is built.
///     Derive from this class and implement <see cref="GetEntityTypes" /> and <see cref="HasQueryFilter{TEntity}" />
///     to provide per-entity filter expressions.
/// </summary>
public abstract class GlobalQueryFilter : IGlobalModelBuilder
{
    #region Fields

    /// <summary>
    ///     Cached reflection <see cref="MethodInfo" /> for the generic <see cref="ApplyQueryFilter{TEntity}" /> method.
    ///     The method is invoked via reflection for each matched entity type.
    /// </summary>
    private readonly MethodInfo _method = typeof(GlobalQueryFilter)
        .GetMethod(nameof(ApplyQueryFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    #endregion

    #region Methods

    /// <summary>
    ///     Applies configured global query filters for all entity types returned by <see cref="GetEntityTypes" />.
    /// </summary>
    /// <param name="modelBuilder">The EF Core <see cref="ModelBuilder" /> instance being configured.</param>
    /// <param name="context">The current <see cref="DbContext" />, provided to allow runtime-aware filters.</param>
    public void Apply(ModelBuilder modelBuilder, DbContext context)
    {
        var entityTypes = GetEntityTypes(modelBuilder);

        foreach (var entityType in entityTypes)
        {
            var genericMethod = _method.MakeGenericMethod(entityType.ClrType);
            // Invoke the generic ApplyQueryFilter<TEntity>(ModelBuilder, DbContext)
            genericMethod.Invoke(this, [modelBuilder, context]);
        }
    }

    /// <summary>
    ///     Reflection-invoked generic method that asks for a filter for <typeparamref name="TEntity" /> and applies
    ///     it to the model when a non-null expression is returned.
    /// </summary>
    /// <typeparam name="TEntity">The entity CLR type being configured.</typeparam>
    /// <param name="modelBuilder">The EF Core <see cref="ModelBuilder" /> instance.</param>
    /// <param name="context">The current <see cref="DbContext" /> used to produce the filter expression.</param>
    private void ApplyQueryFilter<TEntity>(ModelBuilder modelBuilder, DbContext context)
        where TEntity : class
    {
        // TODO: convert to named query filter when migrate to EFCore 10
        var filter = HasQueryFilter<TEntity>(context);
        if (filter is not null) modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

    /// <summary>
    ///     Return the set of entity types the filter should be applied to.
    ///     Implementations should select the appropriate <see cref="IMutableEntityType" /> instances from the model.
    /// </summary>
    /// <param name="modelBuilder">The EF Core <see cref="ModelBuilder" /> instance.</param>
    /// <returns>A sequence of mutable entity types to which the filter will be applied.</returns>
    protected abstract IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder);

    /// <summary>
    ///     Provide a filter expression for the given entity type when building the model.
    ///     Return <c>null</c> when no filter should be applied for the specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity CLR type for which a filter may be provided.</typeparam>
    /// <param name="context">The current <see cref="DbContext" />, which can be used to read runtime information.</param>
    /// <returns>An expression that evaluates to true for entities that should be visible, or <c>null</c> to skip.</returns>
    protected abstract Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context)
        where TEntity : class;

    #endregion
}