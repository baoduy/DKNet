using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Events;
using DKNet.EfCore.Events.Internals;
using DKNet.EfCore.Extensions.Snapshots;

namespace EfCore.Events.Tests.EventMapperNullTests;

/// <summary>
///     Unit tests for <see cref="EventContext" /> constructed with a null mapper.
/// </summary>
public class EventContextMapperNullTests(EventRunnerFixture fixture) : IClassFixture<EventRunnerFixture>
{
    #region Methods

    [Fact]
    public void GetEvents_WithTypeBasedEventAndNullMapper_ThrowsEventExceptionNamingImapper()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var root = new Root("Type-Based Root", "TestOwner");
        root.AddEvent<EntityAddedEvent>();
        db.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(db);
        snapshot.Initialize();

        var eventContext = new EventContext(snapshot, null);

        // Act - force enumeration so the guard fires
        var exception = Should.Throw<EventException>(() => eventContext.GetEvents().ToList());

        // Assert - EventException, not NullReferenceException; message names IMapper
        exception.Message.ShouldContain("IMapper");
    }

    [Fact]
    public void GetEvents_WithDirectEventAndNullMapper_ReturnsDirectEventWithoutThrowing()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var root = new Root("Direct Root", "TestOwner");
        var directEvent = new EntityAddedEvent { Id = root.Id, Name = root.Name };
        root.AddEvent(directEvent);
        db.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(db);
        snapshot.Initialize();

        var eventContext = new EventContext(snapshot, null);

        // Act
        var events = eventContext.GetEvents().Cast<EntityAddedEvent>().ToList();

        // Assert - direct event dispatched without a mapper
        events.ShouldHaveSingleItem();
        events[0].Id.ShouldBe(root.Id);
        events[0].Name.ShouldBe(root.Name);
    }

    #endregion
}