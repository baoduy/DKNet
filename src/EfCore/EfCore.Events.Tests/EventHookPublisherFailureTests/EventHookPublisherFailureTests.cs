using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Events.Internals;
using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.Extensions.Logging;

namespace EfCore.Events.Tests.EventHookPublisherFailureTests;

public class EventHookPublisherFailureTests(EventHookPublisherFailureFixture fixture)
    : IClassFixture<EventHookPublisherFailureFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_WithThrowingPublisher_DoesNotThrowAndPersists()
    {
        // Arrange
        RecordingEventPublisher.Published.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var root = new Root("Persisted Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });

        // Act
        db.Set<Root>().Add(root);
        await db.SaveChangesAsync();

        // Assert - save succeeded and entity persisted despite the failing first publisher
        db.Set<Root>().FirstOrDefault(r => r.Id == root.Id).ShouldNotBeNull();
    }

    [Fact]
    public async Task PublishAsync_FailingPublisherThenSecondPublisher_SecondStillReceivesEvents()
    {
        // Arrange
        RecordingEventPublisher.Published.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var root = new Root("Second Publisher Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });

        // Act
        db.Set<Root>().Add(root);
        await db.SaveChangesAsync();

        // Assert - the publisher registered after the failing one still receives the event
        RecordingEventPublisher.Published.ShouldNotBeEmpty();
        RecordingEventPublisher.Published.OfType<EntityAddedEvent>()
            .Any(e => e.Id == root.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task ClearEvents_AfterFailedPublish_SecondSavePublishesNothing()
    {
        // Arrange
        RecordingEventPublisher.Published.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var root = new Root("Clear Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });

        // Act - first save fails at the first publisher and clears pending events
        db.Set<Root>().Add(root);
        await db.SaveChangesAsync();

        // A second save on the same tracked entities must not republish the cleared events
        root.UpdateName("Renamed");
        await db.SaveChangesAsync();

        // Assert - the event was published exactly once
        RecordingEventPublisher.Published.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AfterSaveAsync_WithThrowingPublisher_LogsErrorWithPublisherAndContextId()
    {
        // Arrange
        RecordingEventPublisher.Published.Clear();
        fixture.LoggerProvider.Entries.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var root = new Root("Logged Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });

        // Act
        db.Set<Root>().Add(root);
        await db.SaveChangesAsync();

        // Assert - the failure is logged at error level with publisher type and context id
        (LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State)? entry =
            fixture.LoggerProvider.Entries.FirstOrDefault(e => e.Level == LogLevel.Error);
        entry.ShouldNotBeNull();
        entry.Value.Message.ShouldContain("EventHook");
        entry.Value.Message.ShouldContain(nameof(FailingEventPublisher));

        var state = entry.Value.State;
        state.ShouldContain(k => k.Key == "Publisher" && Equals(k.Value, nameof(FailingEventPublisher)));
        var contextId = state.First(k => k.Key == "ContextId").Value?.ToString();
        contextId.ShouldNotBeNullOrEmpty();
        contextId.ShouldBe(db.ContextId.ToString());
    }

    [Fact]
    public async Task AfterSaveAsync_WithNullLoggerAndThrowingPublisher_DoesNotThrow()
    {
        // Arrange
        RecordingEventPublisher.Published.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var root = new Root("Null Logger Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });
        db.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(db);
        snapshot.Initialize();

        var hook = new EventHook(
            new IEventPublisher[] { new FailingEventPublisher(), new RecordingEventPublisher() },
            [],
            null);

        // Act & Assert - failure swallowed even when no logger is wired
        await hook.AfterSaveAsync(snapshot);
        RecordingEventPublisher.Published.Count.ShouldBe(1);
    }

    #endregion
}