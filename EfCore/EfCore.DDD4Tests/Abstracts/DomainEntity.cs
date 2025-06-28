namespace EfCore.DDD4Tests.Abstracts;

public abstract class DomainEntity : EntityBase<Guid>
{
    /// <inheritdoc />
    protected DomainEntity(Guid id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null) : base(id,
        ownedBy, createdBy, createdOn)
    {
    }
}