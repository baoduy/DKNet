using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.DataAuthorization.Tests.TestEntities;

public abstract class EntityBase<TKey> : AuditedEntity<TKey>, IOwnedBy
{
    #region Constructors

    /// <inheritdoc />
    protected EntityBase(TKey id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null)
        : base(id)
    {
        this.OwnedBy = ownedBy;
        this.SetCreatedBy(createdBy, createdOn);
    }

    #endregion

    #region Properties

    public string OwnedBy { get; private set; }

    #endregion

    #region Methods

    public void SetOwnedBy(string ownerKey)
    {
        this.OwnedBy = ownerKey;
    }

    public override string ToString() => $"{this.GetType().Name} '{this.Id}'";

    #endregion
}

public abstract class AggregateRoot(Guid id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null)
    : EntityBase<Guid>(id, ownedBy, createdBy, createdOn);

public class Root(string name, string ownedBy) : AggregateRoot(Guid.Empty, ownedBy, $"Unit Test {Guid.NewGuid()}")
{
    #region Properties

    public string Name { get; private set; } = name;

    #endregion
}

internal sealed class RootEfConfig : DefaultEntityTypeConfiguration<Root>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<Root> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }

    #endregion
}