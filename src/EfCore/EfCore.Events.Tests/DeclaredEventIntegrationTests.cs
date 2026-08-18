using EfCore.Events.Tests.TestEntities;

namespace EfCore.Events.Tests;

/// <summary>
///     DRK-437 acceptance criteria — <c>@integration</c> scenarios for auto-generated domain events.
///     Every scenario runs against a real (SQLite) save pipeline driven by <c>DddContext</c>, where the
///     <c>[GenerateEvent]</c> declarations on the test entities produce the event records and the
///     registration via the running source generator.
/// </summary>
public class DeclaredEventIntegrationTests(DeclaredEventFixture fixture)
    : IClassFixture<DeclaredEventFixture>
{
    #region Methods

    [Fact]
    public async Task DeclaredCreateEvent_WithSuffix_IsPublishedWhenEntityPersisted()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var leg = new ShipmentLeg(1, "Hanoi", "Saigon", "verify");

        // Act
        db.Set<ShipmentLeg>().Add(leg);
        await db.SaveChangesAsync();

        // Assert
        var published = DeclaredEventPublisher.Events.OfType<ShipmentLegPlacedEvent>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.LegNumber == 1 && e.Origin == "Hanoi" && e.Destination == "Saigon");
    }

    [Fact]
    public async Task DeclaredCreateEvent_NoSuffix_IsNamedFromEntityAndOperation()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");

        // Act
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();

        // Assert
        var published = DeclaredEventPublisher.Events.OfType<OrderCreatedEvent>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.CompanyName == "Acme Pte Ltd" && e.Status == "Pending");
    }

    [Fact]
    public async Task NarrowedUpdateEvent_RaisesWhenListedPropertyChanges()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var order = new Order("Acme Pte Ltd", "Pending", "Handle with care", "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        DeclaredEventPublisher.Events.Clear();

        // Act
        order.ChangeStatus("Shipped");
        await db.SaveChangesAsync();

        // Assert
        var published = DeclaredEventPublisher.Events.OfType<OrderStatusChangedEvent>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.Status == "Shipped");
    }

    [Fact]
    public async Task NarrowedUpdateEvent_StaysSilentWhenOnlyOtherPropertiesChange()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var order = new Order("Acme Pte Ltd", "Pending", "Handle with care", "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        DeclaredEventPublisher.Events.Clear();

        // Act
        order.UpdateDeliveryNote("Leave at reception");
        await db.SaveChangesAsync();

        // Assert
        DeclaredEventPublisher.Events.OfType<OrderStatusChangedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public async Task UnnarrowedUpdateEvent_RaisesForAnyChange()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        DeclaredEventPublisher.Events.Clear();

        // Act
        order.UpdateDeliveryNote("Leave at reception");
        await db.SaveChangesAsync();

        // Assert
        var published = DeclaredEventPublisher.Events.OfType<OrderUpdatedEvent>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.DeliveryNote == "Leave at reception");
    }

    [Fact]
    public async Task DeleteEvent_CarriesValues_AsTheyWereBeforeRemoval()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var order = new Order("Acme Pte Ltd", "Shipped", "Handle with care", "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        DeclaredEventPublisher.Events.Clear();

        // Act
        db.Set<Order>().Remove(order);
        await db.SaveChangesAsync();

        // Assert
        var published = DeclaredEventPublisher.Events.OfType<OrderDeletedEvent>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.CompanyName == "Acme Pte Ltd" && e.Status == "Shipped");
    }

    [Fact]
    public async Task SingleDeclaration_CoveringTwoOperations_RaisesOnBoth()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var leg = new ShipmentLeg(2, "Hanoi", "Saigon", "verify");

        // Act
        db.Set<ShipmentLeg>().Add(leg);
        await db.SaveChangesAsync();
        var firstRaiseCount = DeclaredEventPublisher.Events.OfType<ShipmentLegRecordedEvent>().Count();

        leg.ChangeOrigin("Da Nang");
        await db.SaveChangesAsync();
        var secondRaiseCount = DeclaredEventPublisher.Events.OfType<ShipmentLegRecordedEvent>().Count();

        // Assert
        DeclaredEventPublisher.Events.OfType<ShipmentLegRecordedEvent>()
            .ShouldContain(e => e.Origin == "Hanoi");
        DeclaredEventPublisher.Events.OfType<ShipmentLegRecordedEvent>()
            .ShouldContain(e => e.Origin == "Da Nang");
        firstRaiseCount.ShouldBe(1);
        secondRaiseCount.ShouldBe(2);
    }

    [Fact]
    public async Task DeclaredPayload_HonoursPropertyExclusions()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var customer = new Customer("Acme Pte Ltd", "GST-123456", "verify");

        // Act
        db.Set<Customer>().Add(customer);
        await db.SaveChangesAsync();

        // Assert
        var published = DeclaredEventPublisher.Events.OfType<CustomerCreatedEvent>().ToList();
        published.ShouldNotBeEmpty();
        typeof(CustomerCreatedEvent).GetProperty("TaxIdentifier").ShouldBeNull();
        published.ShouldContain(e => e.Name == "Acme Pte Ltd");
    }

    [Fact]
    public async Task DeclaredPayload_CarriesOnlyPropertiesAnInclusionListNames()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var vendor = new Vendor("Acme Pte Ltd", "VN-001", "verify");

        // Act
        db.Set<Vendor>().Add(vendor);
        await db.SaveChangesAsync();

        // Assert
        var published = DeclaredEventPublisher.Events.OfType<VendorCreatedEvent>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.Name == "Acme Pte Ltd");
        var properties = typeof(VendorCreatedEvent).GetProperties();
        properties.Select(p => p.Name).ShouldBe(["Name"]);
    }

    [Fact]
    public async Task DeclaredPayload_DropsPropertiesPointingAtOtherEntitiesByDefault()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var order = new Order("Acme Pte Ltd", "Pending", "Handle with care", "verify");

        // Act
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();

        // Assert
        var published = DeclaredEventPublisher.Events.OfType<OrderCreatedEvent>().ToList();
        published.ShouldNotBeEmpty();
        typeof(OrderCreatedEvent).GetProperty("Contact").ShouldBeNull();
        published.ShouldContain(e => e.CompanyName == "Acme Pte Ltd" && e.Status == "Pending");
    }

    [Fact]
    public async Task EntityDeclaringSeveralDistinctEvents_PublishesPerOperation()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");

        // Act
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        var createRaise = DeclaredEventPublisher.Events.OfType<OrderCreatedEvent>().ToList();

        order.ChangeStatus("Shipped");
        await db.SaveChangesAsync();
        var statusRaise = DeclaredEventPublisher.Events.OfType<OrderStatusChangedEvent>().ToList();

        // Assert
        createRaise.ShouldNotBeEmpty();
        statusRaise.ShouldNotBeEmpty();
        statusRaise.ShouldContain(e => e.Status == "Shipped");
    }

    [Fact]
    public async Task DeclaredAndHandRaisedEvents_BothReachSubscribers()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var shipment = new Shipment("Pending", "Vietnam Post", "verify");
        db.Set<Shipment>().Add(shipment);
        await db.SaveChangesAsync();
        DeclaredEventPublisher.Events.Clear();

        // Act
        shipment.Ship("Shipped", "Ninja Van");
        await db.SaveChangesAsync();

        // Assert
        DeclaredEventPublisher.Events.OfType<ShipmentStatusChangedEvent>().ShouldNotBeEmpty();
        var handRaised = DeclaredEventPublisher.Events.OfType<ShipmentStatusNotifiedEvent>().ToList();
        handRaised.ShouldNotBeEmpty();
        handRaised.ShouldContain(e => e.ShipmentId == shipment.Id && e.Status == "Shipped");
    }

    [Fact]
    public async Task NonAggregateRootMappedEntity_MayDeclareEvents()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var leg = new ShipmentLeg(3, "Hanoi", "Vinh", "verify");

        // Act
        db.Set<ShipmentLeg>().Add(leg);
        await db.SaveChangesAsync();

        // Assert
        DeclaredEventPublisher.Events.OfType<ShipmentLegPlacedEvent>()
            .ShouldContain(e => e.LegNumber == 3 && e.Origin == "Hanoi" && e.Destination == "Vinh");
    }

    [Fact]
    public async Task DeclaredEvents_AreNotPublished_WhenSaveFails()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var invalid = new Order("Acme Pte Ltd", string.Empty, string.Empty, "verify");

        // Act - the Status CHECK constraint rejects the empty value, so the save fails
        db.Set<Order>().Add(invalid);
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        DeclaredEventPublisher.Events.ShouldBeEmpty();

        // The failed entity stays tracked as Added; drop it so the shared context stays clean for later tests.
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task NestedOwnedValueChange_DoesNotRaiseOwnerUpdateEvent()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var customer = new Customer("Acme Pte Ltd", "GST-123456", "verify");
        customer.Address.Line = "1 Main Street";
        db.Set<Customer>().Add(customer);
        await db.SaveChangesAsync();
        DeclaredEventPublisher.Events.Clear();

        // Act
        customer.UpdateAddressLine("2 Main Street");
        await db.SaveChangesAsync();

        // Assert
        DeclaredEventPublisher.Events.OfType<CustomerUpdatedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public async Task EntityWithNoDeclarations_KeepsPublishingOnlyHandRaisedEvents()
    {
        // Arrange
        DeclaredEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var product = new Product("Widget", "verify");
        db.Set<Product>().Add(product);
        product.FlagCreated("verify");
        await db.SaveChangesAsync();

        var created = DeclaredEventPublisher.Events.OfType<ProductLifecycleEvent>().Where(e => e.Operation == "Created").ToList();
        var totalBeforeUpdate = DeclaredEventPublisher.Events.Count;

        product.Rename("Widget Pro");
        product.FlagUpdated("verify");
        await db.SaveChangesAsync();
        var updated = DeclaredEventPublisher.Events.OfType<ProductLifecycleEvent>().Where(e => e.Operation == "Updated").ToList();
        var totalAfterUpdate = DeclaredEventPublisher.Events.Count;

        db.Set<Product>().Remove(product);
        product.FlagDeleted("verify");
        await db.SaveChangesAsync();
        var deleted = DeclaredEventPublisher.Events.OfType<ProductLifecycleEvent>().Where(e => e.Operation == "Deleted").ToList();

        // Assert - exactly the hand-raised events, nothing extra, in the expected shape
        created.ShouldNotBeEmpty();
        updated.ShouldNotBeEmpty();
        deleted.ShouldNotBeEmpty();
        totalBeforeUpdate.ShouldBe(1);
        totalAfterUpdate.ShouldBe(2);
        DeclaredEventPublisher.Events.OfType<OrderCreatedEvent>().ShouldBeEmpty();

        var delivered = DeclaredEventPublisher.Events.OfType<ProductLifecycleEvent>().ToList();
        delivered.ShouldContain(e => e.ProductId == product.Id && e.Operation == "Created" && e.Name == "Widget");
        delivered.ShouldContain(e => e.ProductId == product.Id && e.Operation == "Updated" && e.Name == "Widget Pro");
    }

    #endregion
}