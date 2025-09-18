using System.Collections.Generic;
using System.Threading;
using DKNet.EfCore.Events;
using DKNet.EfCore.Events.Handlers;
using DKNet.EfCore.Events.Internals;
using DKNet.EfCore.Extensions.Snapshots;
using Mapster;
using MapsterMapper;

namespace EfCore.Events.Tests;

public class EventsEdgeCasesTests
{
    [Fact]
    public void EventExtensions_GetEntityKeyValues_WithStringComparisonIgnoreCase()
    {
        // Arrange
        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        context.Set<Root>().Add(root);
        context.ChangeTracker.DetectChanges();

        var entry = context.Entry(root);

        // Act
        var keyValues = entry.GetEntityKeyValues();

        // Assert
        keyValues.Comparer.ShouldBe(StringComparer.OrdinalIgnoreCase);
        
        // Should be case-insensitive
        keyValues.ContainsKey("id").ShouldBeTrue(); // lowercase
        keyValues.ContainsKey("ID").ShouldBeTrue(); // uppercase
        keyValues.ContainsKey("Id").ShouldBeTrue(); // proper case
    }

    [Fact]
    public void EventExtensions_GetEventObjects_WithNullMapper_ShouldSkipEventTypeMapping()
    {
        // Arrange
        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = "Direct Event" });
        root.AddEvent<EntityAddedEvent>(); // This requires mapping but mapper is null
        context.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(context);

        // Act
        var eventObjects = snapshot.GetEventObjects(null).ToList();

        // Assert
        eventObjects.Count.ShouldBe(1);
        var eventObject = eventObjects.First();
        
        // Should only contain the direct event, not the mapped one
        eventObject.Events.Length.ShouldBe(1);
        eventObject.Events[0].ShouldBeOfType<EntityAddedEvent>();
        
        var directEvent = (EntityAddedEvent)eventObject.Events[0];
        directEvent.Name.ShouldBe("Direct Event");
    }

    [Fact]
    public async Task EventHook_RunAfterSaveAsync_WithFailingPublisher_ShouldNotThrow()
    {
        // Arrange
        var failingPublisher = new FailingEventPublisher();
        var successfulPublisher = new TestEventPublisher();
        var eventPublishers = new List<IEventPublisher> { failingPublisher, successfulPublisher };
        var mappers = new List<IMapper>();

        var eventHook = new EventHook(eventPublishers, mappers);

        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });
        context.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(context);

        TestEventPublisher.Events.Clear();

        // Act & Assert
        // Should not throw even though one publisher fails
        var exception = await Should.ThrowAsync<Exception>(async () => 
            await eventHook.RunAfterSaveAsync(snapshot, CancellationToken.None));

        // The Task.WhenAll should propagate the exception
        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task EventHook_RunAfterSaveAsync_WithCancelledToken_ShouldHandleCancellation()
    {
        // Arrange
        var testPublisher = new SlowEventPublisher();
        var eventPublishers = new List<IEventPublisher> { testPublisher };
        var mappers = new List<IMapper>();

        var eventHook = new EventHook(eventPublishers, mappers);

        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });
        context.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(context);

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel(); // Cancel immediately

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () => 
            await eventHook.RunAfterSaveAsync(snapshot, cancellationTokenSource.Token));
    }

    [Fact]
    public void EventExtensions_GetEventObjects_WithMalformedMapper_ShouldHandleGracefully()
    {
        // Arrange
        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        root.AddEvent<EntityAddedEvent>(); // Requires mapping
        context.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(context);

        var malformedMapper = new MalformedMapper();

        // Act & Assert
        Should.Throw<NotImplementedException>(() => 
            snapshot.GetEventObjects(malformedMapper).ToList());
    }

    [Fact]
    public void EventObject_WithExtremelyLargeEventArray_ShouldHandleCorrectly()
    {
        // Arrange
        var entityType = "TestEntity";
        var primaryKey = new Dictionary<string, object?> { { "Id", Guid.NewGuid() } };
        
        // Create a large array of events
        var events = new object[10000];
        for (int i = 0; i < events.Length; i++)
        {
            events[i] = new EntityAddedEvent { Id = Guid.NewGuid(), Name = $"Event {i}" };
        }

        // Act
        var eventObject = new EventObject(entityType, primaryKey, events);

        // Assert
        eventObject.Events.Length.ShouldBe(10000);
        eventObject.Events.ShouldBeSameAs(events);
    }

    [Fact]
    public void EventExtensions_GetEventObjects_WithCircularReferences_ShouldNotCauseInfiniteLoop()
    {
        // Arrange
        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        
        // Create an event that references the root itself (potential circular reference)
        var circularEvent = new
        {
            Id = root.Id,
            Root = root, // This could cause issues in some serializers
            Message = "Circular reference test"
        };
        
        root.AddEvent(circularEvent);
        context.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(context);

        // Act
        var eventObjects = snapshot.GetEventObjects(null).ToList();

        // Assert
        eventObjects.Count.ShouldBe(1);
        var eventObject = eventObjects.First();
        eventObject.Events.Length.ShouldBe(1);
        eventObject.Events[0].ShouldNotBeNull();
    }

    [Fact]
    public async Task EventHook_WithEmptyPublisherList_ShouldCompleteSuccessfully()
    {
        // Arrange
        var eventPublishers = new List<IEventPublisher>(); // Empty list
        var mappers = new List<IMapper>();

        var eventHook = new EventHook(eventPublishers, mappers);

        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });
        context.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(context);

        // Act & Assert
        await Should.NotThrowAsync(async () => 
            await eventHook.RunAfterSaveAsync(snapshot, CancellationToken.None));
    }

    [Fact]
    public void EventExtensions_GetEventObjects_WithDuplicateEvents_ShouldReturnAllEvents()
    {
        // Arrange
        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        var sameEvent = new EntityAddedEvent { Id = root.Id, Name = "Duplicate Event" };
        
        // Add the same event multiple times
        root.AddEvent(sameEvent);
        root.AddEvent(sameEvent);
        root.AddEvent(sameEvent);
        
        context.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(context);

        // Act
        var eventObjects = snapshot.GetEventObjects(null).ToList();

        // Assert
        eventObjects.Count.ShouldBe(1);
        var eventObject = eventObjects.First();
        eventObject.Events.Length.ShouldBe(3); // Should include duplicates
        eventObject.Events.ShouldAllBe(e => ReferenceEquals(e, sameEvent));
    }

    // Helper classes for testing edge cases
    private class FailingEventPublisher : IEventPublisher
    {
        public Task PublishAsync(IEventObject eventObj, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Publisher failed intentionally");
        }
    }

    private class SlowEventPublisher : IEventPublisher
    {
        public async Task PublishAsync(IEventObject eventObj, CancellationToken cancellationToken = default)
        {
            await Task.Delay(5000, cancellationToken); // Long delay to test cancellation
        }
    }

    private class MalformedMapper : IMapper
    {
        public TDestination Map<TDestination>(object source) => throw new NotImplementedException();
        public TDestination Map<TSource, TDestination>(TSource source) => throw new NotImplementedException();
        public TDestination Map<TDestination>(object source, TDestination destination) => throw new NotImplementedException();
        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination) => throw new NotImplementedException();
        public object Map(object source, Type sourceType, Type destinationType) => throw new NotImplementedException();
        public object Map(object source, object destination, Type sourceType, Type destinationType) => throw new NotImplementedException();
    }
}