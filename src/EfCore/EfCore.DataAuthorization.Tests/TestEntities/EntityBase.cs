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

    /// <summary>
    ///     Test-only bridge to the protected <see cref="AuditedEntity{TKey}.SetUpdatedBy" />, simulating a
    ///     domain method that explicitly records its own modifier before saving.
    /// </summary>
    public void RecordModifier(string modifiedBy, DateTimeOffset? modifiedOn = null) =>
        SetUpdatedBy(modifiedBy, modifiedOn);

    public override string ToString() => $"{GetType().Name} '{Id}'";

    #endregion
}

public abstract class AggregateRoot(Guid id, string ownedBy, string createdBy, DateTimeOffset? createdOn = null)
    : EntityBase<Guid>(id, ownedBy, createdBy, createdOn);

public class Root(string name, string ownedBy) : AggregateRoot(Guid.Empty, ownedBy, $"Unit Test {Guid.NewGuid()}")
{
    #region Properties

    public string Name { get; private set; } = name;

    /// <summary>Test-only delete marker — this repo has no <c>ISoftDeletableEntity</c> implementation yet.</summary>
    public bool IsDeleted { get; private set; }

    #endregion

    #region Methods

    /// <summary>An ordinary domain mutation that never touches the audit modifier itself.</summary>
    public void Rename(string name) => Name = name;

    /// <summary>Simulates marking a record deleted as an ordinary modification (no real soft-delete here).</summary>
    public void MarkDeleted() => IsDeleted = true;

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

/// <summary>
///     A plain <see cref="IOwnedBy" /> implementer with no audit tracking at all — used to prove that a
///     modified entity which is <see cref="IOwnedBy" /> but not <see cref="IAuditedProperties" /> saves
///     without error and is never stamped by <c>StampModifiedEntity</c>.
/// </summary>
public class OwnedOnlyEntity(string name, string ownedBy) : IOwnedBy
{
    #region Properties

    public Guid Id { get; private set; }

    public string Name { get; private set; } = name;

    public string OwnedBy { get; private set; } = ownedBy;

    #endregion

    #region Methods

    public void Rename(string name) => Name = name;

    #endregion
}

internal sealed class OwnedOnlyEntityEfConfig : DefaultEntityTypeConfiguration<OwnedOnlyEntity>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<OwnedOnlyEntity> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }

    #endregion
}

/// <summary>
///     An audited entity whose <see cref="IAuditedProperties.UpdatedBy" /> column is deliberately unmapped —
///     used to prove a modified entity saves without error when <c>StampModifiedEntity</c>'s
///     <c>FindProperty(UpdatedBy)</c> guard finds nothing to stamp.
/// </summary>
public sealed class AuditedNoUpdatedByColumnEntity : AuditedEntity<Guid>
{
    #region Constructors

    public AuditedNoUpdatedByColumnEntity(string name, string createdBy) : base(Guid.Empty)
    {
        Name = name;
        SetCreatedBy(createdBy);
    }

    #endregion

    #region Properties

    public string Name { get; private set; }

    #endregion

    #region Methods

    public void Rename(string name) => Name = name;

    #endregion
}

internal sealed class AuditedNoUpdatedByColumnEntityEfConfig
    : DefaultEntityTypeConfiguration<AuditedNoUpdatedByColumnEntity>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<AuditedNoUpdatedByColumnEntity> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Ignore(x => x.UpdatedBy);
    }

    #endregion
}

/// <summary>
///     An audited entity whose <see cref="IAuditedProperties.UpdatedOn" /> column is deliberately unmapped —
///     used to prove <c>HasExplicitModifier</c>'s <c>FindProperty(UpdatedOn)</c> guard returns <see langword="false" />
///     when <see cref="IAuditedProperties.UpdatedBy" /> is mapped but <c>UpdatedOn</c> is not, so a modified entity
///     saves without error and is still stamped with the ambient ownership key.
/// </summary>
public sealed class AuditedNoUpdatedOnColumnEntity : AuditedEntity<Guid>
{
    #region Constructors

    public AuditedNoUpdatedOnColumnEntity(string name, string createdBy) : base(Guid.Empty)
    {
        Name = name;
        SetCreatedBy(createdBy);
    }

    #endregion

    #region Properties

    public string Name { get; private set; }

    #endregion

    #region Methods

    public void Rename(string name) => Name = name;

    #endregion
}

internal sealed class AuditedNoUpdatedOnColumnEntityEfConfig
    : DefaultEntityTypeConfiguration<AuditedNoUpdatedOnColumnEntity>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<AuditedNoUpdatedOnColumnEntity> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Ignore(x => x.UpdatedOn);
    }

    #endregion
}