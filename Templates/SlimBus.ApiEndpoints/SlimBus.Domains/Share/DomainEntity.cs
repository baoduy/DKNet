namespace SlimBus.Domains.Share;

public abstract class DomainEntity : EntityBase<Guid>
{
    /// <inheritdoc />
    protected DomainEntity(Guid id, string createdBy, DateTimeOffset? createdOn = null) : base(id, createdBy, createdOn)
    {
        SetCreatedBy(createdBy, createdOn);
    }

    /// <inheritdoc />
    protected DomainEntity()
    {
    }
}