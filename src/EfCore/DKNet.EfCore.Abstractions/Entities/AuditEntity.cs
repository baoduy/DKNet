using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.Abstractions.Attributes;

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
///     Defines the basic auditing properties required for tracking entity changes.
/// </summary>
/// <remarks>
///     This interface provides the fundamental properties needed for maintaining
///     audit trails of entity creation and modifications.
/// </remarks>
public interface IAuditedProperties
{
    #region Properties

    /// <summary>
    ///     Gets the timestamp when the entity was created.
    /// </summary>
    [IgnoreAuditLog]
    DateTimeOffset CreatedOn { get; }

    /// <summary>
    ///     Gets the timestamp when the entity was last updated.
    /// </summary>
    [IgnoreAuditLog]
    DateTimeOffset? UpdatedOn { get; }

    /// <summary>
    ///     Gets the identifier of the user who created the entity.
    /// </summary>
    [IgnoreAuditLog]
    [MaxLength(500)]
    string CreatedBy { get; }

    /// <summary>
    ///     Gets the identifier of the user who last updated the entity.
    /// </summary>
    [MaxLength(500)]
    [IgnoreAuditLog]
    string? UpdatedBy { get; }

    #endregion
}

/// <summary>
///     Defines a contract for auditable entities with a specified key type.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
///     This interface combines entity identification, audit properties, and
///     concurrency control capabilities.
/// </remarks>
public interface IAuditedEntity<out TKey> : IEntity<TKey>, IAuditedProperties
{
    #region Methods

    /// <summary>
    ///     Sets the creation audit information for the entity.
    /// </summary>
    /// <param name="userName">The identifier of the creating user.</param>
    /// <param name="createdOn">Optional creation timestamp.</param>
    void SetCreatedBy(string userName, DateTimeOffset? createdOn = null);

    /// <summary>
    ///     Sets the update audit information for the entity.
    /// </summary>
    /// <param name="userName">The identifier of the updating user.</param>
    /// <param name="updatedOn">Optional update timestamp.</param>
    void SetUpdatedBy(string userName, DateTimeOffset? updatedOn = null);

    #endregion
}

/// <summary>
///     Base class for entities that require audit tracking with a specified key type.
///     Provides automatic tracking of creation and modification timestamps and user information.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
///     This class implements basic audit functionality including
///     - Creation tracking (user and timestamp)
///     - Modification tracking (user and timestamp)
///     - Automatic timestamp management
///     - Concurrency control through inheritance
/// </remarks>
public abstract class AuditedEntity<TKey> : Entity<TKey>,
    IAuditedEntity<TKey>
{
    #region Constructors

    protected AuditedEntity()
    {
    }

    protected AuditedEntity(TKey id) : base(id)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the timestamp when this entity was created.
    /// </summary>
    public DateTimeOffset CreatedOn { get; private set; }

    [NotMapped] public DateTimeOffset LastModifiedOn => this.UpdatedOn ?? this.CreatedOn;

    /// <summary>
    ///     Gets the timestamp when this entity was last modified.
    /// </summary>
    public DateTimeOffset? UpdatedOn { get; private set; }

    /// <summary>
    ///     Gets the user who created this entity.
    /// </summary>
    [MaxLength(500)]
    public string CreatedBy { get; private set; } = null!;

    [NotMapped] public string LastModifiedBy => this.UpdatedBy ?? this.CreatedBy;

    /// <summary>
    ///     Gets the user who last modified this entity.
    /// </summary>
    [MaxLength(500)]
    public string? UpdatedBy { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    ///     Sets the creation audit information for this entity.
    /// </summary>
    /// <param name="userName">The username of the creator.</param>
    /// <param name="createdOn">Optional creation timestamp. Defaults to UTC now if not specified.</param>
    /// <exception cref="ArgumentNullException">Thrown when userName is null.</exception>
    public void SetCreatedBy(string userName, DateTimeOffset? createdOn = null)
    {
        if (!string.IsNullOrEmpty(this.CreatedBy))
        {
            return;
        }

        this.CreatedBy = userName ?? throw new ArgumentNullException(nameof(userName));
        this.CreatedOn = createdOn ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Sets the modification audit information for this entity.
    /// </summary>
    /// <param name="userName">The username of the modifier.</param>
    /// <param name="updatedOn">Optional modification timestamp. Defaults to UTC now if not specified.</param>
    /// <exception cref="ArgumentNullException">Thrown when userName is null or empty.</exception>
    public void SetUpdatedBy(string userName, DateTimeOffset? updatedOn = null)
    {
        if (string.IsNullOrEmpty(userName))
        {
            throw new ArgumentNullException(nameof(userName));
        }

        this.UpdatedBy = userName;
        this.UpdatedOn = updatedOn ?? DateTimeOffset.UtcNow;
    }

    #endregion
}

/// <summary>
///     Base class for entities that require audit tracking with a GUID key.
///     Provides a specialized implementation of AuditedEntity using GUIDs as the primary key.
/// </summary>
public abstract class AuditedEntity : AuditedEntity<Guid>
{
    #region Constructors

    protected AuditedEntity()
    {
    }

    protected AuditedEntity(Guid id) : base(id)
    {
    }

    #endregion
}