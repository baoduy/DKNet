// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ReadRepository.cs
// Description: Read-only repository implementation providing common query helpers over EF Core DbContext.

namespace DKNet.EfCore.Repos;

/// <summary>
///     Read-only repository that exposes common query operations for <typeparamref name="TEntity" />.
///     The repository operates in read-only mode and uses the provided <see cref="DbContext" /> for queries.
/// </summary>
/// <typeparam name="TEntity">The entity CLR type.</typeparam>
public class ReadRepository<TEntity>(DbContext dbContext, IEnumerable<IMapper>? mappers = null)
    : IReadRepository<TEntity>
    where TEntity : class
{
    #region Fields

    private readonly IMapper? _mapper = mappers?.FirstOrDefault();

    #endregion

    #region Methods

    /// <inheritdoc />
    public Task<int> CountAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        Query(filter).CountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        Query(filter).AnyAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default) =>
        FindAsync([keyValue], cancellationToken);

    /// <inheritdoc />
    public async ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default) =>
        await dbContext.FindAsync<TEntity>(keyValues, cancellationToken);

    /// <inheritdoc />
    public Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        Query(filter).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public virtual IQueryable<TEntity> Query() => dbContext.Set<TEntity>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> filter) => Query().Where(filter);

    /// <inheritdoc />
    public IQueryable<TModel> Query<TModel>(Expression<Func<TEntity, bool>> filter)
        where TModel : class
    {
        if (_mapper is null) throw new InvalidOperationException("IMapper is not registered.");

        var query = Query(filter);
        return query.ProjectToType<TModel>(_mapper.Config);
    }

    #endregion
}