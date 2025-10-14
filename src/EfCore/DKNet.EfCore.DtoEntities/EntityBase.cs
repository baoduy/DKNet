namespace DKNet.EfCore.DtoEntities;

public abstract class EntityBase
{
    protected EntityBase()
    {
        CreatedUtc = DateTime.UtcNow;
        CreatedBy = string.Empty;
    }

    public DateTime CreatedUtc { get; protected set; }
    public DateTime? UpdatedUtc { get; protected set; }
    public string CreatedBy { get; protected set; }
    public string? UpdatedBy { get; protected set; }

    protected void SetUpdatedBy(string user)
    {
        UpdatedBy = user;
        UpdatedUtc = DateTime.UtcNow;
    }
}
