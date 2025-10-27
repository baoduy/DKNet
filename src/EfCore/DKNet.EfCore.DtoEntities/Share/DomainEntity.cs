using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.DtoEntities.Share;

public abstract class DomainEntity : AuditedEntity<Guid>, IConcurrencyEntity<byte[]>
{
    #region Constructors

    protected DomainEntity(string byUser)
    {
        SetCreatedBy(byUser);
    }

    protected DomainEntity(Guid id, string createdBy) : base(id)
    {
        SetCreatedBy(createdBy);
    }

    #endregion

    #region Properties

    public byte[]? RowVersion { get; private set; }

    #endregion

    #region Methods

    public void SetRowVersion(byte[] rowVersion)
    {
        RowVersion = rowVersion;
    }

    #endregion
}