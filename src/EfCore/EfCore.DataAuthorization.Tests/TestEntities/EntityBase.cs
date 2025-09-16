using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.DataAuthorization.Tests.TestEntities;

public abstract class EntityBase<TKey> : AuditedEntity<TKey>, IOwnedBy
{
    /// <inheritdoc />
    protected EntityBase(TKey id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null)
        : base(id)
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
    : EntityBase<Guid>(id, ownedBy, createdBy, createdOn);

public class Root(string name, string ownedBy) : AggregateRoot(Guid.Empty, ownedBy, $"Unit Test {Guid.NewGuid()}")
{
    public string Name { get; private set; } = name;
}