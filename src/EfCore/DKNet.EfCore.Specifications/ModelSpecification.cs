namespace DKNet.EfCore.Specifications;

/// <summary>
///     Defines a specification that targets an entity type (<typeparamref name="TEntity"/>) and is intended to produce
///     or describe a related model type (<typeparamref name="TModel"/>). This extends <see cref="ISpecification{TEntity}"/>
///     with the semantic meaning that the specification is used in a context where an entity is projected to a model.
///     Implementations can add projection / selection members as needed (e.g. a selector expression) while still
///     benefiting from the filtering and ordering capabilities of the base specification contract.
/// </summary>
/// <typeparam name="TEntity">The underlying EF Core entity type the specification is built against.</typeparam>
/// <typeparam name="TModel">The model type that the entity is mapped or projected to when executing the specification.</typeparam>
public interface IModelSpecification<TEntity, TModel> : ISpecification<TEntity>
    where TEntity : class
    where TModel : class;

/// <summary>
///     Base implementation for a specification that is conceptually associated with projecting an entity
///     (<typeparamref name="TEntity"/>) to a model (<typeparamref name="TModel"/>). It currently behaves the same as
///     <see cref="Specification{TEntity}"/> and serves as an extension point for adding model projection concerns
///     (such as a selector expression) in derived classes without polluting the simpler entity specification type.
/// </summary>
/// <typeparam name="TEntity">The underlying EF Core entity type the specification operates on.</typeparam>
/// <typeparam name="TModel">The model type produced or described by the specification.</typeparam>
public class ModelSpecification<TEntity, TModel> : Specification<TEntity>, IModelSpecification<TEntity, TModel>
    where TEntity : class
    where TModel : class;
