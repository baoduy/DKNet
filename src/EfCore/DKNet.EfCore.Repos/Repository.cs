// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: Repository.cs
// Description: Read/write repository implementation that composes write functionality and exposes read helpers.

namespace DKNet.EfCore.Repos;

/// <summary>
///     Default repository implementation that provides read helpers on top of <see cref="WriteRepository{TEntity}" />.
/// </summary>
/// <typeparam name="TEntity">Entity CLR type.</typeparam>
public class Repository<TEntity> : WriteRepository<TEntity>, IRepository<TEntity>
    where TEntity : class
{
    #region Fields

    private readonly DbContext _dbContext;
    private readonly IMapper? _mapper;

    #endregion

    #region Constructors

    /// <summary>
    ///     Creates a new repository instance.
    /// </summary>
    /// <param name="dbContext">EF Core DbContext used by the repository.</param>
    /// <param name="mappers">Optional mappers collection; the first mapper is used when mapping is required.</param>
    public Repository(DbContext dbContext, IEnumerable<IMapper>? mappers = null)
        : base(dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mapper = mappers?.FirstOrDefault();
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Counts entities matching the provided <paramref name="filter" /> asynchronously.
    /// </summary>
    /// <param name="filter">Predicate to filter entities.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of matching entities.</returns>
    public Task<int> CountAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        Query(filter).CountAsync(cancellationToken);

    /// <summary>
    ///     Determines whether any entity exists that satisfies the <paramref name="filter" />.
    /// </summary>
    /// <param name="filter">Predicate to filter entities.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when at least one entity matches; otherwise <c>false</c>.</returns>
    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
        => Query(filter).AnyAsync(cancellationToken);

    /// <summary>
    ///     Finds an entity by its primary key value asynchronously.
    /// </summary>
    /// <param name="keyValue">Single primary key value (or composite key element when single).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The found entity or <c>null</c> when not found.</returns>
    public ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default)
        => FindAsync(new[] { keyValue }, cancellationToken);

    /// <summary>
    ///     Finds an entity by its primary key values asynchronously.
    /// </summary>
    /// <param name="keyValues">Array of key values for composite keys.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The found entity or <c>null</c> when not found.</returns>
    public async ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default)
        => await _dbContext.FindAsync<TEntity>(keyValues, cancellationToken);

    /// <summary>
    ///     Finds the first entity that satisfies the provided <paramref name="filter" />, or <c>null</c> if none match.
    /// </summary>
    /// <param name="filter">Predicate to filter entities.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matched entity or <c>null</c>.</returns>
    public Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        Query(filter).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    ///     Projects entities that satisfy the specified <paramref name="filter" /> to the target model
    ///     <typeparamref name="TModel" />.
    ///     Requires an <see cref="IMapper" /> to be registered.
    /// </summary>
    /// <typeparam name="TModel">The model type to project to.</typeparam>
    /// <param name="filter">Predicate to filter entities.</param>
    /// <returns>An <see cref="IQueryable{TModel}" /> representing the projected query.</returns>
    public IQueryable<TModel> Query<TModel>(Expression<Func<TEntity, bool>> filter)
        where TModel : class
    {
        if (_mapper is null) throw new InvalidOperationException("IMapper is not registered.");

        var query = Query(filter);
        return query.ProjectToType<TModel>(_mapper.Config);
    }

    /// <summary>
    ///     Returns a queryable for the entity type. Implementations may override to apply default includes or filters.
    /// </summary>
    public virtual IQueryable<TEntity> Query() => _dbContext.Set<TEntity>();

    /// <summary>
    ///     Returns a queryable filtered by the provided <paramref name="filter" />.
    /// </summary>
    /// <param name="filter">Predicate to filter entities.</param>
    public virtual IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> filter) => Query().Where(filter);

    #endregion
}