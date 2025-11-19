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
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        CreatedBy = "system";
        UpdatedBy = "system";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsActive = true;
    }

    private GlobalExclusionTestEntity()
    {
        Id = Guid.Empty;
        Name = string.Empty;
        Description = string.Empty;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
    }

    #endregion

    #region Properties

    public DateTime CreatedAt { get; private set; }

    public string CreatedBy { get; private set; }

    public string Description { get; private set; }

    public Guid Id { get; private set; }

    public bool IsActive { get; private set; }

    public string Name { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public string UpdatedBy { get; private set; }

    #endregion
}