using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
/// Base class for entities that require audit tracking with a specified key type.
/// Provides automatic tracking of creation and modification timestamps and user information.
/// </summary>
/// <typeparam name="TKey">The type of the entity's primary key.</typeparam>
/// <remarks>
/// This class implements basic audit functionality including:
/// - Creation tracking (user and timestamp)
/// - Modification tracking (user and timestamp)
/// - Automatic timestamp management
/// - Concurrency control through inheritance
/// </remarks>
public abstract class AuditedEntity<TKey> : Entity<TKey>, IAuditedEntity<TKey>
{
    protected AuditedEntity()
    {
    }

    protected AuditedEntity(TKey id) : base(id)
    {
    }

    protected AuditedEntity(TKey id, string createdBy, DateTimeOffset? createdOn = null)
        : base(id)
    {
        SetCreatedBy(createdBy, createdOn);
    }

    /// <summary>
    /// Gets the user who created this entity.
    /// </summary>
    [Required]
    [StringLength(500)]
    [Column(Order = 996)]
    public virtual string CreatedBy { get; private set; } = null!;

    /// <summary>
    /// Gets the timestamp when this entity was created.
    /// </summary>
    [Required]
    [Column(Order = 997)]
    public virtual DateTimeOffset CreatedOn { get; private set; }

    /// <summary>
    /// Gets the user who last modified this entity.
    /// </summary>
    [StringLength(500)]
    [Column(Order = 998)]
    public virtual string? UpdatedBy { get; private set; }

    /// <summary>
    /// Gets the timestamp when this entity was last modified.
    /// </summary>
    [Column(Order = 999)]
    public virtual DateTimeOffset? UpdatedOn { get; private set; }

    [NotMapped] public virtual string LastModifiedBy => UpdatedBy ?? CreatedBy;

    [NotMapped] public virtual DateTimeOffset LastModifiedOn => UpdatedOn ?? CreatedOn;

    /// <summary>
    /// Sets the creation audit information for this entity.
    /// </summary>
    /// <param name="userName">The username of the creator.</param>
    /// <param name="createdOn">Optional creation timestamp. Defaults to UTC now if not specified.</param>
    /// <exception cref="ArgumentNullException">Thrown when userName is null.</exception>
    public void SetCreatedBy(string userName, DateTimeOffset? createdOn = null)
    {
        if (!string.IsNullOrEmpty(CreatedBy)) return;

        CreatedBy = userName ?? throw new ArgumentNullException(nameof(userName));
        CreatedOn = createdOn ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the modification audit information for this entity.
    /// </summary>
    /// <param name="userName">The username of the modifier.</param>
    /// <param name="updatedOn">Optional modification timestamp. Defaults to UTC now if not specified.</param>
    /// <exception cref="ArgumentNullException">Thrown when userName is null or empty.</exception>
    public void SetUpdatedBy(string userName, DateTimeOffset? updatedOn = null)
    {
        if (string.IsNullOrEmpty(userName))
            throw new ArgumentNullException(nameof(userName));

        UpdatedBy = userName;
        UpdatedOn = updatedOn ?? DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Base class for entities that require audit tracking with a GUID key.
/// Provides a specialized implementation of AuditedEntity using GUIDs as the primary key.
/// </summary>
public abstract class AuditedEntity : AuditedEntity<Guid>
{
    protected AuditedEntity()
    {
    }

    protected AuditedEntity(Guid id) : base(id)
    {
    }

    protected AuditedEntity(Guid id, string createdBy, DateTimeOffset? createdOn = null)
        : base(id, createdBy, createdOn)
    {
    }
}