using System.ComponentModel.DataAnnotations;

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
/// Defines the basic auditing properties required for tracking entity changes.
/// </summary>
/// <remarks>
/// This interface provides the fundamental properties needed for maintaining
/// audit trails of entity creation and modifications.
/// </remarks>
public interface IAuditedProperties
{
    /// <summary>
    /// Gets the identifier of the user who created the entity.
    /// </summary>
    [Required]
    [MaxLength(255)]
    string CreatedBy { get; }

    /// <summary>
    /// Gets the timestamp when the entity was created.
    /// </summary>
    [Required]
    DateTimeOffset CreatedOn { get; }

    /// <summary>
    /// Gets the identifier of the user who last updated the entity.
    /// </summary>
    [MaxLength(255)]
    string? UpdatedBy { get; }

    /// <summary>
    /// Gets the timestamp when the entity was last updated.
    /// </summary>
    DateTimeOffset? UpdatedOn { get; }
}

/// <summary>
/// Defines a contract for auditable entities with a specified key type.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
/// This interface combines entity identification, audit properties, and
/// concurrency control capabilities.
/// </remarks>
public interface IAuditedEntity<out TKey> : IEntity<TKey>, IAuditedProperties, IConcurrencyEntity
{
    /// <summary>
    /// Sets the creation audit information for the entity.
    /// </summary>
    /// <param name="userName">The identifier of the creating user.</param>
    /// <param name="createdOn">Optional creation timestamp.</param>
    void SetCreatedBy(string userName, DateTimeOffset? createdOn = null);

    /// <summary>
    /// Sets the update audit information for the entity.
    /// </summary>
    /// <param name="userName">The identifier of the updating user.</param>
    /// <param name="updatedOn">Optional update timestamp.</param>
    void SetUpdatedBy(string userName, DateTimeOffset? updatedOn = null);
}