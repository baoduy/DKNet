namespace DKNet.EfCore.DtoEntities;

public abstract class EntityBase(string createdBy)
{
    #region Properties

    public string CreatedBy { get; protected set; } = createdBy;

    public DateTime CreatedUtc { get; protected set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; protected set; }

    public DateTime? UpdatedUtc { get; protected set; }

    #endregion

    #region Methods

    protected void SetUpdatedBy(string user)
    {
        UpdatedBy = user;
        UpdatedUtc = DateTime.UtcNow;
    }

    #endregion
}