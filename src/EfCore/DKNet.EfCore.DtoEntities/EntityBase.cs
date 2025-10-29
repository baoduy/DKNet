namespace DKNet.EfCore.DtoEntities;

public abstract class EntityBase(string createdBy)
{
    #region Properties

    public DateTime CreatedUtc { get; protected set; } = DateTime.UtcNow;

    public DateTime? UpdatedUtc { get; protected set; }

    public string CreatedBy { get; protected set; } = createdBy;

    public string? UpdatedBy { get; protected set; }

    #endregion

    #region Methods

    protected void SetUpdatedBy(string user)
    {
        this.UpdatedBy = user;
        this.UpdatedUtc = DateTime.UtcNow;
    }

    #endregion
}