// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ListQueryRequest.cs
// Description: The paging, filtering, search and ordering parameters accepted by the generic list endpoints.

using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     The complete query-string contract of a generic list endpoint: which page, which conditions, what to
///     search for, and how to order.
/// </summary>
/// <remarks>
///     Every property is nullable so that an absent parameter is distinguishable from a supplied one and the
///     defaults live here rather than being restated at each call site. <see cref="PageNumberValue" /> and
///     <see cref="PageSizeValue" /> are the values an endpoint should actually page by — they apply the
///     defaults and the ceiling, so a caller cannot ask for an unbounded page.
/// </remarks>
public sealed record ListQueryRequest
{
    #region Fields

    /// <summary>Page size used when the caller does not ask for one.</summary>
    private const int DefaultPageSize = 20;

    /// <summary>Largest page a caller may request, whatever they ask for.</summary>
    private const int MaxPageSize = 100;

    #endregion

    #region Properties

    /// <summary>One-based page to return. Values below 1 are treated as the first page.</summary>
    [FromQuery(Name = "pageNumber")]
    [Description("One-based page number. Values below 1 are treated as the first page.")]
    public int? PageNumber { get; init; }

    /// <summary>Items per page. Defaults to 20 and is capped at 100.</summary>
    [FromQuery(Name = "pageSize")]
    [Description("Number of items per page. Values above 100 are clamped to 100.")]
    public int? PageSize { get; init; }

    /// <summary>Filter conditions, all of which must hold. Repeat the parameter to add conditions.</summary>
    [FromQuery(Name = "filter")]
    [Description(ListQuery.FilterDescription)]
    public ListFilter[]? Filter { get; init; }

    /// <summary>Free-text search across the returned model's text fields.</summary>
    [FromQuery(Name = "search")]
    [Description(ListQuery.SearchDescription)]
    public string? Search { get; init; }

    /// <summary>Field to order by, replacing the endpoint's default ordering.</summary>
    [FromQuery(Name = "orderBy")]
    [Description(ListQuery.OrderByDescription)]
    public string? OrderBy { get; init; }

    /// <summary>Whether <see cref="OrderBy" /> sorts descending.</summary>
    [FromQuery(Name = "desc")]
    [Description("Sort descending instead of ascending. Ignored without orderBy.")]
    public bool? Desc { get; init; }

    /// <summary>The page number to actually query, with the below-1 case folded to the first page.</summary>
    internal int PageNumberValue => PageNumber is null or < 1 ? 1 : PageNumber.Value;

    /// <summary>The page size to actually query, with the default applied and the ceiling enforced.</summary>
    internal int PageSizeValue =>
        PageSize is null or < 1 ? DefaultPageSize : Math.Min(PageSize.Value, MaxPageSize);

    /// <summary>Whether ordering is descending, defaulting to ascending.</summary>
    internal bool IsDescending => Desc ?? false;

    #endregion
}
