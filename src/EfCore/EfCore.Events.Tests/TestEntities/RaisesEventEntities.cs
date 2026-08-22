using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.DtoGenerator;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Events.Tests.TestEntities;

// Payload records — ordinary [GenerateDto] payloads, declared exactly as payload generation already
// supports. The raise rules living on the entities below are what connect them to the save pipeline.
[GenerateDto(typeof(Order))]
public partial record OrderPlacedEvent;

[GenerateDto(typeof(Order))]
public partial record OrderStatusChangedEvent;

[GenerateDto(typeof(Order))]
public partial record OrderChangedEvent;

[GenerateDto(typeof(Order))]
public partial record OrderCancelledEvent;

[GenerateDto(typeof(Order))]
public partial record OrderRecordedEvent;

[GenerateDto(typeof(Customer), Exclude = new[] { "TaxIdentifier" })]
public partial record CustomerRegisteredEvent;

[GenerateDto(typeof(Customer))]
public partial record CustomerUpdatedEvent;

[GenerateDto(typeof(ShipmentLeg))]
public partial record ShipmentLegPlacedEvent;

// Convention-form payload — no hand-written [GenerateDto] record: the build generates
// LoyaltyMembershipTierUpdatedEvent below from the label-less [RaisesEvent(EventOperations.Updated,
// nameof(Tier))] rule carried by LoyaltyMembership. This hand-authored declaration is the developer's own
// extension point — a compatible partial record stub the generated partial merges into (DRK-471 §3
// requirement 2).
public partial record LoyaltyMembershipTierUpdatedEvent
{
    /// <summary>Hand-added member proving the generated record is genuinely partially-declarable.</summary>
    public string Note => "hand-authored extension";
}

[GenerateDto(typeof(LoyaltyMembership))]
public partial record LoyaltyMembershipEvents;

/// <summary>
///     Mapped entity (not an aggregate root) carrying five raise rules: a duplicated create rule for
///     <see cref="OrderPlacedEvent" /> (two rules naming the same payload for the same operation raise
///     it once — see <see cref="EfCore.Events.Tests.RaisesEventIntegrationTests" />), a narrowed update
///     rule, an unnarrowed update rule, a delete rule and a rule covering both create and update.
///     <see cref="ChangeStatus" /> also hand-raises <see cref="OrderStatusNotifiedEvent" />, proving
///     declared and hand-raised events coexist.
/// </summary>
[RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Created)]
[RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Created)]
[RaisesEvent(typeof(OrderStatusChangedEvent), EventOperations.Updated, nameof(Order.Status))]
[RaisesEvent(EventOperations.Updated, nameof(Order.Status))]
[RaisesEvent(typeof(OrderChangedEvent), EventOperations.Updated)]
[RaisesEvent(typeof(OrderCancelledEvent), EventOperations.Deleted)]
[RaisesEvent(typeof(OrderRecordedEvent), EventOperations.Created | EventOperations.Updated)]
public class Order : EntityBase<Guid>
{
    #region Constructors

    public Order(string companyName, string status, string deliveryNote, string createdBy)
        : base(Guid.CreateVersion7(), "TestOwner", createdBy)
    {
        CompanyName = companyName;
        Status = status;
        DeliveryNote = deliveryNote;
    }

    #endregion

    #region Properties

    [Required] public string CompanyName { get; private set; }

    [Required] public string Status { get; private set; }

    public string DeliveryNote { get; private set; }

    #endregion

    #region Methods

    public void ChangeStatus(string status)
    {
        Status = status;
        AddEvent(new OrderStatusNotifiedEvent { OrderId = Id, Status = status });
    }

    public void UpdateDeliveryNote(string deliveryNote) => DeliveryNote = deliveryNote;

    #endregion
}

/// <summary>Hand-raised event used by <see cref="Order.ChangeStatus" />; a distinct type from the declared one.</summary>
public sealed record OrderStatusNotifiedEvent
{
    #region Properties

    public required Guid OrderId { get; init; }

    public required string Status { get; init; }

    #endregion
}

/// <summary>
///     Mapped entity carrying a create rule that excludes <c>TaxIdentifier</c>, an unnarrowed update
///     rule, and a string-form create rule (<see cref="LoyaltyMembershipTierUpdatedEvent" />-style generation,
///     here proving the default generated shape carries the entity's default values — name, email, tax
///     identifier), with an owned <see cref="OwnedAddress" /> value. Used to verify exclusion honouring
///     and the nested-owned-value limitation (a change confined to <see cref="Address" /> does not raise
///     the owner's update event). The "Verified" rule (DRK-692 §5 @integration) is a convention-form
///     declaration whose own <c>Exclude</c> keeps <c>TaxIdentifier</c> out of its COMPOSED payload — distinct
///     from <see cref="CustomerRegisteredEvent" />, whose exclusion instead comes from its own hand-written
///     <c>[GenerateDto]</c> filter.
/// </summary>
[RaisesEvent(typeof(CustomerRegisteredEvent), EventOperations.Created)]
[RaisesEvent(typeof(CustomerUpdatedEvent), EventOperations.Updated)]
[RaisesEvent("Touched", EventOperations.Created)]
[RaisesEvent("Verified", EventOperations.Created, Exclude = new[] { nameof(TaxIdentifier) })]
public class Customer : EntityBase<Guid>
{
    #region Constructors

    public Customer(string name, string email, string taxIdentifier, string createdBy)
        : base(Guid.CreateVersion7(), "TestOwner", createdBy)
    {
        Name = name;
        Email = email;
        TaxIdentifier = taxIdentifier;
        Address = new OwnedAddress();
    }

    #endregion

    #region Properties

    [Required] public string Name { get; private set; }

    [Required] public string Email { get; private set; }

    public string TaxIdentifier { get; private set; }

    /// <summary>Nested owned value — a change confined to this must not raise <see cref="Customer" />'s update event.</summary>
    public OwnedAddress Address { get; private set; }

    #endregion

    #region Methods

    public void UpdateAddressLine(string line) => Address.Line = line;

    #endregion
}

/// <summary>Owned value type nested inside <see cref="Customer" />.</summary>
public sealed class OwnedAddress
{
    #region Properties

    public string Line { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    #endregion
}

/// <summary>
///     Mapped entity that is not an aggregate root (derives from <see cref="EntityBase{TKey}" /> directly)
///     and carries a single create rule, proving any mapped entity may declare raise rules.
/// </summary>
[RaisesEvent(typeof(ShipmentLegPlacedEvent), EventOperations.Created)]
public class ShipmentLeg : EntityBase<Guid>
{
    #region Constructors

    public ShipmentLeg(int legNumber, string origin, string destination, string createdBy)
        : base(Guid.CreateVersion7(), "TestOwner", createdBy)
    {
        LegNumber = legNumber;
        Origin = origin;
        Destination = destination;
    }

    #endregion

    #region Properties

    public int LegNumber { get; private set; }

    [Required] public string Origin { get; private set; }

    [Required] public string Destination { get; private set; }

    #endregion
}

/// <summary>
///     Mapped entity carrying a type-form create+update rule narrowed to <see cref="Points" /> (regression
///     guard — same payload, same behaviour as before the string form shipped) and a string-form update
///     rule narrowed to the unrelated <see cref="Tier" /> property, proving both forms coexist and raise
///     independently: a <see cref="Points" />-only save raises only <see cref="LoyaltyMembershipEvents" />,
///     and a <see cref="Tier" /> change raises only the generated <see cref="LoyaltyMembershipTierUpdatedEvent" />.
/// </summary>
[RaisesEvent(typeof(LoyaltyMembershipEvents), EventOperations.Created | EventOperations.Updated, nameof(Points))]
[RaisesEvent(EventOperations.Updated, nameof(Tier))]
public class LoyaltyMembership : EntityBase<Guid>
{
    #region Constructors

    public LoyaltyMembership(int points, string tier, string createdBy)
        : base(Guid.CreateVersion7(), "TestOwner", createdBy)
    {
        Points = points;
        Tier = tier;
    }

    #endregion

    #region Properties

    public int Points { get; private set; }

    [Required] public string Tier { get; private set; }

    #endregion

    #region Methods

    public void AddPoints(int points) => Points += points;

    public void ChangeTier(string tier) => Tier = tier;

    #endregion
}

/// <summary>
///     Mapped entity carrying no raise rule; it keeps raising its events by hand and must behave exactly
///     as it does on the current release (regression guard).
/// </summary>
public class Product : EntityBase<Guid>
{
    #region Constructors

    public Product(string name, string createdBy)
        : base(Guid.CreateVersion7(), "TestOwner", createdBy)
    {
        Name = name;
    }

    #endregion

    #region Properties

    [Required] public string Name { get; private set; }

    #endregion

    #region Methods

    public void Rename(string name) => Name = name;

    public void FlagCreated(string by) => AddEvent(new ProductLifecycleEvent { ProductId = Id, Operation = "Created", Name = Name, By = by });

    public void FlagUpdated(string by) => AddEvent(new ProductLifecycleEvent { ProductId = Id, Operation = "Updated", Name = Name, By = by });

    public void FlagDeleted(string by) => AddEvent(new ProductLifecycleEvent { ProductId = Id, Operation = "Deleted", Name = Name, By = by });

    #endregion
}

/// <summary>Hand-raised event used by <see cref="Product" /> throughout its lifecycle.</summary>
public sealed record ProductLifecycleEvent
{
    #region Properties

    public required Guid ProductId { get; init; }

    public required string Operation { get; init; }

    public required string Name { get; init; }

    public required string By { get; init; }

    #endregion
}

internal sealed class OrderEfConfig : DefaultEntityTypeConfiguration<Order>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<Order> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CompanyName).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(50);
        builder.ToTable(t => t.HasCheckConstraint("CK_Order_Status", "\"Status\" <> ''"));
    }

    #endregion
}

internal sealed class CustomerEfConfig : DefaultEntityTypeConfiguration<Customer>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<Customer> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.OwnsOne(x => x.Address);
    }

    #endregion
}

internal sealed class LoyaltyMembershipEfConfig : DefaultEntityTypeConfiguration<LoyaltyMembership>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<LoyaltyMembership> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
    }

    #endregion
}

internal sealed class ShipmentLegEfConfig : DefaultEntityTypeConfiguration<ShipmentLeg>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<ShipmentLeg> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Origin).HasMaxLength(100);
        builder.Property(x => x.Destination).HasMaxLength(100);
    }

    #endregion
}

internal sealed class ProductEfConfig : DefaultEntityTypeConfiguration<Product>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }

    #endregion
}
