using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Extensions.Extensions;

internal class EfCorePageAsyncEnumerator<T>(IQueryable<T> query, int pageSize) : IAsyncEnumerable<T>
{
    private int _currentPage;
    private bool _hasMorePages = true;

    private async Task<IReadOnlyList<T>> GetNextPageAsync(CancellationToken cancellationToken = default)
    {
        if (!_hasMorePages) return [];

        var page = await query
            .Skip(_currentPage * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _currentPage++;
        _hasMorePages = page.Count == pageSize;

        return page;
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        do
        {
            var page = await GetNextPageAsync(cancellationToken);
            if (page.Count == 0)
            {
                _hasMorePages = false;
                yield break;
            }

            foreach (var item in page)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                yield return item;
            }
        } while (_hasMorePages);
    }
}

public static class PageAsyncEnumeratorExtensions
{
    public static IAsyncEnumerable<T> ToPageEnumerable<T>(this IQueryable<T> query, int pageSize = 100)
        => new EfCorePageAsyncEnumerator<T>(query, pageSize);
}