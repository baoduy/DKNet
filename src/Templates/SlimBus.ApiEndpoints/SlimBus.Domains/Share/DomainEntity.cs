using DKNet.EfCore.Abstractions.Entities;

namespace SlimBus.Domains.Share;

public abstract class DomainEntity : AuditedEntity<Guid>
{
    /// <inheritdoc />
    protected DomainEntity(Guid id, string createdBy, DateTimeOffset? createdOn = null) : base(id)
    {
        SetCreatedBy(createdBy, createdOn);
    }

    /// <inheritdoc />
    protected DomainEntity()
    {
    }
}