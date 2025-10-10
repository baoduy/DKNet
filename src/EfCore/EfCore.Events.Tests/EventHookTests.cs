using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Events.Internals;
using DKNet.EfCore.Extensions.Snapshots;
using Mapster;
using MapsterMapper;

namespace EfCore.Events.Tests;

public class EventHookTests
{
    [Fact]
    public async Task RunBeforeSaveAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var eventPublishers = new List<IEventPublisher> { new TestEventPublisher() };
        var mappers = new List<IMapper>();

        var eventHook = new EventHook(eventPublishers, mappers);

        await using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, null);

        await using var snapshot = new SnapshotContext(context);

        // Act & Assert
        await Should.NotThrowAsync(async () =>
            await eventHook.BeforeSaveAsync(snapshot, CancellationToken.None));
    }

    [Fact]
    public async Task RunAfterSaveAsync_WithNoEventEntities_ShouldCompleteWithoutError()
    {
        // Arrange
        var testPublisher = new TestEventPublisher();
        var eventPublishers = new List<IEventPublisher> { testPublisher };
        var mappers = new List<IMapper>();

        var eventHook = new EventHook(eventPublishers, mappers);

        await using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, null);

        await using var snapshot = new SnapshotContext(context);

        TestEventPublisher.Events.Clear();

        // Act
        await eventHook.AfterSaveAsync(snapshot, CancellationToken.None);

        // Assert
        TestEventPublisher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAfterSaveAsync_WithEventEntities_ShouldPublishEvents()
    {
        // Arrange
        var testPublisher = new TestEventPublisher();
        var eventPublishers = new List<IEventPublisher> { testPublisher };
        var mappers = new List<IMapper>();

        var eventHook = new EventHook(eventPublishers, mappers);

        await using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseAutoConfigModel()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        var testEvent = new EntityAddedEvent { Id = root.Id, Name = root.Name };
        root.AddEvent(testEvent);
        context.Set<Root>().Add(root);

        await using var snapshot = new SnapshotContext(context);

        TestEventPublisher.Events.Clear();

        // Act
        await eventHook.AfterSaveAsync(snapshot, CancellationToken.None);

        // Assert
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBe(1);
        TestEventPublisher.Events[0].ShouldBeOfType<EntityAddedEvent>();

        var publishedEvent = (EntityAddedEvent)TestEventPublisher.Events[0];
        publishedEvent.Id.ShouldBe(testEvent.Id);
        publishedEvent.Name.ShouldBe(testEvent.Name);
    }

    [Fact]
    public async Task RunAfterSaveAsync_WithMultiplePublishers_ShouldPublishToAllPublishers()
    {
        // Arrange
        var testPublisher1 = new TestEventPublisher();
        var testPublisher2 = new TestEventPublisher();
        var eventPublishers = new List<IEventPublisher> { testPublisher1, testPublisher2 };
        var mappers = new List<IMapper>();

        var eventHook = new EventHook(eventPublishers, mappers);

        await using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseAutoConfigModel()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        var testEvent = new EntityAddedEvent { Id = root.Id, Name = root.Name };
        root.AddEvent(testEvent);
        context.Set<Root>().Add(root);

        await using var snapshot = new SnapshotContext(context);

        TestEventPublisher.Events.Clear();

        // Act
        await eventHook.AfterSaveAsync(snapshot, CancellationToken.None);

        // Assert
        // Note: Both publishers share the same static Events collection in TestEventPublisher
        // So we should see the event published twice (once per publisher)
        TestEventPublisher.Events.Count.ShouldBe(2);
        TestEventPublisher.Events.ShouldAllBe(e => e is EntityAddedEvent);
    }

    [Fact]
    public async Task RunAfterSaveAsync_WithMapper_ShouldMapEventTypes()
    {
        // Arrange
        var testPublisher = new TestEventPublisher();
        var eventPublishers = new List<IEventPublisher> { testPublisher };

        var config = TypeAdapterConfig.GlobalSettings;
        config.NewConfig<Root, EntityAddedEvent>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Name, src => src.Name);

        var mapper = new ServiceMapper(null, config);
        var mappers = new List<IMapper> { mapper };

        var eventHook = new EventHook(eventPublishers, mappers);

        await using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseAutoConfigModel()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        root.AddEvent<EntityAddedEvent>(); // Add event type to be mapped
        context.Set<Root>().Add(root);

        await using var snapshot = new SnapshotContext(context);

        TestEventPublisher.Events.Clear();

        // Act
        await eventHook.AfterSaveAsync(snapshot, CancellationToken.None);

        // Assert
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBe(1);
        TestEventPublisher.Events[0].ShouldBeOfType<EntityAddedEvent>();

        var mappedEvent = (EntityAddedEvent)TestEventPublisher.Events[0];
        mappedEvent.Id.ShouldBe(root.Id);
        mappedEvent.Name.ShouldBe(root.Name);
    }

    [Fact]
    public async Task RunAfterSaveAsync_WithMultipleEventEntities_ShouldPublishAllEvents()
    {
        // Arrange
        var testPublisher = new TestEventPublisher();
        var eventPublishers = new List<IEventPublisher> { testPublisher };
        var mappers = new List<IMapper>();

        var eventHook = new EventHook(eventPublishers, mappers);

        await using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseAutoConfigModel()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, null);

        var root1 = new Root("Root 1", "TestOwner");
        var root2 = new Root("Root 2", "TestOwner");

        root1.AddEvent(new EntityAddedEvent { Id = root1.Id, Name = root1.Name });
        root2.AddEvent(new EntityAddedEvent { Id = root2.Id, Name = root2.Name });

        context.Set<Root>().AddRange(root1, root2);

        await using var snapshot = new SnapshotContext(context);

        TestEventPublisher.Events.Clear();

        // Act
        await eventHook.AfterSaveAsync(snapshot, CancellationToken.None);

        // Assert
        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Count.ShouldBe(2);
        TestEventPublisher.Events.ShouldAllBe(e => e is EntityAddedEvent);

        var eventIds = TestEventPublisher.Events.Cast<EntityAddedEvent>().Select(e => e.Id).ToList();
        eventIds.ShouldContain(root1.Id);
        eventIds.ShouldContain(root2.Id);
    }

    [Fact]
    public async Task RunAfterSaveAsync_WithNoMappers_ShouldHandleGracefully()
    {
        // Arrange
        var testPublisher = new TestEventPublisher();
        var eventPublishers = new List<IEventPublisher> { testPublisher };
        var mappers = new List<IMapper>(); // No mappers

        var eventHook = new EventHook(eventPublishers, mappers);

        await using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseAutoConfigModel()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, null);

        var root = new Root("Test Root", "TestOwner");
        root.AddEvent<EntityAddedEvent>(); // Add event type (requires mapper)
        context.Set<Root>().Add(root);

        await using var snapshot = new SnapshotContext(context);

        TestEventPublisher.Events.Clear();

        // Act & Assert
        await Should.NotThrowAsync(async () =>
            await eventHook.AfterSaveAsync(snapshot, CancellationToken.None));

        // Should complete without error, but no events should be published
        // since event types need mapper but none provided
        TestEventPublisher.Events.ShouldBeEmpty();
    }
}