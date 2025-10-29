// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: PagedResult.cs
// Description: Simple paged result wrapper used by SlimBus minimal API endpoints to return paged responses.

using X.PagedList;

namespace DKNet.AspCore.SlimBus;

/// <summary>
///     Represents a paged result returned from a query including paging metadata and the items for the current page.
/// </summary>
/// <typeparam name="TResult">Type of the items contained in the page.</typeparam>
public sealed record PagedResult<TResult>
{
    #region Constructors

    /// <summary>
    ///     Creates an empty paged result.
    /// </summary>
    public PagedResult() => Items = new List<TResult>();

    /// <summary>
    ///     Creates a paged result from an <see cref="IPagedList{T}" /> produced by X.PagedList.
    /// </summary>
    /// <param name="list">The source paged list.</param>
    public PagedResult(IPagedList<TResult> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        PageNumber = list.PageNumber;
        PageSize = list.PageSize;
        PageCount = list.PageCount;
        TotalItemCount = list.TotalItemCount;
        Items = list.ToList();
    }

    #endregion

    #region Properties

    /// <summary>
    ///     The items contained in the current page.
    /// </summary>
    public IList<TResult> Items { get; init; } = new List<TResult>();

    /// <summary>
    ///     Total number of pages available.
    /// </summary>
    public int PageCount { get; init; }

    /// <summary>
    ///     The current page number (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    ///     The size of a single page (number of items per page).
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    ///     Total number of items across all pages.
    /// </summary>
    public int TotalItemCount { get; init; }

    #endregion
}