using System.Linq.Expressions;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Defines a specification that targets an entity type (<typeparamref name="TEntity" />) and is intended to produce
///     or describe a related model type (<typeparamref name="TModel" />). This extends
///     <see cref="ISpecification{TEntity}" /> to convey that the specification will be used in a projection scenario
///     (e.g. mapping via Mapster / AutoMapper).
/// </summary>
/// <typeparam name="TEntity">The EF Core entity type being queried.</typeparam>
/// <typeparam name="TModel">The destination model / DTO type the entity will be projected to.</typeparam>
public interface IModelSpecification<TEntity, TModel> : ISpecification<TEntity>
    where TEntity : class
    where TModel : class;

/// <summary>
///     Base implementation for a specification that is associated with projecting an entity
///     (<typeparamref name="TEntity" />) to a model (<typeparamref name="TModel" />). It adds no additional behavior
///     beyond <see cref="Specification{TEntity}" /> but serves as an extension point for future projection concerns
///     (e.g. a selector expression) without affecting simpler entity-only specifications.
/// </summary>
/// <typeparam name="TEntity">The EF Core entity type being queried.</typeparam>
/// <typeparam name="TModel">The destination model / DTO type the entity will be projected to.</typeparam>
public class ModelSpecification<TEntity, TModel> : Specification<TEntity>, IModelSpecification<TEntity, TModel>
    where TEntity : class
    where TModel : class
{
    #region Constructors

    /// <summary>
    ///     Initializes a new model specification by copying state from an existing <see cref="ISpecification{TEntity}" />.
    /// </summary>
    /// <param name="copyFrom">The source specification whose filter / ordering state will be cloned.</param>
    protected ModelSpecification(ISpecification<TEntity> copyFrom) : base(copyFrom)
    {
    }

    /// <summary>
    ///     Initializes a new, empty model specification (no filter, include, or ordering applied initially).
    /// </summary>
    protected ModelSpecification()
    {
    }

    /// <summary>
    ///     Initializes a new model specification with an initial filter predicate.
    /// </summary>
    /// <param name="query">The predicate used to filter entities.</param>
    protected ModelSpecification(Expression<Func<TEntity, bool>> query) : base(query)
    {
    }

    #endregion
}