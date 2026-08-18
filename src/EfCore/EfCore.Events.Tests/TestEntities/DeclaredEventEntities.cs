using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.DtoGenerator;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Events.Tests.TestEntities;

/// <summary>
///     Mapped entity (not an aggregate root) that declares create, update and delete events. The four
///     declarations exercise the no-suffix naming (<c>OrderCreatedEvent</c>/<c>OrderUpdatedEvent</c>/
///     <c>OrderDeletedEvent</c>), suffix naming (<c>OrderStatusChangedEvent</c>) and update narrowing to
///     <c>Status</c>. <see cref="Contact" /> is a plain reference type and must not appear in any generated
///     payload because complex types are excluded by default.
/// </summary>
[GenerateEvent(Kinds = EventKinds.Created)]
[GenerateEvent(Kinds = EventKinds.Updated)]
[GenerateEvent(Kinds = EventKinds.Updated, NameSuffix = "StatusChanged", Properties = new[] { "Status" })]
[GenerateEvent(Kinds = EventKinds.Deleted)]
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

    /// <summary>Plain reference type, excluded from generated payloads by default (complex type).</summary>
    public OrderContact? Contact { get; private set; }

    #endregion

    #region Methods

    public void ChangeStatus(string status) => Status = status;

    public void UpdateDeliveryNote(string deliveryNote) => DeliveryNote = deliveryNote;

    #endregion
}

/// <summary>Plain reference type used to verify complex properties are dropped from generated payloads.</summary>
public sealed class OrderContact
{
    #region Properties

    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    #endregion
}

/// <summary>
///     Mapped entity declaring a create event that excludes <c>TaxIdentifier</c> and an unnarrowed update
///     event, with an owned <see cref="Address" /> value. Used to verify exclusion honouring and the
///     nested-owned-value limitation.
/// </summary>
[GenerateEvent(Kinds = EventKinds.Created, Exclude = new[] { "TaxIdentifier" })]
[GenerateEvent(Kinds = EventKinds.Updated)]
public class Customer : EntityBase<Guid>
{
    #region Constructors

    public Customer(string name, string taxIdentifier, string createdBy)
        : base(Guid.CreateVersion7(), "TestOwner", createdBy)
    {
        Name = name;
        TaxIdentifier = taxIdentifier;
        Address = new OwnedAddress();
    }

    #endregion

    #region Properties

    [Required] public string Name { get; private set; }

    public string TaxIdentifier { get; private set; }

    /// <summary>Nested owned value.</summary>
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

/// <summary>Mapped entity declaring a create event that includes only <c>Name</c>.</summary>
[GenerateEvent(Kinds = EventKinds.Created, Include = new[] { "Name" })]
public class Vendor : EntityBase<Guid>
{
    #region Constructors

    public Vendor(string name, string code, string createdBy)
        : base(Guid.CreateVersion7(), "TestOwner", createdBy)
    {
        Name = name;
        Code = code;
    }

    #endregion

    #region Properties

    [Required] public string Name { get; private set; }

    public string Code { get; private set; }

    #endregion
}

/// <summary>
///     Mapped entity (not an aggregate root) declaring a suffix-named create event
///     (<c>ShipmentLegPlacedEvent</c>) and a single declaration covering create and update
///     (<c>ShipmentLegRecordedEvent</c>).
/// </summary>
[GenerateEvent(Kinds = EventKinds.Created, NameSuffix = "Placed")]
[GenerateEvent(Kinds = EventKinds.Created | EventKinds.Updated, NameSuffix = "Recorded")]
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

    #region Methods

    public void ChangeOrigin(string origin) => Origin = origin;

    #endregion
}

/// <summary>
///     Mapped entity that both declares an update event narrowed to <c>Status</c> and hand-raises its own
///     status-change event from the mutation method, proving declared and hand-raised events coexist.
/// </summary>
[GenerateEvent(Kinds = EventKinds.Updated, NameSuffix = "StatusChanged", Properties = new[] { "Status" })]
public class Shipment : EntityBase<Guid>
{
    #region Constructors

    public Shipment(string status, string carrier, string createdBy)
        : base(Guid.CreateVersion7(), "TestOwner", createdBy)
    {
        Status = status;
        Carrier = carrier;
    }

    #endregion

    #region Properties

    [Required] public string Status { get; private set; }

    public string Carrier { get; private set; }

    #endregion

    #region Methods

    public void Ship(string status, string carrier)
    {
        Status = status;
        Carrier = carrier;
        AddEvent(new ShipmentStatusNotifiedEvent { ShipmentId = Id, Status = status, Carrier = carrier });
    }

    #endregion
}

/// <summary>
///     Mapped entity carrying none of the new declarations; it keeps raising its events by hand and must
///     behave exactly as before (regression guard). The generated pipeline must not add anything extra.
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

/// <summary>Hand-raised event used by <see cref="Shipment" />; a distinct type from the declared one.</summary>
public sealed record ShipmentStatusNotifiedEvent
{
    #region Properties

    public required Guid ShipmentId { get; init; }

    public required string Status { get; init; }

    public string Carrier { get; init; } = string.Empty;

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
        builder.Ignore(x => x.Contact);
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

internal sealed class VendorEfConfig : DefaultEntityTypeConfiguration<Vendor>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<Vendor> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
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

internal sealed class ShipmentEfConfig : DefaultEntityTypeConfiguration<Shipment>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<Shipment> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(50);
        builder.Property(x => x.Carrier).HasMaxLength(100);
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