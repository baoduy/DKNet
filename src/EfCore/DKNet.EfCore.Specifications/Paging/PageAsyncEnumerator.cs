using Microsoft.EntityFrameworkCore;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: PageAsyncEnumerator.cs
// Description: Helper that enumerates an IQueryable in pages asynchronously to avoid loading entire result sets.

namespace DKNet.EfCore.Specifications.Paging;

/// <summary>
///     Asynchronously enumerates an <see cref="IQueryable{T}" /> in pages of a fixed size.
///     Useful to stream large query results without materializing the full result set in memory.
/// </summary>
/// <typeparam name="TEntity">Element type of the query.</typeparam>
internal sealed class EfCorePageAsyncEnumerator<TEntity> : IAsyncEnumerable<TEntity>
{
    #region Fields

    private readonly int _pageSize;

    private readonly IQueryable<TEntity> _query;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of <see cref="EfCorePageAsyncEnumerator{T}" />.
    /// </summary>
    /// <param name="query">The query to page through. Must not be null.</param>
    /// <param name="pageSize">The page size to use; must be greater than zero.</param>
    public EfCorePageAsyncEnumerator(IQueryable<TEntity> query, int pageSize)
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
    ///     Each call starts from the first row, so the enumerable can be enumerated more than once.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the enumeration.</param>
    /// <returns>An async enumerator that streams items.</returns>
    public async IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var currentPage = 0;

        while (true)
        {
            var page = await _query
                .Skip(currentPage * _pageSize)
                .Take(_pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            currentPage++;
            var hasMorePages = page.Count == _pageSize;

            foreach (var item in page)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return item;
            }

            if (!hasMorePages) yield break;
        }
    }

    #endregion
}

/// <summary>
///     Extension methods for paging an IQueryable as an IAsyncEnumerable.
/// </summary>
internal static class PageAsyncEnumeratorExtensions
{
    #region Fields

    /// <summary>
    ///     Default number of rows fetched per database round-trip when no explicit page size is supplied.
    /// </summary>
    internal const int DefaultPageSize = 100;

    #endregion

    #region Methods

    public static IAsyncEnumerable<TEntity> ToPageEnumerable<TEntity>(
        this IQueryable<TEntity> query, int pageSize = DefaultPageSize) where TEntity : class
        => new EfCorePageAsyncEnumerator<TEntity>(query, pageSize);

    #endregion
}