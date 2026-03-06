using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.EF;

namespace DKNet.EfCore.Specifications.Extensions;

/// <summary>
///     Provides extension methods for applying specifications to repositories and queries.
/// </summary>
public static class SpecRepoExtensions
{
    /// <param name="repo">The repository</param>
    extension(IRepositorySpec repo)
    {
        /// <summary>
        ///     Asynchronously determines whether any elements match the specification.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>
        ///     A task representing the asynchronous operation that returns true if any elements match the specification;
        ///     otherwise, false
        /// </returns>
        public Task<bool> AnyAsync<TEntity>(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default)
            where TEntity : class =>
            repo.Query(specification).AnyAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns the number of elements matching the specification.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>
        ///     A task representing the asynchronous operation that returns the number of elements that match the
        ///     specification
        /// </returns>
        public Task<int> CountAsync<TEntity>(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default)
            where TEntity : class =>
            repo.Query(specification).CountAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns the first entity matching the specification.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation that returns the first entity</returns>
        /// <exception cref="InvalidOperationException">Thrown when no entity matching the specification is found</exception>
        public Task<TEntity> FirstAsync<TEntity>(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default)
            where TEntity : class =>
            repo.Query(specification).FirstAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns the first entity matching the specification, or null if none found.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation that returns the first entity or null</returns>
        public Task<TEntity?> FirstOrDefaultAsync<TEntity>(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default)
            where TEntity : class =>
            repo.Query(specification).FirstOrDefaultAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns the first projected model matching the specification, or null if none found.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <typeparam name="TModel">Type of the model to project to</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation that returns the first projected model or null</returns>
        public Task<TModel?> FirstOrDefaultAsync<TEntity, TModel>(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default)
            where TEntity : class
            where TModel : class
            => repo.Query<TEntity, TModel>(specification).FirstOrDefaultAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns a list of entities matching the specification.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation that returns a list of entities</returns>
        public async Task<IList<TEntity>> ToListAsync<TEntity>(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default)
            where TEntity : class =>
            await repo.Query(specification).ToListAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns a list of projected models matching the specification.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <typeparam name="TModel">Type of the model to project to</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation that returns a list of projected models</returns>
        public async Task<IList<TModel>> ToListAsync<TEntity, TModel>(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default)
            where TEntity : class
            where TModel : class
            => await repo.Query<TEntity, TModel>(specification).ToListAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns a paged list of entities matching the specification.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <returns>A task representing the asynchronous operation that returns a paged list of entities</returns>
        public Task<IPagedList<TEntity>> ToPagedListAsync<TEntity>(ISpecification<TEntity> specification,
            int pageNumber,
            int pageSize) where TEntity : class => repo.Query(specification).ToPagedListAsync(pageNumber, pageSize);

        /// <summary>
        ///     Asynchronously returns a paged list of projected models matching the specification.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <typeparam name="TModel">Type of the model to project to</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <returns>A task representing the asynchronous operation that returns a paged list of projected models</returns>
        public Task<IPagedList<TModel>> ToPagedListAsync<TEntity, TModel>(ISpecification<TEntity> specification,
            int pageNumber,
            int pageSize)
            where TEntity : class
            where TModel : class
            => repo.Query<TEntity, TModel>(specification).ToPagedListAsync(pageNumber, pageSize);

        /// <summary>
        ///     Returns an async enumerable of entities matching the specification, paged.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <returns>An async enumerable of entities</returns>
        public IAsyncEnumerable<TEntity> ToPageEnumerable<TEntity>(ISpecification<TEntity> specification)
            where TEntity : class
        {
            specification.EnsureSpecHasOrdering();
            var query = (IOrderedQueryable<TEntity>)repo.Query(specification);
            return query.ToPageEnumerable();
        }

        /// <summary>
        ///     Returns an async enumerable of projected models matching the specification, paged.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity</typeparam>
        /// <typeparam name="TModel">Type of the model to project to</typeparam>
        /// <param name="specification">The specification to apply</param>
        /// <returns>An async enumerable of projected models</returns>
        public IAsyncEnumerable<TModel> ToPageEnumerable<TEntity, TModel>(ISpecification<TEntity> specification)
            where TEntity : class
            where TModel : class
        {
            specification.EnsureSpecHasOrdering();
            var query = repo.Query<TEntity, TModel>(specification);
            return query.ToPageEnumerable();
        }

        /// <summary>
        ///     Asynchronously returns a keyset-paginated list of entities matching the specification,
        ///     starting after the provided single-key cursor value.
        ///     Keyset pagination is significantly more efficient than offset pagination for large datasets
        ///     because it uses an index seek instead of a full table scan.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity.</typeparam>
        /// <typeparam name="TKey">Type of the keyset cursor key.</typeparam>
        /// <param name="specification">The specification that defines filtering and ordering.</param>
        /// <param name="keySelector">Expression selecting the key column used as the cursor.</param>
        /// <param name="cursor">The last seen cursor value; the result will contain rows after this value.</param>
        /// <param name="pageSize">The maximum number of rows to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        ///     A task that returns an <see cref="IList{TEntity}" /> containing up to <paramref name="pageSize" /> rows
        ///     that come after <paramref name="cursor" /> in sort order.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageSize" /> is less than or equal to zero.</exception>
        public async Task<IList<TEntity>> ToKeysetPageAsync<TEntity, TKey>(
            ISpecification<TEntity> specification,
            Expression<Func<TEntity, TKey>> keySelector,
            TKey cursor,
            int pageSize,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (pageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be greater than zero.");

            return await repo.Query(specification)
                .AfterKeyset(keySelector, cursor)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        ///     Asynchronously returns a keyset-paginated list of entities matching the specification,
        ///     starting after the provided composite two-key cursor value.
        ///     Keyset pagination is significantly more efficient than offset pagination for large datasets
        ///     because it uses an index seek instead of a full table scan.
        ///     The generated SQL is equivalent to the row-value comparison
        ///     <c>(key1, key2) &gt; (cursor1, cursor2)</c>.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity.</typeparam>
        /// <typeparam name="TKey1">Type of the primary key column.</typeparam>
        /// <typeparam name="TKey2">Type of the secondary (tie-break) key column.</typeparam>
        /// <param name="specification">The specification that defines filtering and ordering.</param>
        /// <param name="key1Selector">Expression selecting the primary key column.</param>
        /// <param name="key2Selector">Expression selecting the secondary key column.</param>
        /// <param name="cursor1">The primary cursor value from the last seen row.</param>
        /// <param name="cursor2">The secondary cursor value from the last seen row.</param>
        /// <param name="pageSize">The maximum number of rows to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        ///     A task that returns an <see cref="IList{TEntity}" /> containing up to <paramref name="pageSize" /> rows
        ///     that come after the composite cursor in sort order.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageSize" /> is less than or equal to zero.</exception>
        public async Task<IList<TEntity>> ToKeysetPageAsync<TEntity, TKey1, TKey2>(
            ISpecification<TEntity> specification,
            Expression<Func<TEntity, TKey1>> key1Selector,
            Expression<Func<TEntity, TKey2>> key2Selector,
            TKey1 cursor1,
            TKey2 cursor2,
            int pageSize,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (pageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be greater than zero.");

            return await repo.Query(specification)
                .AfterKeyset(key1Selector, key2Selector, cursor1, cursor2)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }
    }
}