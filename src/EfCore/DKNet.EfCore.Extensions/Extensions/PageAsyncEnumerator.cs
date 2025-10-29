namespace DKNet.EfCore.Extensions.Extensions;

internal class EfCorePageAsyncEnumerator<T>(IQueryable<T> query, int pageSize) : IAsyncEnumerable<T>
{
    #region Fields

    private int _currentPage;
    private bool _hasMorePages = true;

    #endregion

    #region Methods

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        do
        {
            var page = await this.GetNextPageAsync(cancellationToken);
            if (page.Count == 0)
            {
                this._hasMorePages = false;
                yield break;
            }

            foreach (var item in page)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                yield return item;
            }
        } while (this._hasMorePages);
    }

    private async Task<IReadOnlyList<T>> GetNextPageAsync(CancellationToken cancellationToken = default)
    {
        if (!this._hasMorePages)
        {
            return [];
        }

        var page = await query
            .Skip(this._currentPage * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        this._currentPage++;
        this._hasMorePages = page.Count == pageSize;

        return page;
    }

    #endregion
}

public static class PageAsyncEnumeratorExtensions
{
    #region Methods

    public static IAsyncEnumerable<T> ToPageEnumerable<T>(this IQueryable<T> query, int pageSize = 100) =>
        new EfCorePageAsyncEnumerator<T>(query, pageSize);

    #endregion
}