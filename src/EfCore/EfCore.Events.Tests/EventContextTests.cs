using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Events.Internals;
using DKNet.EfCore.Extensions.Snapshots;
using MapsterMapper;

namespace EfCore.Events.Tests;

public class EventContextTests(EventRunnerFixture fixture) : IClassFixture<EventRunnerFixture>
{
    #region Methods

    [Fact]
    public void GetEvents_CalledTwiceOverSameCachedEventInstance_OverwritesSourceTypeWithoutThrowing()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var mapper = fixture.Provider.GetRequiredService<IMapper>();

        var root = new Root("Idempotent Root", "TestOwner");
        root.AddEvent(new IdempotentTestEvent { Id = root.Id });
        db.Set<Root>().Add(root);

        using var snapshot = new SnapshotContext(db);
        snapshot.Initialize();

        var eventContext = new EventContext(snapshot, mapper);

        // Act - first pass tags AdditionalData with sourceType (existing behaviour)
        var firstPass = eventContext.GetEvents().Cast<IdempotentTestEvent>().ToList();

        // Assert
        firstPass.ShouldHaveSingleItem();
        firstPass[0].AdditionalData["sourceType"].ShouldBe(typeof(Root).FullName);

        // Act - second pass reprocesses the SAME IEventItem instance: EventHook only calls
        // ClearEvents() after every publisher succeeds, so a failed publish leaves the entity's
        // events queued and GetEvents() runs again over the already-tagged instance on retry.
        var secondPass = Should.NotThrow(() => eventContext.GetEvents().Cast<IdempotentTestEvent>().ToList());

        // Assert - overwritten, not duplicated, and the same instance as the first pass
        secondPass.ShouldHaveSingleItem();
        secondPass[0].ShouldBeSameAs(firstPass[0]);
        secondPass[0].AdditionalData["sourceType"].ShouldBe(typeof(Root).FullName);
        secondPass[0].AdditionalData.Count.ShouldBe(1);

        db.Set<Root>().Remove(root);
    }

    #endregion
}

public sealed record IdempotentTestEvent : EventItem
{
    #region Properties

    public required Guid Id { get; init; }

    #endregion
}
