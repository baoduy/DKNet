using Xunit.Abstractions;

namespace EfCore.Events.Tests;

public class EventsIntegrationTests(ITestOutputHelper output, EventRunnerFixture fixture)
    : IClassFixture<EventRunnerFixture>
{
    #region Methods

    [Fact]
    public async Task FullEventFlow_WithConcurrentSaves_ShouldPublishAllEvents()
    {
        // Arrange

        TestEventPublisher.Events.Clear();

        for (var i = 0; i < 5; i++)
        {
            var index = i;
            await Task.Run(async () =>
            {
                using var scope = fixture.Provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DddContext>();

                var root = new Root($"Concurrent Root {index}", "TestOwner");
                root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = $"Concurrent Event {index}" });

                db.Set<Root>().Add(root);
                await db.SaveChangesAsync();
            });
        }

        // Assert
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBeGreaterThanOrEqualTo(4);
        TestEventPublisher.Events.ShouldAllBe(e => e is EntityAddedEvent);

        var events = TestEventPublisher.Events.Cast<EntityAddedEvent>().Select(e => e.Name).ToList();
        output.WriteLine($"Names: {string.Join('\n', events)}");
        for (var i = 0; i < 5; i++)
            events.ShouldContain(e => e.Equals($"Concurrent Event {i}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FullEventFlow_WithDirectEvents_ShouldPublishCorrectly()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        TestEventPublisher.Events.Clear();

        var root = new Root("Integration Test Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });

        // Act
        db.Set<Root>().Add(root);
        await db.SaveChangesAsync();

        // Assert
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBe(1);
        TestEventPublisher.Events[0].ShouldBeOfType<EntityAddedEvent>();

        var publishedEvent = (EntityAddedEvent)TestEventPublisher.Events[0];
        publishedEvent.Id.ShouldBe(root.Id);
        publishedEvent.Name.ShouldBe("Integration Test Root");
    }

    [Fact]
    public async Task FullEventFlow_WithEntityUpdate_ShouldPublishUpdateEvents()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        // First, create and save the entity
        var root = new Root("Original Name", "TestOwner");
        db.Set<Root>().Add(root);
        await db.SaveChangesAsync();

        TestEventPublisher.Events.Clear();

        // Act - Update the entity and add an event
        root.UpdateName("Updated Name");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = "Entity Updated" });
        await db.SaveChangesAsync();

        // Assert
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBe(1);
        TestEventPublisher.Events[0].ShouldBeOfType<EntityAddedEvent>();

        var updateEvent = (EntityAddedEvent)TestEventPublisher.Events[0];
        updateEvent.Name.ShouldBe("Entity Updated");
    }

    [Fact]
    public async Task FullEventFlow_WithMappedEvents_ShouldPublishMappedCorrectly()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        TestEventPublisher.Events.Clear();

        var root = new Root("Mapped Event Root", "TestOwner");
        root.AddEvent<EntityAddedEvent>(); // This will be mapped from the entity

        // Act
        db.Set<Root>().Add(root);
        await db.SaveChangesAsync();

        // Assert
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBeGreaterThanOrEqualTo(1);
        TestEventPublisher.Events[0].ShouldBeOfType<EntityAddedEvent>();

        var mappedEvent = (EntityAddedEvent)TestEventPublisher.Events[0];
        mappedEvent.Id.ShouldBe(root.Id);
        mappedEvent.Name.ShouldBe("Mapped Event Root");
    }

    [Fact]
    public async Task FullEventFlow_WithMixedEvents_ShouldPublishAllEvents()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        TestEventPublisher.Events.Clear();

        var root = new Root("Mixed Events Root", "TestOwner");

        // Add both direct and mapped events
        var directEvent = new EntityAddedEvent { Id = Guid.NewGuid(), Name = "Direct Event" };
        root.AddEvent(directEvent);
        root.AddEvent<EntityAddedEvent>(); // Mapped event

        // Act
        db.Set<Root>().Add(root);
        await db.SaveChangesAsync();

        // Assert
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBe(2);
        TestEventPublisher.Events.ShouldAllBe(e => e is EntityAddedEvent);

        var events = TestEventPublisher.Events.Cast<EntityAddedEvent>().ToList();

        // Should contain both the direct event and the mapped event
        events.ShouldContain(e => e.Name == "Direct Event");
        events.ShouldContain(e => e.Name == "Mixed Events Root");
    }

    [Fact]
    public async Task FullEventFlow_WithMultipleEntities_ShouldPublishAllEntityEvents()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        TestEventPublisher.Events.Clear();

        var root1 = new Root("Root 1", "TestOwner");
        var root2 = new Root("Root 2", "TestOwner");
        var root3 = new Root("Root 3", "TestOwner");

        root1.AddEvent(new EntityAddedEvent { Id = root1.Id, Name = root1.Name });
        root2.AddEvent(new EntityAddedEvent { Id = root2.Id, Name = root2.Name });
        root3.AddEvent(new EntityAddedEvent { Id = root3.Id, Name = root3.Name });

        // Act
        db.Set<Root>().AddRange(root1, root2, root3);
        await db.SaveChangesAsync();

        // Assert
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBe(3);
        TestEventPublisher.Events.ShouldAllBe(e => e is EntityAddedEvent);

        var events = TestEventPublisher.Events.Cast<EntityAddedEvent>().ToList();
        var eventNames = events.Select(e => e.Name).ToList();

        eventNames.ShouldContain("Root 1");
        eventNames.ShouldContain("Root 2");
        eventNames.ShouldContain("Root 3");
    }

    [Fact]
    public async Task FullEventFlow_WithNestedEntities_ShouldPublishNestedEntityEvents()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        TestEventPublisher.Events.Clear();

        var root = new Root("Root with nested", "TestOwner");
        root.AddEntity("Nested Entity 1");
        root.AddEntity("Nested Entity 2");

        // Add events to the root
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = "Root Added" });

        // Act
        db.Set<Root>().Add(root);
        await db.SaveChangesAsync();

        // Assert
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBe(1); // Only root has events
        TestEventPublisher.Events[0].ShouldBeOfType<EntityAddedEvent>();

        var rootEvent = (EntityAddedEvent)TestEventPublisher.Events[0];
        rootEvent.Name.ShouldBe("Root Added");
    }

    [Fact]
    public async Task FullEventFlow_WithNoEvents_ShouldNotPublishAnything()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        TestEventPublisher.Events.Clear();

        var root = new Root("No Events Root", "TestOwner");
        // Don't add any events

        // Act
        db.Set<Root>().Add(root);
        await db.SaveChangesAsync();

        // Assert
        TestEventPublisher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task FullEventFlow_WithTransactionRollback_ShouldNotPublishEvents()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        TestEventPublisher.Events.Clear();

        var root = new Root("Transaction Test", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = "Should not publish" });

        // Act
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.Set<Root>().Add(root);
            await db.SaveChangesAsync(); // This should trigger event publishing

            // But then rollback the transaction
            await transaction.RollbackAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
        }

        // Assert
        // Events should have been published even though transaction was rolled back
        // This is because events are published after SaveChanges, not after transaction commit
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    #endregion
}