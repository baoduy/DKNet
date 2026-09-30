using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.DataAuthorization.Tests.TestEntities;

/// <summary>
///     DRK-1897 shape (a): a concrete <see cref="IOwnedBy" /> root of a TPH hierarchy. The hierarchy is declared
///     through <see cref="OwnedCardPaymentEfConfig" />, the consumer path <c>UseAutoConfigModel()</c> scans.
/// </summary>
public class OwnedPayment(string reference, string ownedBy) : IOwnedBy
{
    #region Properties

    public Guid Id { get; private set; }

    public string Reference { get; private set; } = reference;

    public string OwnedBy { get; private set; } = ownedBy;

    #endregion
}

/// <summary>DRK-1897 shape (a): the TPH-derived type of <see cref="OwnedPayment" />.</summary>
public class OwnedCardPayment(string reference, string cardLast4, string ownedBy) : OwnedPayment(reference, ownedBy)
{
    #region Properties

    public string CardLast4 { get; private set; } = cardLast4;

    #endregion
}

internal sealed class OwnedPaymentEfConfig : DefaultEntityTypeConfiguration<OwnedPayment>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<OwnedPayment> builder)
    {
        base.Configure(builder);
        builder.Property(x => x.Reference).HasMaxLength(100);
    }

    #endregion
}

internal sealed class OwnedCardPaymentEfConfig : DefaultEntityTypeConfiguration<OwnedCardPayment>
{
    #region Methods

    // base.Configure is not called: it would declare a key on the derived type, which EF Core rejects.
    // The key comes from the root (OwnedPaymentEfConfig).
    public override void Configure(EntityTypeBuilder<OwnedCardPayment> builder)
    {
        builder.HasBaseType<OwnedPayment>();
        builder.Property(x => x.CardLast4).HasMaxLength(4);
    }

    #endregion
}
