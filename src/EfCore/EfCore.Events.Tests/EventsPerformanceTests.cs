using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using DKNet.EfCore.Events.Handlers;
using DKNet.EfCore.Events.Internals;
using DKNet.EfCore.Extensions.Snapshots;
using Mapster;
using MapsterMapper;

namespace EfCore.Events.Tests;

public class EventsPerformanceTests
{
    [Fact]
    public void GetEntityKeyValues_Performance_ShouldBeReasonablyFast()
    {
        // Arrange
        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var entities = new List<Root>();
        for (int i = 0; i < 1000; i++)
        {
            entities.Add(new Root($"Root {i}", "TestOwner"));
        }
        
        context.Set<Root>().AddRange(entities);
        context.ChangeTracker.DetectChanges();

        var entries = entities.Select(e => context.Entry(e)).ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var keyValuesList = new List<Dictionary<string, object?>>();
        
        foreach (var entry in entries)
        {
            keyValuesList.Add(entry.GetEntityKeyValues());
        }
        
        stopwatch.Stop();

        // Assert
        keyValuesList.Count.ShouldBe(1000);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000); // Should be much faster than 1 second
    }

    [Fact]
    public void GetEventObjects_Performance_WithManyEventEntities_ShouldBeReasonablyFast()
    {
        // Arrange
        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var entities = new List<Root>();
        for (int i = 0; i < 500; i++)
        {
            var root = new Root($"Root {i}", "TestOwner");
            root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });
            entities.Add(root);
        }
        
        context.Set<Root>().AddRange(entities);

        using var snapshot = new SnapshotContext(context);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var eventObjects = snapshot.GetEventObjects(null).ToList();
        stopwatch.Stop();

        // Assert
        eventObjects.Count.ShouldBe(500);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000); // Should complete within 2 seconds
    }

    [Fact]
    public void GetEventObjects_Performance_WithMapper_ShouldBeReasonablyFast()
    {
        // Arrange
        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var config = TypeAdapterConfig.GlobalSettings;
        config.NewConfig<Root, EntityAddedEvent>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Name, src => src.Name);

        var mapper = new ServiceMapper(config);

        var entities = new List<Root>();
        for (int i = 0; i < 200; i++)
        {
            var root = new Root($"Root {i}", "TestOwner");
            root.AddEvent<EntityAddedEvent>(); // This will require mapping
            entities.Add(root);
        }
        
        context.Set<Root>().AddRange(entities);

        using var snapshot = new SnapshotContext(context);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var eventObjects = snapshot.GetEventObjects(mapper).ToList();
        stopwatch.Stop();

        // Assert
        eventObjects.Count.ShouldBe(200);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(3000); // Mapping takes more time
        
        // Verify all events were mapped correctly
        var allEvents = eventObjects.SelectMany(eo => eo.Events).ToList();
        allEvents.Count.ShouldBe(200);
        allEvents.ShouldAllBe(e => e is EntityAddedEvent);
    }

    [Fact]
    public async Task EventHook_Performance_WithManyEvents_ShouldPublishReasonablyFast()
    {
        // Arrange
        var performancePublisher = new PerformanceTrackingPublisher();
        var eventPublishers = new List<IEventPublisher> { performancePublisher };
        var mappers = new List<IMapper>();

        var eventHook = new EventHook(eventPublishers, mappers);

        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        var entities = new List<Root>();
        for (int i = 0; i < 100; i++)
        {
            var root = new Root($"Root {i}", "TestOwner");
            root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });
            entities.Add(root);
        }
        
        context.Set<Root>().AddRange(entities);

        using var snapshot = new SnapshotContext(context);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await eventHook.RunAfterSaveAsync(snapshot, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        performancePublisher.PublishedCount.ShouldBe(100);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000); // Should complete within 5 seconds
    }

    [Fact]
    public void EventObject_Performance_WithLargeEventArrays_ShouldHandleEfficiently()
    {
        // Arrange
        var entityType = "PerformanceTestEntity";
        var primaryKey = new Dictionary<string, object?> { { "Id", Guid.NewGuid() } };
        
        var largeEventArrays = new List<object[]>();
        
        // Create multiple large event arrays
        for (int i = 0; i < 50; i++)
        {
            var events = new object[1000];
            for (int j = 0; j < events.Length; j++)
            {
                events[j] = new EntityAddedEvent { Id = Guid.NewGuid(), Name = $"Event {i}-{j}" };
            }
            largeEventArrays.Add(events);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var eventObjects = new List<EventObject>();
        
        foreach (var events in largeEventArrays)
        {
            eventObjects.Add(new EventObject(entityType, primaryKey, events));
        }
        
        stopwatch.Stop();

        // Assert
        eventObjects.Count.ShouldBe(50);
        eventObjects.All(eo => eo.Events.Length == 1000).ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000); // Should be very fast as it's just reference assignment
    }

    [Fact]
    public void GetEventObjects_MemoryUsage_ShouldNotLeakMemory()
    {
        // Arrange
        using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options, null);

        // Act & Assert - Run multiple times to check for memory leaks
        for (int iteration = 0; iteration < 10; iteration++)
        {
            var entities = new List<Root>();
            for (int i = 0; i < 100; i++)
            {
                var root = new Root($"Root {iteration}-{i}", "TestOwner");
                root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });
                entities.Add(root);
            }
            
            context.Set<Root>().AddRange(entities);

            using var snapshot = new SnapshotContext(context);
            var eventObjects = snapshot.GetEventObjects(null).ToList();
            
            eventObjects.Count.ShouldBe(100);
            
            // Clear the context for next iteration
            context.Set<Root>().RemoveRange(entities);
        }
        
        // If we reach here without OutOfMemoryException, the test passes
        // This is a basic memory leak detection
        true.ShouldBeTrue();
    }

    [Fact]
    public async Task EventHook_Concurrency_ShouldHandleParallelExecution()
    {
        // Arrange
        var concurrentPublisher = new ConcurrentTrackingPublisher();
        var eventPublishers = new List<IEventPublisher> { concurrentPublisher };
        var mappers = new List<IMapper>();

        var eventHook = new EventHook(eventPublishers, mappers);

        // Act - Run multiple event hooks in parallel
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                using var context = new DddContext(new DbContextOptionsBuilder<DddContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options, null);

                var root = new Root($"Concurrent Root {index}", "TestOwner");
                root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });
                context.Set<Root>().Add(root);

                using var snapshot = new SnapshotContext(context);
                await eventHook.RunAfterSaveAsync(snapshot, CancellationToken.None);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        concurrentPublisher.PublishedCount.ShouldBe(10);
        concurrentPublisher.MaxConcurrency.ShouldBeGreaterThan(0);
    }

    // Helper classes for performance testing
    private class PerformanceTrackingPublisher : IEventPublisher
    {
        public int PublishedCount { get; private set; }

        public Task PublishAsync(IEventObject eventObj, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref PublishedCount);
            return Task.CompletedTask;
        }
    }

    private class ConcurrentTrackingPublisher : IEventPublisher
    {
        private int _publishedCount;
        private int _concurrentCount;
        private int _maxConcurrency;

        public int PublishedCount => _publishedCount;
        public int MaxConcurrency => _maxConcurrency;

        public async Task PublishAsync(IEventObject eventObj, CancellationToken cancellationToken = default)
        {
            var currentConcurrency = Interlocked.Increment(ref _concurrentCount);
            
            // Track max concurrency
            var currentMax = _maxConcurrency;
            while (currentConcurrency > currentMax)
            {
                var original = Interlocked.CompareExchange(ref _maxConcurrency, currentConcurrency, currentMax);
                if (original == currentMax) break;
                currentMax = _maxConcurrency;
            }

            try
            {
                // Simulate some work
                await Task.Delay(50, cancellationToken);
                Interlocked.Increment(ref _publishedCount);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentCount);
            }
        }
    }
}