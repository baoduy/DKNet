namespace EfCore.Events.Tests.EventPublisherTests;

public class TestEventPublisherTests(EvenPublisherFixture provider) : IClassFixture<EvenPublisherFixture>
{
    #region Methods

    [Fact]
    public async Task AfterSaveEventTestAsync()
    {
        TestEventPublisher.Events.Clear();
        await provider.EnsureSqlReadyAsync();

        var db = provider.Provider.GetRequiredService<DddContext>();

        var p = new Root("P1", "Steven");
        p.SetOwnedBy("Steven");
        p.AddEvent<EntityAddedEvent>();

        db.Add(p);
        await db.SaveChangesAsync();

        TestEventPublisher.Events.ShouldNotBeEmpty();
        TestEventPublisher.Events.Any(e => e is EntityAddedEvent).ShouldBeTrue();
    }

    #endregion
}