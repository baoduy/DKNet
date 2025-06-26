namespace EfCore.TestDataLayer;

public abstract class BaseEntity : AuditedEntity<int>
{

    /// <inheritdoc />
    protected BaseEntity(string createdBy) : this(0, createdBy)
    {
    }

    /// <inheritdoc />
    protected BaseEntity(int id, string createdBy) : base(id, createdBy)
    {
    }

    /// <inheritdoc />
    protected BaseEntity()
    {
    }
}