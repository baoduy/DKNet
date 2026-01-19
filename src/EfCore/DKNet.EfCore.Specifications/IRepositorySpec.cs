using System.Linq.Expressions;
using DKNet.EfCore.Extensions.Extensions;
using DKNet.EfCore.Specifications.Extensions;
using LinqKit;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Defines a repository contract for querying and persisting entities using specifications.
/// </summary>
public interface IRepositorySpec
{
    #region Methods

    /// <summary>
    ///     Adds a new entity to the repository asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    ///     Adds a collection of entities to the repository asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entities">The entities to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask AddRangeAsync<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    ///     Begins a new database transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns a database context transaction.</returns>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks an entity for deletion.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to delete.</param>
    void Delete<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    ///     Marks a collection of entities for deletion.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entities">The entities to delete.</param>
    [Obsolete(
        "Using the BulkDeleteRangeAsync <see cref=\"BulkDeleteRangeAsync{TEntity}(IEnumerable{TEntity}, CancellationToken)\" />")]
    void DeleteRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;

    /// <summary>
    ///     The bulk deletes a collection of entities asynchronously.
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns></returns>
    Task<int> BulkDeleteAsync<TEntity>(Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    ///     Gets the entity entry for the specified entity, providing access to change tracking information.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <returns>The entity entry for the specified entity.</returns>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    ///     Queries entities of type <typeparamref name="TEntity" /> using the provided specification.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query.</typeparam>
    /// <param name="spec">The specification to apply to the query.</param>
    /// <returns>A queryable collection of entities matching the specification.</returns>
    IQueryable<TEntity> Query<TEntity>(ISpecification<TEntity> spec) where TEntity : class;

    /// <summary>
    ///     Queries entities of type <typeparamref name="TEntity" /> using the provided specification and projects them to
    ///     <typeparamref name="TModel" />.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query.</typeparam>
    /// <typeparam name="TModel">The model type to project to.</typeparam>
    /// <param name="spec">The specification to apply to the query.</param>
    /// <returns>A queryable collection of projected models.</returns>
    IQueryable<TModel> Query<TEntity, TModel>(ISpecification<TEntity> spec)
        where TEntity : class
        where TModel : class;

    /// <summary>
    ///     Saves all changes made in this repository to the underlying database asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    ///     A task representing the asynchronous operation that returns the number of state entries written to the
    ///     database.
    /// </returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing entity asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns the number of new entities added from navigations.</returns>
    Task<int> UpdateAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    ///     Updates a collection of entities asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entities">The entities to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateRangeAsync<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        where TEntity : class;

    #endregion
}

/// <summary>
///     Implementation of <see cref="IRepositorySpec" /> that provides repository operations using specifications and
///     Entity Framework Core.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public sealed class RepositorySpec<TDbContext> : IRepositorySpec where TDbContext : DbContext
{
    #region Fields

    private readonly TDbContext _dbContext;

    private readonly IMapper? _mapper;
    private readonly IServiceProvider? _provider;

    #endregion

    #region Constructors

    internal RepositorySpec(TDbContext dbContext, IMapper? mapper = null)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <summary>
    ///     The constructor for the repository specification.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="provider"></param>
    public RepositorySpec(TDbContext dbContext,
        IServiceProvider? provider = null)
    {
        _dbContext = dbContext;
        _provider = provider;
        _mapper = provider?.GetService<IMapper>();
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async ValueTask AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class
        => await _dbContext.AddAsync(entity, cancellationToken);

    /// <inheritdoc />
    public async ValueTask AddRangeAsync<TEntity>(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class
        => await _dbContext.AddRangeAsync(entities, cancellationToken);

    /// <inheritdoc />
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _dbContext.Database.BeginTransactionAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> BulkDeleteAsync<TEntity>(Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) where TEntity : class
        => _dbContext.Set<TEntity>().Where(predicate).ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public void Delete<TEntity>(TEntity entity)
        where TEntity : class
        => _dbContext.Set<TEntity>().Remove(entity);

    /// <inheritdoc />
    public void DeleteRange<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class
        => _dbContext.Set<TEntity>().RemoveRange(entities);


    /// <inheritdoc />
    public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class
        => _dbContext.Entry(entity);

    /// <inheritdoc />
    public IQueryable<TEntity> Query<TEntity>(ISpecification<TEntity> spec) where TEntity : class =>
        _dbContext.Set<TEntity>().AsExpandable().ApplySpecs(spec);

    /// <inheritdoc />
    public IQueryable<TModel> Query<TEntity, TModel>(ISpecification<TEntity> spec)
        where TEntity : class
        where TModel : class
    {
        if (_mapper is null)
            throw new InvalidOperationException($"No {nameof(IMapper)} instance available for mapping.");

        return Query(spec).AsNoTracking().ProjectToType<TModel>(_mapper.Config);
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.AddNewEntitiesFromNavigations(cancellationToken);
        var handler = _provider?.GetKeyedService<IEfCoreExceptionHandler>(_dbContext.GetType().FullName);
        return await _dbContext.SaveChangesWithConcurrencyHandlingAsync(handler, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> UpdateAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        _dbContext.Entry(entity).State = EntityState.Modified;

        var newEntities = _dbContext.GetNewEntitiesFromNavigations(_dbContext.Entry(entity)).ToList();
        await _dbContext.AddRangeAsync(newEntities, cancellationToken);
        return newEntities.Count;
    }

    /// <inheritdoc />
    public async Task UpdateRangeAsync<TEntity>(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        foreach (var entity in entities) await UpdateAsync(entity, cancellationToken);
    }

    #endregion
}