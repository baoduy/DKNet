using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
///     Defines the base contract for all entities in the system.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
///     This interface establishes the fundamental structure for entities,
///     ensuring they have a unique identifier of a specified type.
/// </remarks>
public interface IEntity<out TKey>
{
    #region Properties

    /// <summary>
    ///     Gets the unique identifier for the entity.
    /// </summary>
    /// <value>The entity's primary key value.</value>
    TKey Id { get; }

    #endregion
}

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
    protected Entity(TKey id) => this.Id = id;

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

    public void AddEvent(object eventObj) => this._events.Add(eventObj);

    public void AddEvent<TEvent>() where TEvent : class => this._eventTypes.Add(typeof(TEvent));

    public (object[]events, Type[]eventTypes) GetEventsAndClear()
    {
        var events = this._events.ToArray();
        var eventTypes = this._eventTypes.ToArray();
        this._events.Clear();
        this._eventTypes.Clear();

        return (events, eventTypes);
    }

    /// <summary>
    ///     Returns a string that represents the current entity.
    /// </summary>
    /// <returns>A string in the format "EntityTypeName 'Id'".</returns>
    public override string ToString() => $"{this.GetType().Name} '{this.Id}'";

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
///     - Supports easier data merging and synchronization scenarios
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
    protected Entity(Guid id) : base(id)
    {
    }

    #endregion
}