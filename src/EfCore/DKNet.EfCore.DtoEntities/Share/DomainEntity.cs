using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.DtoEntities.Share;

public abstract class DomainEntity : AuditedEntity<Guid>, IConcurrencyEntity<byte[]>
{
    protected DomainEntity(string byUser)
    {
        SetCreatedBy(byUser);
    }

    protected DomainEntity(Guid id, string createdBy) : base(id)
    {
        SetCreatedBy(createdBy);
    }

    public void SetRowVersion(byte[] rowVersion)
    {
        RowVersion = rowVersion;
    }

    public byte[]? RowVersion { get; private set; }
}