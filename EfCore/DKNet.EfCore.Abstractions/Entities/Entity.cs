using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
/// Provides a base implementation for entities with a specified key type.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
/// This abstract class serves as the foundation for all entities in the system by:
/// - Implementing core identity management through a generic key type
/// - Providing built-in concurrency control via row versioning
/// - Establishing a consistent pattern for entity identification and tracking
/// 
/// The generic key type allows for flexibility in choosing appropriate identifier types
/// while maintaining a consistent entity structure across the application.
/// </remarks>
public abstract class Entity<TKey> : IEntity<TKey>, IConcurrencyEntity
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Entity{TKey}"/> class.
    /// </summary>
    protected Entity()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Entity{TKey}"/> class with a specified ID.
    /// </summary>
    /// <param name="id">The unique identifier for the entity.</param>
    /// <remarks>
    /// This constructor is primarily used for EF Core data seeding scenarios.
    /// </remarks>
    protected Entity(TKey id)
    {
        Id = id;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the unique identifier for this entity.
    /// </summary>
    /// <value>
    /// The entity's unique identifier of type <typeparamref name="TKey"/>.
    /// </value>
    [Key]
    [Column(Order = 0)]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual TKey Id { get; private set; } = default!;

    /// <summary>
    /// Gets the row version used for optimistic concurrency control.
    /// </summary>
    /// <value>
    /// A byte array representing the current version of the entity row.
    /// </value>
    [Column(Order = 1000)]
    [Timestamp, ConcurrencyCheck]
    public virtual byte[]? RowVersion { get; private set; }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the row version for concurrency control.
    /// </summary>
    /// <param name="rowVersion">The new row version value to set.</param>
    public void SetRowVersion(byte[] rowVersion) => RowVersion = rowVersion;

    /// <summary>
    /// Returns a string that represents the current entity.
    /// </summary>
    /// <returns>A string in the format "EntityTypeName 'Id'".</returns>
    public override string ToString() => $"{GetType().Name} '{Id}'";

    #endregion
}

/// <summary>
/// Provides a base implementation for entities with a GUID key.
/// </summary>
/// <remarks>
/// This abstract class specializes the generic <see cref="Entity{TKey}"/> class to use
/// GUIDs as the primary key type. This is the recommended default for distributed systems as it:
/// - Ensures global uniqueness
/// - Eliminates the need for database coordination when generating IDs
/// - Supports easier data merging and synchronization scenarios
/// </remarks>
public abstract class Entity : Entity<Guid>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Entity"/> class.
    /// </summary>
    protected Entity()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Entity"/> class with a specified GUID.
    /// </summary>
    /// <param name="id">The unique GUID identifier for the entity.</param>
    /// <remarks>
    /// This constructor is primarily used for EF Core data seeding scenarios.
    /// </remarks>
    protected Entity(Guid id) : base(id)
    {
    }
}