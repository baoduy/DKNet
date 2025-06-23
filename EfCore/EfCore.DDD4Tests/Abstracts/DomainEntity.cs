using System;

namespace EfCore.DDD4Tests.Abstracts;

public abstract class DomainEntity : EntityBase<Guid>
{
    /// <inheritdoc />
    protected DomainEntity(Guid id, string createdBy, DateTimeOffset? createdOn = null) : base(id,createdBy,createdOn)
    {
    }

    /// <inheritdoc />
    protected DomainEntity()
    {
    }
}