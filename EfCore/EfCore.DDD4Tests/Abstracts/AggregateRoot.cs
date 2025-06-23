using System;

namespace EfCore.DDD4Tests.Abstracts;

public abstract class AggregateRoot : EntityBase<Guid>
{
    protected AggregateRoot(string createdBy, DateTimeOffset? createdOn = null)
        : this(Guid.Empty, createdBy, createdOn)
    {
    }

    protected AggregateRoot(Guid id, string createdBy, DateTimeOffset? createdOn = null)
        : base(id, createdBy, createdOn)
    {
    }

    /// <inheritdoc />
    protected AggregateRoot()
    {
    }
}