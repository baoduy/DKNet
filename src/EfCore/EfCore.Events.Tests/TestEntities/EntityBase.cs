using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.DataAuthorization;

namespace EfCore.Events.Tests.TestEntities;

public abstract class EntityBase<TKey> : AuditedEntity<TKey>, IOwnedBy
{
    #region Constructors

    /// <inheritdoc />
    protected EntityBase(TKey id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null) : base(id)
    {
        OwnedBy = ownedBy;
        SetCreatedBy(createdBy, createdOn);
    }

    #endregion

    #region Properties

    public string OwnedBy { get; private set; }

    #endregion

    #region Methods

    public void SetOwnedBy(string ownerKey)
    {
        OwnedBy = ownerKey;
    }

    public override string ToString() => $"{GetType().Name} '{Id}'";

    #endregion
}

public abstract class AggregateRoot(Guid id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null)
    : EntityBase<Guid>(id,
        ownedBy, createdBy, createdOn);