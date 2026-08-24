// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ListQuery.cs
// Description: Validates the filter/ordering parameters of the generic list endpoint into a specification input.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using DKNet.EfCore.Specifications.Dynamics;
using DKNet.EfCore.Specifications.Extensions;
using LinqKit;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     The validated filter and ordering inputs a generic list endpoint hands to its specification.
/// </summary>
/// <typeparam name="TEntity">Entity type the filter predicate applies to.</typeparam>
/// <param name="Filter">Combined filter predicate, or <see langword="null" /> when no filter was requested.</param>
/// <param name="OrderBy">
///     PascalCase name of the property to order by, or <see langword="null" /> to keep the endpoint's default
///     ordering.
/// </param>
/// <param name="Descending">Whether <paramref name="OrderBy" /> sorts descending.</param>
internal sealed record ListQuery<TEntity>(
    Expression<Func<TEntity, bool>>? Filter,
    string? OrderBy,
    bool Descending)
    where TEntity : class;

/// <summary>
///     Validates the <see cref="ListFilter" /> conditions and <c>orderBy</c> field of the generic list endpoints
///     against the model they project to, and compiles the conditions into a single predicate.
/// </summary>
/// <remarks>
///     Two rules make this safe to expose generically over any entity:
///     <list type="bullet">
///         <item>
///             A field is filterable/sortable only when <c>TModel</c> — the projection the endpoint already
///             returns — declares it. Nothing the caller cannot already see becomes a filter, so no field can
///             be used as an oracle and no hidden column can be sorted on.
///         </item>
///         <item>
///             An unusable condition is rejected, never silently dropped. Dropping it would answer a filtered
///             query with unfiltered data.
///         </item>
///     </list>
/// </remarks>
internal static class ListQuery
{
    #region Fields

    /// <summary>OpenAPI description for the repeatable <c>filter</c> parameter.</summary>
    internal const string FilterDescription =
        "Filter as 'field:operation:value', repeatable; conditions combine with AND. Operations: " +
        "Equal, NotEqual, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Contains, NotContains, " +
        "StartsWith, EndsWith, In, NotIn. In/NotIn take a comma-separated value list. Only fields present on " +
        "the returned model can be filtered; anything else is rejected with 400.";

    /// <summary>OpenAPI description for the <c>orderBy</c> parameter.</summary>
    internal const string OrderByDescription =
        "Name of a field on the returned model to sort by. Omit to keep the endpoint's default ordering.";

    /// <summary>OpenAPI description for the <c>search</c> parameter.</summary>
    internal const string SearchDescription =
        "Free-text search: matches rows where any text field of the returned model contains this value. " +
        "Case sensitivity follows the database collation. Combines with 'filter' using AND. Omit or leave " +
        "blank for no search; to match a non-text field exactly, use 'filter' instead.";

    /// <summary>Property names per type, so a case-insensitive lookup cannot throw on case-only overloads.</summary>
    private static readonly ConcurrentDictionary<Type, HashSet<string>> PropertyNames = new();

    #endregion

    #region Methods

    /// <summary>
    ///     Validates the bound inputs of a generic list endpoint and compiles its filter conditions.
    /// </summary>
    /// <typeparam name="TEntity">Entity type the filters are applied to.</typeparam>
    /// <typeparam name="TModel">Projection model whose properties define what may be filtered and sorted.</typeparam>
    /// <param name="request">The endpoint's bound query-string parameters.</param>
    /// <param name="query">The validated inputs on success; otherwise <see langword="null" />.</param>
    /// <param name="error">A caller-facing reason on failure; otherwise <see langword="null" />.</param>
    /// <returns><see langword="true" /> when every input was valid; otherwise <see langword="false" />.</returns>
    internal static bool TryValidate<TEntity, TModel>(
        ListQueryRequest request,
        out ListQuery<TEntity>? query,
        out string? error)
        where TEntity : class
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(request);

        query = null;
        Expression<Func<TEntity, bool>>? combined = null;
        var search = request.Search;
        var orderBy = request.OrderBy;

        foreach (var filter in request.Filter ?? [])
        {
            if (!TryBuild<TEntity, TModel>(filter, out var expression, out error)) return false;
            combined = combined is null ? expression : combined.And(expression!);
        }

        // A blank search is simply absent — no condition, so the endpoint behaves exactly as it does without
        // the parameter. A non-blank one ANDs its whole OR-group onto the filters.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchExpression = Search<TEntity, TModel>(search.Trim());
            combined = combined is null ? searchExpression : combined.And(searchExpression);
        }

        var order = orderBy.ToPascalCase();
        if (order.Length == 0)
            order = null;
        else if (!Declares<TModel>(order) || !Declares<TEntity>(order))
        {
            error = $"Cannot sort by '{orderBy}': no such field on {typeof(TModel).Name}.";
            return false;
        }

        error = null;
        query = new ListQuery<TEntity>(combined, order, request.IsDescending);
        return true;
    }

    /// <summary>
    ///     Validates one condition against <typeparamref name="TModel" /> and compiles it into a predicate over
    ///     <typeparamref name="TEntity" />.
    /// </summary>
    /// <typeparam name="TEntity">Entity type the condition applies to.</typeparam>
    /// <typeparam name="TModel">Projection model whose properties define what may be filtered.</typeparam>
    /// <param name="filter">The condition to compile.</param>
    /// <param name="expression">The compiled predicate on success; otherwise <see langword="null" />.</param>
    /// <param name="error">A caller-facing reason on failure; otherwise <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the condition was valid; otherwise <see langword="false" />.</returns>
    private static bool TryBuild<TEntity, TModel>(
        ListFilter filter,
        out Expression<Func<TEntity, bool>>? expression,
        out string? error)
        where TEntity : class
        where TModel : class
    {
        expression = null;

        var property = filter.Field.ToPascalCase();
        if (!Declares<TModel>(property))
        {
            error = $"Cannot filter by '{filter.Field}': no such field on {typeof(TModel).Name}.";
            return false;
        }

        // In/NotIn bind against a collection; every other operation takes the value verbatim and lets the
        // predicate builder coerce it to the property's CLR type.
        object value = filter.Operation is Ops.In or Ops.NotIn
            ? filter.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : filter.Value;

        if (!DynamicPredicateExtensions.TryBuildPredicate<TEntity>(property, filter.Operation, value,
                out expression))
        {
            error = $"Cannot filter by '{filter.Field}': '{filter.Value}' is not a valid " +
                    $"{filter.Operation} value for it.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    ///     Builds the free-text search predicate: every text field of <typeparamref name="TModel" /> OR'd
    ///     together.
    /// </summary>
    /// <remarks>
    ///     A model with no text field cannot match a search, so the predicate matches nothing and the endpoint
    ///     answers with an empty page. That is the honest answer rather than an error — and note it is the
    ///     opposite of dropping the condition, which would answer with every row.
    /// </remarks>
    /// <typeparam name="TEntity">Entity type the search applies to.</typeparam>
    /// <typeparam name="TModel">Projection model whose text fields are searched.</typeparam>
    /// <param name="search">The search text.</param>
    /// <returns>The search predicate.</returns>
    private static Expression<Func<TEntity, bool>> Search<TEntity, TModel>(string search)
        where TEntity : class
        where TModel : class
    {
        var clauses = ModelSearch.Clauses<TModel, TEntity>();
        if (clauses.Length == 0) return _ => false;

        var predicate = PredicateBuilder.New<TEntity>();
        foreach (var clause in clauses) predicate = predicate.DynamicOr(clause, search);

        return predicate;
    }

    /// <summary>Determines whether <typeparamref name="T" /> declares a public instance property by name.</summary>
    /// <typeparam name="T">The type to inspect.</typeparam>
    /// <param name="name">Property name; matched case-insensitively.</param>
    /// <returns><see langword="true" /> when the property exists; otherwise <see langword="false" />.</returns>
    private static bool Declares<T>(string name) =>
        PropertyNames.GetOrAdd(
                typeof(T),
                static type => type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase))
            .Contains(name);

    #endregion
}
