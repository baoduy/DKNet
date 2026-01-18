using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.Abstractions.Events;

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
///     Provides a base implementation for entities with a specified key type.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
///     This abstract class serves as the foundation for all entities in the system by:
///     - Implementing core identity management through a generic key type
///     - Providing built-in concurrency control via row versioning
///     - Establishing a consistent pattern for entity identification and tracking
///     The generic key type allows for flexibility in choosing appropriate identifier types
///     while maintaining a consistent entity structure across the application.
/// </remarks>
public abstract class Entity<TKey> : IEntity<TKey>, IEventEntity
{
    #region Fields

    [NotMapped] private readonly Collection<object> _events = [];

    [NotMapped] private readonly Collection<Type> _eventTypes = [];

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="Entity{TKey}" /> class.
    /// </summary>
    protected Entity()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Entity{TKey}" /> class with a specified ID.
    /// </summary>
    /// <param name="id">The unique identifier for the entity.</param>
    /// <remarks>
    ///     This constructor is primarily used for EF Core data seeding scenarios.
    /// </remarks>
    protected Entity(TKey id) => Id = id;

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the unique identifier for this entity.
    /// </summary>
    /// <value>
    ///     The entity's unique identifier of type <typeparamref name="TKey" />.
    /// </value>
    public virtual TKey Id { get; private set; } = default!;

    #endregion

    #region Methods

    /// <inheritdoc />
    public void AddEvent(object eventObj) => _events.Add(eventObj);

    /// <inheritdoc />
    public void AddEvent<TEvent>()
        where TEvent : class
        => _eventTypes.Add(typeof(TEvent));

    /// <inheritdoc />
    public void ClearEvents()
    {
        _events.Clear();
        _eventTypes.Clear();
    }

    /// <inheritdoc />
    public (object[] Events, Type[] EventTypes) GetEvents()
    {
        var events = _events.ToArray();
        var eventTypes = _eventTypes.ToArray();
        return (events, eventTypes);
    }

    /// <summary>
    ///     Returns a string that represents the current entity.
    /// </summary>
    /// <returns>A string in the format "EntityTypeName 'Id'".</returns>
    public override string ToString() => $"{GetType().Name} '{Id}'";

    #endregion
}

/// <summary>
///     Provides a base implementation for entities with a GUID key.
/// </summary>
/// <remarks>
///     This abstract class specializes the generic <see cref="Entity{TKey}" /> class to use
///     GUIDs as the primary key type. This is the recommended default for distributed systems as it:
///     - Ensures global uniqueness
///     - Eliminates the need for database coordination when generating IDs
///     - Supports easier data merging and synchronization scenarios.
/// </remarks>
public abstract class Entity : Entity<Guid>
{
    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="Entity" /> class.
    /// </summary>
    protected Entity()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Entity" /> class with a specified GUID.
    /// </summary>
    /// <param name="id">The unique GUID identifier for the entity.</param>
    /// <remarks>
    ///     This constructor is primarily used for EF Core data seeding scenarios.
    /// </remarks>
    protected Entity(Guid id)
        : base(id)
    {
    }

    #endregion
}