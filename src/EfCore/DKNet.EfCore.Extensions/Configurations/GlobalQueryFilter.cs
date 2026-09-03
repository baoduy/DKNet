// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: GlobalQueryFilter.cs
// Description: Base class to apply global query filters to entity types at model build time.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
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

    /// <summary>
    ///     Tracks <see cref="IsIgnorable" /> by <see cref="FilterKey" /> for every <see cref="GlobalQueryFilter" />
    ///     that has been applied to a model, so <see cref="IgnorableFilterKeys" /> can report which registered
    ///     filters are safe to bypass.
    /// </summary>
    private static readonly ConcurrentDictionary<string, bool> KnownFilterKeys = new();

    /// <summary>
    ///     Cached snapshot of <see cref="IgnorableFilterKeys" />, rebuilt whenever <see cref="Apply" /> registers a
    ///     filter. <see cref="KnownFilterKeys" /> only changes during model building, so recomputing the array on
    ///     every read (as <c>SpecificationExtensions.ApplySpecs</c> does, once per specification) is pure waste.
    /// </summary>
    private static string[] _ignorableFilterKeysCache = [];

    #endregion

    #region Properties

    /// <summary>
    ///     The query filter key used to identify this filter.
    ///     Support from EF Core 10 onwards for named query filters.
    /// </summary>
    public abstract string FilterKey { get; }

    /// <summary>
    ///     Whether this filter may be bypassed via <c>ISpecification.IsIgnoreQueryFilters</c>. Defaults to
    ///     <c>true</c>, preserving today's bypass behaviour for any filter that doesn't override it (e.g. a future
    ///     soft-delete filter). Override to <c>false</c> for filters that must never be bypassed this way, such as
    ///     row-level tenant/ownership isolation.
    /// </summary>
    public virtual bool IsIgnorable => true;

    /// <summary>
    ///     The <see cref="FilterKey" /> of every registered <see cref="GlobalQueryFilter" /> whose
    ///     <see cref="IsIgnorable" /> is <c>true</c> — the set of filters that
    ///     <c>SpecificationExtensions.ApplySpecs</c> is allowed to bypass.
    /// </summary>
    public static IReadOnlyCollection<string> IgnorableFilterKeys => _ignorableFilterKeysCache;

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
        KnownFilterKeys[FilterKey] = IsIgnorable;
        _ignorableFilterKeysCache = KnownFilterKeys.Where(kv => kv.Value).Select(kv => kv.Key).ToArray();

        foreach (var entityType in entityTypes)
        {
            var genericMethod = _method.MakeGenericMethod(entityType.ClrType);
            try
            {
                // Invoke the generic ApplyQueryFilter<TEntity>(ModelBuilder, DbContext)
                genericMethod.Invoke(this, [modelBuilder, context]);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // Unwrap so callers (and tests) see the real exception, not a reflection wrapper.
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
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
        var filter = HasQueryFilter<TEntity>(context);
        if (filter is not null) modelBuilder.Entity<TEntity>().HasQueryFilter(FilterKey, filter);
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