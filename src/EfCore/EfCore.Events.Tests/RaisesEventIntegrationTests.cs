namespace EfCore.Events.Tests;

/// <summary>
///     DRK-450 acceptance criteria — <c>@integration</c> scenarios for the two-declaration domain event
///     raise rules ( <c>[GenerateDto]</c> payload + <c>[RaisesEvent]</c> rule). Every scenario runs
///     against a real (SQLite) save pipeline driven by <c>DddContext</c>; declared events are raised by
///     <c>EventHook</c> reading <c>[RaisesEvent]</c> via reflection, no generated code of its own.
/// </summary>
public class RaisesEventIntegrationTests(RaisesEventFixture fixture) : IClassFixture<RaisesEventFixture>
{
    #region Methods

    [Fact]
    public async Task CreateRule_RaisesEvent_WhenEntityPersistedForFirstTime()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");

        // Act
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();

        // Assert
        var published = RaisesEventPublisher.Events.OfType<OrderPlacedEvent>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.CompanyName == "Acme Pte Ltd" && e.Status == "Pending");
    }

    [Fact]
    public async Task NarrowedUpdateRule_Raises_OnlyForTheNamedProperty()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        RaisesEventPublisher.Events.Clear();

        // Act
        order.ChangeStatus("Shipped");
        await db.SaveChangesAsync();

        // Assert
        var published = RaisesEventPublisher.Events.OfType<OrderStatusChangedEvent>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.Status == "Shipped");
    }

    [Fact]
    public async Task NarrowedUpdateRule_StaysSilent_WhenOnlyOtherPropertiesChange()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", "Handle with care", "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        RaisesEventPublisher.Events.Clear();

        // Act
        order.UpdateDeliveryNote("Leave at reception");
        await db.SaveChangesAsync();

        // Assert
        RaisesEventPublisher.Events.OfType<OrderStatusChangedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public async Task UnnarrowedUpdateRule_Raises_ForAnyChangeToTheEntity()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        RaisesEventPublisher.Events.Clear();

        // Act
        order.UpdateDeliveryNote("Leave at reception");
        await db.SaveChangesAsync();

        // Assert
        var published = RaisesEventPublisher.Events.OfType<OrderChangedEvent>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.DeliveryNote == "Leave at reception");
    }

    [Fact]
    public async Task DeleteRule_CarriesTheValues_TheEntityHeldBeforeRemoval()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Shipped", string.Empty, "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        RaisesEventPublisher.Events.Clear();

        // Act
        db.Set<Order>().Remove(order);
        await db.SaveChangesAsync();

        // Assert
        var published = RaisesEventPublisher.Events.OfType<OrderCancelledEvent>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.CompanyName == "Acme Pte Ltd" && e.Status == "Shipped");
    }

    [Fact]
    public async Task RuleCoveringTwoOperations_RaisesOnBoth()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");

        // Act - first save (create)
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        var firstSaveCount = RaisesEventPublisher.Events.OfType<OrderRecordedEvent>().Count();

        // Act - second save (update)
        order.UpdateDeliveryNote("Leave at reception");
        await db.SaveChangesAsync();
        var secondSaveCount = RaisesEventPublisher.Events.OfType<OrderRecordedEvent>().Count();

        // Assert
        firstSaveCount.ShouldBe(1);
        secondSaveCount.ShouldBe(2);
    }

    [Fact]
    public async Task EntityWithSeveralDistinctRules_RaisesEachOnItsOwnOperation()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");

        // Act
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        var createRaise = RaisesEventPublisher.Events.OfType<OrderPlacedEvent>().ToList();

        order.ChangeStatus("Shipped");
        await db.SaveChangesAsync();
        var statusRaise = RaisesEventPublisher.Events.OfType<OrderStatusChangedEvent>().ToList();

        // Assert
        createRaise.ShouldNotBeEmpty();
        statusRaise.ShouldNotBeEmpty();
        statusRaise.ShouldContain(e => e.Status == "Shipped");
    }

    [Fact]
    public async Task PayloadExclusions_AreHonoured()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var customer = new Customer("Acme Pte Ltd", "billing@acme.example", "GST-123456", "verify");

        // Act
        db.Set<Customer>().Add(customer);
        await db.SaveChangesAsync();

        // Assert
        var published = RaisesEventPublisher.Events.OfType<CustomerRegisteredEvent>().ToList();
        published.ShouldNotBeEmpty();
        typeof(CustomerRegisteredEvent).GetProperty("TaxIdentifier").ShouldBeNull();
        published.ShouldContain(e => e.Name == "Acme Pte Ltd");
    }

    [Fact]
    public async Task TwoRulesNamingTheSamePayloadForTheSameOperation_RaiseItOnce()
    {
        // Arrange - Order carries [RaisesEvent(typeof(OrderPlacedEvent), Created)] twice
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");

        // Act
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();

        // Assert
        RaisesEventPublisher.Events.OfType<OrderPlacedEvent>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task RulesRaise_OnceTheApplicationRegistersPublishing()
    {
        // Arrange - the fixture wires [RaisesEvent] rules to the save pipeline the same way an
        // application registers event publishing today (AddEventPublisher + AddDbContextWithHook);
        // the entity/attribute compile cleanly with no direct reference to DKNet.EfCore.Events
        // (proven by the reference-only build unit test) and raise once that registration exists.
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");

        // Act
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();

        // Assert
        RaisesEventPublisher.Events.OfType<OrderPlacedEvent>().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DeclaredAndHandRaisedEvents_BothReachSubscribers()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        RaisesEventPublisher.Events.Clear();

        // Act - ChangeStatus mutates Status (narrowed declared rule) and hand-raises its own event
        order.ChangeStatus("Shipped");
        await db.SaveChangesAsync();

        // Assert
        RaisesEventPublisher.Events.OfType<OrderStatusChangedEvent>().ShouldNotBeEmpty();
        var handRaised = RaisesEventPublisher.Events.OfType<OrderStatusNotifiedEvent>().ToList();
        handRaised.ShouldNotBeEmpty();
        handRaised.ShouldContain(e => e.OrderId == order.Id && e.Status == "Shipped");
    }

    [Fact]
    public async Task NonAggregateRootMappedEntity_MayCarryARule()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var leg = new ShipmentLeg(1, "Hanoi", "Saigon", "verify");

        // Act
        db.Set<ShipmentLeg>().Add(leg);
        await db.SaveChangesAsync();

        // Assert
        RaisesEventPublisher.Events.OfType<ShipmentLegPlacedEvent>()
            .ShouldContain(e => e.LegNumber == 1 && e.Origin == "Hanoi" && e.Destination == "Saigon");
    }

    [Fact]
    public async Task DeclaredEvents_AreNotPublished_WhenSaveFails()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var invalid = new Order("Acme Pte Ltd", string.Empty, string.Empty, "verify");

        // Act - the Status CHECK constraint rejects the empty value, so the save fails
        db.Set<Order>().Add(invalid);
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        RaisesEventPublisher.Events.ShouldBeEmpty();

        // The failed entity stays tracked as Added; drop it so the shared context stays clean for later tests.
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task NestedOwnedValueChange_DoesNotRaiseTheOwnersUpdateEvent()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var customer = new Customer("Acme Pte Ltd", "billing@acme.example", "GST-123456", "verify");
        customer.Address.Line = "1 Main Street";
        db.Set<Customer>().Add(customer);
        await db.SaveChangesAsync();
        RaisesEventPublisher.Events.Clear();

        // Act
        customer.UpdateAddressLine("2 Main Street");
        await db.SaveChangesAsync();

        // Assert
        RaisesEventPublisher.Events.OfType<CustomerUpdatedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public async Task EntityWithNoRule_BehavesExactlyAsOnTheCurrentRelease()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var product = new Product("Widget", "verify");

        // Act
        db.Set<Product>().Add(product);
        product.FlagCreated("verify");
        await db.SaveChangesAsync();
        var totalAfterCreate = RaisesEventPublisher.Events.Count;

        product.Rename("Widget Pro");
        product.FlagUpdated("verify");
        await db.SaveChangesAsync();

        db.Set<Product>().Remove(product);
        product.FlagDeleted("verify");
        await db.SaveChangesAsync();

        // Assert - exactly the hand-raised events, nothing extra
        var delivered = RaisesEventPublisher.Events.OfType<ProductLifecycleEvent>().ToList();
        totalAfterCreate.ShouldBe(1);
        delivered.ShouldContain(e => e.ProductId == product.Id && e.Operation == "Created" && e.Name == "Widget");
        delivered.ShouldContain(e => e.ProductId == product.Id && e.Operation == "Updated" && e.Name == "Widget Pro");
        delivered.ShouldContain(e => e.ProductId == product.Id && e.Operation == "Deleted");
        RaisesEventPublisher.Events.Count.ShouldBe(delivered.Count);
    }

    // --- DRK-474: [RaisesEvent] string form + generated payload record ---------------------------------

    [Fact]
    public async Task StringFormRule_GeneratesItsRecordAndRaisesIt_WithNoHandWrittenPayload()
    {
        // Arrange - LoyaltyMembershipOtherEvents has no [GenerateDto] declaration anywhere; it exists
        // only because the string-form rule below told the build to generate it.
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var membership = new LoyaltyMembership(100, "Bronze", "verify");
        db.Set<LoyaltyMembership>().Add(membership);
        await db.SaveChangesAsync();
        RaisesEventPublisher.Events.Clear();

        // Act
        membership.ChangeTier("Silver");
        await db.SaveChangesAsync();

        // Assert
        var published = RaisesEventPublisher.Events.OfType<LoyaltyMembershipOtherEvents>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.Tier == "Silver" && e.Points == 100);
    }

    [Fact]
    public void GeneratedRecord_IsPublicAndPartial_InTheCarryingEntitysNamespace()
    {
        // Assert - the build placed LoyaltyMembershipOtherEvents in LoyaltyMembership's own namespace,
        // made it public, and it accepted the hand-authored Note member declared alongside the entity
        // (see the partial record stub in RaisesEventEntities.cs) — proof the record is partially declarable.
        var generatedType = typeof(LoyaltyMembershipOtherEvents);
        generatedType.IsPublic.ShouldBeTrue();
        generatedType.Namespace.ShouldBe(typeof(LoyaltyMembership).Namespace);
        generatedType.GetProperty("Note").ShouldNotBeNull();
        generatedType.GetProperty("Points").ShouldNotBeNull();
    }

    [Fact]
    public async Task BothForms_CoexistOnOneEntity_EachRaisingOnlyOnItsOwnSave()
    {
        // Arrange - LoyaltyMembership carries a type-naming rule (LoyaltyMembershipEvents, on
        // Points changes) and a string-form rule (LoyaltyMembershipOtherEvents, narrowed to Tier).
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var membership = new LoyaltyMembership(100, "Bronze", "verify");

        // Act - first save: create
        db.Set<LoyaltyMembership>().Add(membership);
        await db.SaveChangesAsync();
        var afterCreate = RaisesEventPublisher.Events.ToList();

        // Act - second save: only Tier changes
        RaisesEventPublisher.Events.Clear();
        membership.ChangeTier("Silver");
        await db.SaveChangesAsync();
        var afterTierChange = RaisesEventPublisher.Events.ToList();

        // Assert
        afterCreate.OfType<LoyaltyMembershipEvents>().ShouldContain(e => e.Points == 100);
        afterCreate.OfType<LoyaltyMembershipOtherEvents>().ShouldBeEmpty();
        afterTierChange.OfType<LoyaltyMembershipOtherEvents>().ShouldContain(e => e.Tier == "Silver");
        afterTierChange.OfType<LoyaltyMembershipEvents>().ShouldBeEmpty();
    }

    [Fact]
    public async Task TypeNamingRule_RaisesOnlyThePreExistingEvent_AcrossTwoSaves_UnaffectedByTheStringForm()
    {
        // Arrange - regression guard: a Points-only save must raise LoyaltyMembershipEvents exactly as
        // it did before the string form shipped, and never the unrelated string-form event (narrowed to
        // the untouched Tier property).
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var membership = new LoyaltyMembership(100, "Bronze", "verify");

        // Act - first save (create)
        db.Set<LoyaltyMembership>().Add(membership);
        await db.SaveChangesAsync();
        var afterCreate = RaisesEventPublisher.Events.ToList();

        // Act - second save (update, points only)
        RaisesEventPublisher.Events.Clear();
        membership.AddPoints(150);
        await db.SaveChangesAsync();
        var afterUpdate = RaisesEventPublisher.Events.ToList();

        // Assert
        afterCreate.OfType<LoyaltyMembershipEvents>().ShouldContain(e => e.Points == 100);
        afterCreate.Count.ShouldBe(1);
        afterUpdate.OfType<LoyaltyMembershipEvents>().ShouldContain(e => e.Points == 250);
        afterUpdate.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StringFormDefaultShape_CarriesTheEntitysDefaultValues()
    {
        // Arrange - CustomerTouched is generated with no shaping options: the default shape for Customer.
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var customer = new Customer("Acme Pte Ltd", "billing@acme.example", "T08-9911", "verify");

        // Act
        db.Set<Customer>().Add(customer);
        await db.SaveChangesAsync();

        // Assert
        var published = RaisesEventPublisher.Events.OfType<CustomerTouched>().ToList();
        published.ShouldContain(e =>
            e.Name == "Acme Pte Ltd" && e.Email == "billing@acme.example" && e.TaxIdentifier == "T08-9911");
    }

    [Fact]
    public async Task NarrowedStringFormUpdateRule_StaysSilent_WhenTheNarrowedPropertyDoesNotChange()
    {
        // Arrange - the string-form OrderStatusChanged rule is narrowed to Status, exactly like the
        // pre-existing type-form OrderStatusChangedEvent rule on the same entity.
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        RaisesEventPublisher.Events.Clear();

        // Act - only DeliveryNote changes
        order.UpdateDeliveryNote("Leave at reception");
        await db.SaveChangesAsync();

        // Assert
        RaisesEventPublisher.Events.OfType<OrderStatusChanged>().ShouldBeEmpty();
    }

    [Fact]
    public async Task NarrowedStringFormUpdateRule_Raises_WhenTheNarrowedPropertyChanges()
    {
        // Arrange
        RaisesEventPublisher.Events.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var order = new Order("Acme Pte Ltd", "Pending", string.Empty, "verify");
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        RaisesEventPublisher.Events.Clear();

        // Act
        order.ChangeStatus("Shipped");
        await db.SaveChangesAsync();

        // Assert
        var published = RaisesEventPublisher.Events.OfType<OrderStatusChanged>().ToList();
        published.ShouldNotBeEmpty();
        published.ShouldContain(e => e.Status == "Shipped");
    }

    #endregion
}
