using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.DtoEntities;

/// <summary>
///     Test entity for global exclusion functionality.
///     Contains standard audit properties that should be globally excluded.
/// </summary>
public sealed class GlobalExclusionTestEntity : IEntity<Guid>
{
    #region Constructors

    public GlobalExclusionTestEntity(string name, string description)
    {
        this.Id = Guid.NewGuid();
        this.Name = name;
        this.Description = description;
        this.CreatedBy = "system";
        this.UpdatedBy = "system";
        this.CreatedAt = DateTime.UtcNow;
        this.UpdatedAt = DateTime.UtcNow;
        this.IsActive = true;
    }

    private GlobalExclusionTestEntity()
    {
        this.Id = Guid.Empty;
        this.Name = string.Empty;
        this.Description = string.Empty;
        this.CreatedBy = string.Empty;
        this.UpdatedBy = string.Empty;
    }

    #endregion

    #region Properties

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid Id { get; private set; }

    public string CreatedBy { get; private set; }

    public string Description { get; private set; }

    public string Name { get; private set; }

    public string UpdatedBy { get; private set; }

    #endregion
}