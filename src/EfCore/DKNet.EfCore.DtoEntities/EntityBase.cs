namespace DKNet.EfCore.DtoEntities;

public abstract class EntityBase
{
    #region Constructors

    protected EntityBase()
    {
        CreatedUtc = DateTime.UtcNow;
        CreatedBy = string.Empty;
    }

    #endregion

    #region Properties

    public string CreatedBy { get; protected set; }

    public DateTime CreatedUtc { get; protected set; }
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