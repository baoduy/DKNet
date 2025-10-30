using DKNet.EfCore.Extensions.Extensions;
using LinqKit;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global

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
    void DeleteRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;

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
        where TEntity : class where TModel : class;

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
/// <param name="dbContext">The database context instance.</param>
/// <param name="mappers">Collection of mappers for entity-to-model projections.</param>
public class RepositorySpec<TDbContext>(TDbContext dbContext, IEnumerable<IMapper> mappers)
    : IRepositorySpec where TDbContext : DbContext
{
    #region Fields

    private readonly IMapper? _mapper = mappers.FirstOrDefault();

    #endregion

    #region Methods

    /// <summary>
    ///     Adds a new entity to the repository asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual async ValueTask AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class
        => await dbContext.AddAsync(entity, cancellationToken);

    /// <summary>
    ///     Adds a collection of entities to the repository asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entities">The entities to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual async ValueTask AddRangeAsync<TEntity>(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class
        => await dbContext.AddRangeAsync(entities, cancellationToken);

    /// <summary>
    ///     Begins a new database transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns a database context transaction.</returns>
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => dbContext.Database.BeginTransactionAsync(cancellationToken);

    /// <summary>
    ///     Marks an entity for deletion.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to delete.</param>
    public virtual void Delete<TEntity>(TEntity entity)
        where TEntity : class
        => dbContext.Set<TEntity>().Remove(entity);

    /// <summary>
    ///     Marks a collection of entities for deletion.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entities">The entities to delete.</param>
    public virtual void DeleteRange<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class
        => dbContext.Set<TEntity>().RemoveRange(entities);

    /// <summary>
    ///     Gets the entity entry for the specified entity, providing access to change tracking information.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <returns>The entity entry for the specified entity.</returns>
    public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class
        => dbContext.Entry(entity);

    /// <summary>
    ///     Queries entities of type <typeparamref name="TEntity" /> using the provided specification.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query.</typeparam>
    /// <param name="spec">The specification to apply to the query.</param>
    /// <returns>A queryable collection of entities matching the specification.</returns>
    public IQueryable<TEntity> Query<TEntity>(ISpecification<TEntity> spec) where TEntity : class =>
        dbContext.Set<TEntity>().AsExpandable().ApplySpecs(spec);

    /// <summary>
    ///     Queries entities of type <typeparamref name="TEntity" /> using the provided specification and projects them to
    ///     <typeparamref name="TModel" />.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query.</typeparam>
    /// <typeparam name="TModel">The model type to project to.</typeparam>
    /// <param name="spec">The specification to apply to the query.</param>
    /// <returns>A queryable collection of projected models.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no mapper instance is available for projection.</exception>
    public IQueryable<TModel> Query<TEntity, TModel>(ISpecification<TEntity> spec)
        where TEntity : class
        where TModel : class
    {
        if (_mapper is null)
            throw new InvalidOperationException($"No {nameof(IMapper)} instance available for mapping.");

        return Query(spec).AsNoTracking().ProjectToType<TModel>(_mapper.Config);
    }

    /// <summary>
    ///     Saves all changes made in this repository to the underlying database asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    ///     A task representing the asynchronous operation that returns the number of state entries written to the
    ///     database.
    /// </returns>
    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.AddNewEntitiesFromNavigations(cancellationToken);
        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     Updates an existing entity asynchronously and adds any new entities found in navigation properties.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns the number of new entities added from navigations.</returns>
    public virtual async Task<int> UpdateAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        dbContext.Entry(entity).State = EntityState.Modified;

        var newEntities = dbContext.GetNewEntitiesFromNavigations(dbContext.Entry(entity)).ToList();
        await dbContext.AddRangeAsync(newEntities, cancellationToken);
        return newEntities.Count;
    }

    /// <summary>
    ///     Updates a collection of entities asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entities">The entities to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateRangeAsync<TEntity>(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        foreach (var entity in entities) await UpdateAsync(entity, cancellationToken);
    }

    #endregion
}