using DKNet.EfCore.Abstractions.Entities;

namespace SlimBus.Domains.Share;

public abstract class DomainEntity : AuditedEntity<Guid>
{
    #region Constructors

    /// <inheritdoc />
    protected DomainEntity(Guid id, string createdBy, DateTimeOffset? createdOn = null) : base(id)
    {
        this.SetCreatedBy(createdBy, createdOn);
    }

    /// <inheritdoc />
    protected DomainEntity()
    {
    }

    #endregion
}