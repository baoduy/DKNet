namespace EfCore.DDD4Tests.Abstracts;

public abstract class AggregateRoot : EntityBase<Guid>
{
    protected AggregateRoot(string ownedBy, string createdBy, DateTimeOffset? createdOn = null)
        : this(Guid.Empty, ownedBy, createdBy, createdOn)
    {
    }

    protected AggregateRoot(Guid id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null)
        : base(id, ownedBy, createdBy, createdOn)
    {
    }
}