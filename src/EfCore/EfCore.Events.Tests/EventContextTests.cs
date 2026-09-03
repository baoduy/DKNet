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

    [Fact]
    public void GetEvents_AcrossMultipleEntities_YieldsEachEntitysOwnEventExactlyOnce()
    {
        // Arrange: two entities, each raising its own event. GetEvents() reuses a single HashSet across
        // entities (Clear()-ed between iterations) instead of allocating a fresh one per entity — if that
        // reuse ever forgot to clear, the first entity's event would still be sitting in the set and get
        // yielded a second time while iterating the second entity's set.
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var mapper = fixture.Provider.GetRequiredService<IMapper>();

        var root1 = new Root("Root One", "TestOwner");
        var root2 = new Root("Root Two", "TestOwner");
        root1.AddEvent(new IdempotentTestEvent { Id = root1.Id });
        root2.AddEvent(new IdempotentTestEvent { Id = root2.Id });
        db.Set<Root>().AddRange(root1, root2);

        using var snapshot = new SnapshotContext(db);
        snapshot.Initialize();
        var eventContext = new EventContext(snapshot, mapper);

        // Act
        var events = eventContext.GetEvents().Cast<IdempotentTestEvent>().ToList();

        // Assert: exactly one event per entity, no leak or duplication across iterations
        events.Count.ShouldBe(2);
        events.ShouldContain(e => e.Id == root1.Id);
        events.ShouldContain(e => e.Id == root2.Id);

        db.Set<Root>().RemoveRange(root1, root2);
    }

    #endregion
}

public sealed record IdempotentTestEvent : EventItem
{
    #region Properties

    public required Guid Id { get; init; }

    #endregion
}
