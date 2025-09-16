using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.DataAuthorization;

namespace EfCore.Events.Tests.TestEntities;

public abstract class EntityBase<TKey> : AuditedEntity<TKey>, IOwnedBy
{
    /// <inheritdoc />
    protected EntityBase(TKey id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null) : base(id)
    {
        OwnedBy = ownedBy;
        SetCreatedBy(createdBy, createdOn);
    }

    public string OwnedBy { get; private set; }

    public void SetOwnedBy(string ownerKey)
    {
        OwnedBy = ownerKey;
    }

    public override string ToString() => $"{GetType().Name} '{Id}'";
}

public abstract class AggregateRoot(Guid id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null)
    : EntityBase<Guid>(id,
        ownedBy, createdBy, createdOn);