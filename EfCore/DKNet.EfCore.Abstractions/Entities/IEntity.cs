using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
/// Defines the base contract for all entities in the system.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
/// This interface establishes the fundamental structure for entities,
/// ensuring they have a unique identifier of a specified type.
/// </remarks>
public interface IEntity<out TKey>
{
    /// <summary>
    /// Gets the unique identifier for the entity.
    /// </summary>
    /// <value>The entity's primary key value.</value>
    [Key]
    [Column(Order = 1)]
    TKey Id { get; }
}

