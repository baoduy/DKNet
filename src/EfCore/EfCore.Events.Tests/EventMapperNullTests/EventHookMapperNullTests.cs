using DKNet.EfCore.Events;

namespace EfCore.Events.Tests.EventMapperNullTests;

/// <summary>
///     Full-flow tests proving EventHook dispatches with a null mapper (no IMapper registered):
///     type-based events throw <see cref="EventException" />, direct events still publish.
/// </summary>
public class EventHookMapperNullTests(EventMapperNullFixture fixture) : IClassFixture<EventMapperNullFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_WithTypeBasedEventAndNoMapper_ThrowsEventExceptionNamingImapper()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var root = new Root("Type-Based Hook Root", "TestOwner");
        root.AddEvent<EntityAddedEvent>();
        db.Set<Root>().Add(root);

        // Act - EventHook runs after save; GetEvents() must throw EventException, not NullReferenceException
        var exception = await Should.ThrowAsync<EventException>(() => db.SaveChangesAsync());

        // Assert - message names IMapper
        exception.Message.ShouldContain("IMapper");
    }

    [Fact]
    public async Task SaveChangesAsync_WithDirectEventAndNoMapper_PublishesEventWithoutThrowing()
    {
        // Arrange
        RecordingMapperNullPublisher.Published.Clear();
        var db = fixture.Provider.GetRequiredService<DddContext>();

        var root = new Root("Direct Hook Root", "TestOwner");
        root.AddEvent(new EntityAddedEvent { Id = root.Id, Name = root.Name });
        db.Set<Root>().Add(root);

        // Act - must not throw: direct events dispatch without a mapper
        await db.SaveChangesAsync();

        // Assert
        RecordingMapperNullPublisher.Published.OfType<EntityAddedEvent>()
            .Any(e => e.Id == root.Id).ShouldBeTrue();
    }

    #endregion
}