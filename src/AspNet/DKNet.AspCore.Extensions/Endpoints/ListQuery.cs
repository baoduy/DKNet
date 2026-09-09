// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ListQuery.cs
// Description: Validates the filter/ordering parameters of the generic list endpoint into a specification input.

using System.Collections.Concurrent;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;
using DKNet.EfCore.Abstractions.Entities;
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
        "StartsWith, EndsWith, In, NotIn, IsNull, IsNotNull. In/NotIn take a comma-separated value list; " +
        "IsNull/IsNotNull take no value ('field:IsNull'). Only fields present on the returned model can be " +
        "filtered; anything else is rejected with 400. At most 20 conditions per request.";

    /// <summary>OpenAPI description for the <c>orderBy</c> parameter.</summary>
    internal const string OrderByDescription =
        "Name of a field on the returned model to sort by. Omit to keep the endpoint's default ordering.";

    /// <summary>OpenAPI description for the <c>search</c> parameter.</summary>
    internal const string SearchDescription =
        "Free-text search: matches rows where any text field of the returned model contains this value. " +
        "At least 2 characters. Case sensitivity follows the database collation. Combines with 'filter' " +
        "using AND. Omit or leave blank for no search; to match a non-text field exactly, use 'filter' instead.";

    /// <summary>OpenAPI description for the <c>fromDate</c> parameter.</summary>
    internal const string FromDateDescription =
        "Inclusive lower bound on when a record was last active — created or updated. Naming either " +
        "'fromDate' or 'toDate' replaces the default activity window entirely, rather than narrowing it. " +
        "Pass the earliest representable moment (0001-01-01T00:00:00Z) to ask for all history. With neither " +
        "bound supplied, records that carry audit timestamps are limited to the host's default recent-activity " +
        "window (3 months, unless the host configures a different DefaultActivityWindowMonths). Ignored for " +
        "records that carry no audit timestamps.";

    /// <summary>OpenAPI description for the <c>toDate</c> parameter.</summary>
    internal const string ToDateDescription =
        "Inclusive upper bound on the same last-active notion as 'fromDate'. Naming either bound replaces " +
        "the default activity window entirely, rather than narrowing it. Ignored for records that carry no " +
        "audit timestamps.";

    /// <summary>
    ///     Most filter conditions accepted per request. Each one costs reflection, parsing, and a SQL
    ///     predicate, so the work a single request can demand has to be bounded.
    /// </summary>
    internal const int MaxFilterCount = 20;

    /// <summary>
    ///     Shortest accepted search text. A single character ORs a full <c>LIKE '%…%'</c> scan across every
    ///     text column for almost no selectivity.
    /// </summary>
    internal const int MinSearchLength = 2;

    /// <summary>Property names per type, so a case-insensitive lookup cannot throw on case-only overloads.</summary>
    private static readonly ConcurrentDictionary<Type, HashSet<string>> PropertyNames = new();

    /// <summary>
    ///     Free-text search predicate templates, cached per <c>(TModel, TEntity)</c> pair so the Dynamic LINQ
    ///     parse of <see cref="ModelSearch.Clauses{TModel,TEntity}" /> runs once ever instead of once per
    ///     request (see performance finding P5). <see langword="null" /> caches a model with no searchable
    ///     field.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type Model, Type Entity), SearchTemplate?> SearchTemplates = new();

    /// <summary>
    ///     Recent-activity window predicate templates, cached per <c>(TEntity, shape)</c> pair — <see cref="WindowShape" />
    ///     picks which side(s) of the window are bound — so the reflection over <c>CreatedOn</c>/<c>UpdatedOn</c>
    ///     and the expression tree built from it run once ever instead of once per request. Only reached for a
    ///     <c>TEntity</c> that implements <see cref="IAuditedProperties" /> (R2/R6); the caller checks that first.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type Entity, WindowShape Shape), WindowTemplate> WindowTemplates = new();

    #endregion

    #region Methods

    /// <summary>
    ///     Validates the bound inputs of a generic list endpoint and compiles its filter conditions.
    /// </summary>
    /// <typeparam name="TEntity">Entity type the filters are applied to.</typeparam>
    /// <typeparam name="TModel">Projection model whose properties define what may be filtered and sorted.</typeparam>
    /// <param name="request">The endpoint's bound query-string parameters.</param>
    /// <param name="options">The host's configured page-size and activity-window defaults.</param>
    /// <param name="query">The validated inputs on success; otherwise <see langword="null" />.</param>
    /// <param name="error">A caller-facing reason on failure; otherwise <see langword="null" />.</param>
    /// <returns><see langword="true" /> when every input was valid; otherwise <see langword="false" />.</returns>
    internal static bool TryValidate<TEntity, TModel>(
        ListQueryRequest request,
        ListQueryOptions options,
        out ListQuery<TEntity>? query,
        out string? error)
        where TEntity : class
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        query = null;
        Expression<Func<TEntity, bool>>? combined = null;
        var search = request.Search;
        var orderBy = request.OrderBy;

        // R1: an impossible window is refused before anything else — including for a type the window will
        // turn out to ignore (R2/R6) — because a self-contradictory request is a caller mistake regardless.
        if (request.FromDate is not null && request.ToDate is not null && request.FromDate > request.ToDate)
        {
            error = $"fromDate '{request.FromDate:O}' is later than toDate '{request.ToDate:O}'.";
            return false;
        }

        if (request.Filter is { Length: > MaxFilterCount })
        {
            error = $"Too many filter conditions: {request.Filter.Length}. At most {MaxFilterCount} are " +
                    "accepted per request.";
            return false;
        }

        foreach (var filter in request.Filter ?? [])
        {
            if (!TryBuild<TEntity, TModel>(filter, out var expression, out error)) return false;
            combined = combined is null ? expression : combined.And(expression!);
        }

        // A blank search is simply absent — no condition, so the endpoint behaves exactly as it does without
        // the parameter. A non-blank one ANDs its whole OR-group onto the filters.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            if (text.Length < MinSearchLength)
            {
                error = $"Search text must be at least {MinSearchLength} characters.";
                return false;
            }

            var searchExpression = Search<TEntity, TModel>(text);
            combined = combined is null ? searchExpression : combined.And(searchExpression);
        }

        // R2/R6: the window applies only where TEntity carries audit timestamps; otherwise the bounds are
        // ignored (never refused). R3: the caller's own bounds replace the default outright; absent both, the
        // host's configured window applies unless switched off.
        var windowExpression = BuildWindowExpression<TEntity>(request, options);
        if (windowExpression is not null)
            combined = combined is null ? windowExpression : combined.And(windowExpression);

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
        var template = SearchTemplates.GetOrAdd(
            (typeof(TModel), typeof(TEntity)),
            static _ => BuildSearchTemplate<TEntity, TModel>());
        if (template is null) return _ => false;

        // A fresh box per call, not a mutation of the cached template's placeholder: concurrent requests must
        // never race over the same mutable value. Swapping the constant node is cheap tree work, not a parse.
        var box = new SearchBox { Value = search };
        var swapped = new SearchBoxSwap(template.Placeholder, box).Visit(template.Expression);
        return (Expression<Func<TEntity, bool>>)swapped;
    }

    /// <summary>
    ///     Parses <typeparamref name="TModel" />'s search clauses into a single predicate, once per
    ///     <c>(TModel, TEntity)</c> pair. The clause text always reads its comparison value through
    ///     <see cref="SearchBox.Value" /> on a placeholder instance rather than a literal, so the parsed
    ///     <see cref="ConstantExpression" /> node can be swapped for a new box on every request instead of
    ///     re-parsed — and so EF Core reads the value through a member access, which it binds as a query
    ///     parameter instead of inlining as a SQL literal.
    /// </summary>
    /// <typeparam name="TEntity">Entity type the search applies to.</typeparam>
    /// <typeparam name="TModel">Projection model whose text fields are searched.</typeparam>
    /// <returns>The parsed template, or <see langword="null" /> when the model has no searchable field.</returns>
    private static SearchTemplate? BuildSearchTemplate<TEntity, TModel>()
        where TEntity : class
        where TModel : class
    {
        var clauses = ModelSearch.Clauses<TModel, TEntity>();
        if (clauses.Length == 0) return null;

        var combined = string.Join(
            " || ",
            clauses.Select(c => $"({c.Replace("@0", "@0.Value", StringComparison.Ordinal)})"));

        var placeholder = new SearchBox();
        var expression = DynamicExpressionParser.ParseLambda<TEntity, bool>(
            ParsingConfig.Default, false, combined, placeholder);
        return new SearchTemplate(expression, placeholder);
    }

    /// <summary>
    ///     Resolves the effective recent-activity window (R3) for <typeparamref name="TEntity" /> and, where one
    ///     applies, builds its predicate. Returns <see langword="null" /> whenever no window predicate is
    ///     needed: <typeparamref name="TEntity" /> carries no audit timestamps (R2/R6), or neither bound was
    ///     named and the host's default window is switched off.
    /// </summary>
    /// <typeparam name="TEntity">Entity type the window applies to.</typeparam>
    /// <param name="request">The endpoint's bound query-string parameters.</param>
    /// <param name="options">The host's configured default activity-window width.</param>
    /// <returns>The window predicate, or <see langword="null" /> when none applies.</returns>
    private static Expression<Func<TEntity, bool>>? BuildWindowExpression<TEntity>(
        ListQueryRequest request,
        ListQueryOptions options)
        where TEntity : class
    {
        if (!typeof(IAuditedProperties).IsAssignableFrom(typeof(TEntity))) return null; // R2/R6

        DateTimeOffset? from;
        DateTimeOffset? to;
        if (request.FromDate is not null || request.ToDate is not null)
        {
            // The caller's own bounds replace the default entirely — open-ended on whichever side they left out.
            from = request.FromDate;
            to = request.ToDate;
        }
        else if (options.DefaultActivityWindowMonths > 0)
        {
            from = DateTimeOffset.UtcNow.AddMonths(-options.DefaultActivityWindowMonths);
            to = null;
        }
        else
        {
            return null;
        }

        // Which side(s) are bound decides the predicate's *shape*, not just its values — a template is cached
        // per (TEntity, shape) so the tree the caller-visible bound(s) sit in never carries a branch for a side
        // that is not present, and no "is this bound null" check ever has to reach the database.
        var shape = from is not null && to is not null
            ? WindowShape.Both
            : from is not null
                ? WindowShape.FromOnly
                : WindowShape.ToOnly;

        var template = WindowTemplates.GetOrAdd((typeof(TEntity), shape), static key => BuildWindowTemplate<TEntity>(key.Shape));

        // A fresh box per call, not a mutation of the cached template's placeholder: concurrent requests must
        // never race over the same mutable bounds. Swapping the constant node is cheap tree work, not a rebuild.
        // Only the side(s) the chosen shape's template actually references are guaranteed non-null here.
        var box = new WindowBox { From = from.GetValueOrDefault(), To = to.GetValueOrDefault() };
        var swapped = new WindowBoxSwap(template.Placeholder, box).Visit(template.Expression);
        return (Expression<Func<TEntity, bool>>)swapped;
    }

    /// <summary>
    ///     Builds the recent-activity window predicate template for <typeparamref name="TEntity" /> and a given
    ///     bound <paramref name="shape" />, once per <c>(TEntity, shape)</c> pair:
    ///     <c>inRange(CreatedOn) || (UpdatedOn != null &amp;&amp; inRange(UpdatedOn))</c> (R4), where
    ///     <c>inRange</c> compares only the side(s) <paramref name="shape" /> carries, reading the bound(s)
    ///     through a placeholder <see cref="WindowBox" /> member access rather than a literal — the same shape
    ///     <see cref="BuildSearchTemplate{TEntity,TModel}" /> uses for the search value — so EF Core binds them
    ///     as query parameters instead of inlining them, and a fresh box can be swapped in per request without
    ///     rebuilding the tree.
    /// </summary>
    /// <typeparam name="TEntity">Entity type to reflect <c>CreatedOn</c>/<c>UpdatedOn</c> off.</typeparam>
    /// <param name="shape">Which side(s) of the window are bound.</param>
    /// <returns>The template. <typeparamref name="TEntity" />'s <see cref="IAuditedProperties" /> membership is checked by the caller.</returns>
    private static WindowTemplate BuildWindowTemplate<TEntity>(WindowShape shape)
        where TEntity : class
    {
        // Reflected off TEntity itself, not IAuditedProperties: TEntity is not statically constrained to the
        // interface, and reading through the concrete property (rather than an interface cast) is what lets EF
        // Core translate the access — CreatedOn/UpdatedOn are guaranteed present by the interface check the
        // caller already made.
        var createdOnProperty = typeof(TEntity).GetProperty(nameof(IAuditedProperties.CreatedOn))!;
        var updatedOnProperty = typeof(TEntity).GetProperty(nameof(IAuditedProperties.UpdatedOn))!;

        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var placeholder = new WindowBox();
        var fromAccess = Expression.Property(Expression.Constant(placeholder), nameof(WindowBox.From));
        var toAccess = Expression.Property(Expression.Constant(placeholder), nameof(WindowBox.To));

        Expression InRange(Expression dateAccess)
        {
            // dateAccess is CreatedOn (DateTimeOffset) or UpdatedOn (DateTimeOffset?); the box's bounds are
            // always non-nullable, so any lifting needed is entirely on dateAccess's side.
            Expression Ge(Expression bound) =>
                dateAccess.Type == typeof(DateTimeOffset?)
                    ? Expression.GreaterThanOrEqual(dateAccess, Expression.Convert(bound, typeof(DateTimeOffset?)))
                    : Expression.GreaterThanOrEqual(dateAccess, bound);

            Expression Le(Expression bound) =>
                dateAccess.Type == typeof(DateTimeOffset?)
                    ? Expression.LessThanOrEqual(dateAccess, Expression.Convert(bound, typeof(DateTimeOffset?)))
                    : Expression.LessThanOrEqual(dateAccess, bound);

            return shape switch
            {
                WindowShape.FromOnly => Ge(fromAccess),
                WindowShape.ToOnly => Le(toAccess),
                _ => Expression.AndAlso(Ge(fromAccess), Le(toAccess)),
            };
        }

        var createdOnAccess = Expression.Property(parameter, createdOnProperty);
        Expression body = InRange(createdOnAccess);

        var updatedOnAccess = Expression.Property(parameter, updatedOnProperty);
        var updatedOnNotNull = Expression.NotEqual(updatedOnAccess, Expression.Constant(null, typeof(DateTimeOffset?)));
        body = Expression.OrElse(body, Expression.AndAlso(updatedOnNotNull, InRange(updatedOnAccess)));

        var lambda = Expression.Lambda(body, parameter);
        return new WindowTemplate(lambda, placeholder);
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

/// <summary>A free-text search predicate, parsed once, plus the placeholder its value is read through.</summary>
/// <param name="Expression">The parsed predicate; its comparisons read through <paramref name="Placeholder" />.</param>
/// <param name="Placeholder">The <see cref="SearchBox" /> instance the parse embedded as a constant.</param>
internal sealed record SearchTemplate(LambdaExpression Expression, SearchBox Placeholder);

/// <summary>
///     Mutable holder for a free-text search value. A cached predicate template reads the value through a
///     member access on an instance of this class rather than a literal, so the value can be swapped per
///     request without re-parsing, and so EF Core binds it as a query parameter instead of inlining it.
/// </summary>
internal sealed class SearchBox
{
    /// <summary>The search text the predicate compares against.</summary>
    public string Value { get; init; } = string.Empty;
}

/// <summary>Replaces every reference to one <see cref="SearchBox" /> in an expression tree with another.</summary>
/// <param name="from">The placeholder instance to replace.</param>
/// <param name="to">The instance to replace it with.</param>
internal sealed class SearchBoxSwap(SearchBox from, SearchBox to) : ExpressionVisitor
{
    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node) =>
        ReferenceEquals(node.Value, from) ? Expression.Constant(to) : node;
}

/// <summary>Which side(s) of a recent-activity window are bound — decides a window predicate template's shape.</summary>
internal enum WindowShape
{
    /// <summary>Only a lower bound; open-ended above.</summary>
    FromOnly,

    /// <summary>Only an upper bound; open-ended below.</summary>
    ToOnly,

    /// <summary>Both a lower and an upper bound.</summary>
    Both,
}

/// <summary>A recent-activity window predicate, built once per entity type, plus the placeholder its bounds are read through.</summary>
/// <param name="Expression">The predicate; its bound comparisons read through <paramref name="Placeholder" />.</param>
/// <param name="Placeholder">The <see cref="WindowBox" /> instance the tree embedded as a constant.</param>
internal sealed record WindowTemplate(LambdaExpression Expression, WindowBox Placeholder);

/// <summary>
///     Mutable holder for a recent-activity window's bounds. A cached predicate template reads the bounds
///     through a member access on an instance of this class rather than a literal, so a fresh pair of bounds can
///     be swapped in per request without rebuilding the tree, and so EF Core binds them as query parameters
///     instead of inlining them — the default window's lower bound is a distinct value on every request, and an
///     inlined literal would seed a fresh query plan each time.
/// </summary>
internal sealed class WindowBox
{
    /// <summary>
    ///     Inclusive lower bound. Only meaningful when the cached template's <see cref="WindowShape" /> is
    ///     <see cref="WindowShape.FromOnly" /> or <see cref="WindowShape.Both" /> — otherwise unreferenced.
    /// </summary>
    public DateTimeOffset From { get; init; }

    /// <summary>
    ///     Inclusive upper bound. Only meaningful when the cached template's <see cref="WindowShape" /> is
    ///     <see cref="WindowShape.ToOnly" /> or <see cref="WindowShape.Both" /> — otherwise unreferenced.
    /// </summary>
    public DateTimeOffset To { get; init; }
}

/// <summary>Replaces every reference to one <see cref="WindowBox" /> in an expression tree with another.</summary>
/// <param name="from">The placeholder instance to replace.</param>
/// <param name="to">The instance to replace it with.</param>
internal sealed class WindowBoxSwap(WindowBox from, WindowBox to) : ExpressionVisitor
{
    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node) =>
        ReferenceEquals(node.Value, from) ? Expression.Constant(to) : node;
}
