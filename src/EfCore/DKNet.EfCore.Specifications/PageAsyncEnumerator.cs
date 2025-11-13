using Microsoft.EntityFrameworkCore;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: PageAsyncEnumerator.cs
// Description: Helper that enumerates an IQueryable in pages asynchronously to avoid loading entire result sets.

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Asynchronously enumerates an <see cref="IQueryable{T}" /> in pages of a fixed size.
///     Useful to stream large query results without materializing the full result set in memory.
/// </summary>
/// <typeparam name="TEntity">Element type of the query.</typeparam>
internal sealed class EfCorePageAsyncEnumerator<TEntity> : IAsyncEnumerable<TEntity>
{
    #region Fields

    private readonly int _pageSize;

    private readonly IOrderedQueryable<TEntity> _query;
    private int _currentPage;
    private bool _hasMorePages = true;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of <see cref="EfCorePageAsyncEnumerator{T}" />.
    /// </summary>
    /// <param name="query">The query to page through. Must not be null.</param>
    /// <param name="pageSize">The page size to use; must be greater than zero.</param>
    public EfCorePageAsyncEnumerator(IOrderedQueryable<TEntity> query, int pageSize)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be greater than zero.");
        _pageSize = pageSize;
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Asynchronously enumerates the query, yielding items page-by-page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the enumeration.</param>
    /// <returns>An async enumerator that streams items.</returns>
    public async IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var page = await GetNextPageAsync(cancellationToken).ConfigureAwait(false);
            if (page.Count == 0) yield break;

            foreach (var item in page)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return item;
            }

            if (!_hasMorePages) yield break;
        }
    }

    /// <summary>
    ///     Retrieves the next page of results from the underlying query.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the async DB call.</param>
    /// <returns>A read-only list representing the next page (maybe empty).</returns>
    private async Task<IReadOnlyList<TEntity>> GetNextPageAsync(CancellationToken cancellationToken = default)
    {
        if (!_hasMorePages) return [];

        var page = await _query
            .Skip(_currentPage * _pageSize)
            .Take(_pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _currentPage++;
        _hasMorePages = page.Count == _pageSize;

        return page;
    }

    #endregion
}

/// <summary>
///     Extension methods for paging an IQueryable as an IAsyncEnumerable.
/// </summary>
public static class PageAsyncEnumeratorExtensions
{
    #region Methods

    public static IAsyncEnumerable<TEntity> ToPageEnumerable<TEntity>(
        this IOrderedQueryable<TEntity> query, int pageSize = 100) where TEntity : class
        => new EfCorePageAsyncEnumerator<TEntity>(query, pageSize);

    #endregion
}