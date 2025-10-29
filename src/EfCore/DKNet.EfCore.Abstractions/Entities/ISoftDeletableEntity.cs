using System.ComponentModel.DataAnnotations;
using FluentResults;

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
///     Represents an entity that supports soft deletion.
/// </summary>
public interface ISoftDeletableEntity
{
    #region Properties

    /// <summary>
    ///     Gets a value indicating whether this entity has been soft deleted.
    /// </summary>
    public bool IsDeleted { get; }

    /// <summary>
    ///     Gets the timestamp when this entity was deleted.
    /// </summary>
    public DateTimeOffset? DeletedOn { get; }

    /// <summary>
    ///     Gets the user who deleted this entity.
    /// </summary>
    [MaxLength(250)] public string? DeletedBy { get; }

    #endregion

    #region Methods

    /// <summary>
    ///     Marks this entity as deleted (soft delete).
    /// </summary>
    /// <param name="byUser">The user who is deleting the entity.</param>
    /// <param name="deletedOn">Optional timestamp for the deletion. If not provided, current UTC time is used.</param>
    /// <returns>A result indicating success or failure.</returns>
    IResultBase Delete(string byUser, DateTimeOffset? deletedOn = null);

    #endregion
}