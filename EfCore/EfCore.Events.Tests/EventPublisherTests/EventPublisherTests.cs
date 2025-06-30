namespace EfCore.Events.Tests.EventPublisherTests;

public class TestEventPublisherTests(EvenPublisherFixture provider) : IClassFixture<EvenPublisherFixture>
{
    [Fact]
    public async Task AfterSaveTestAsync()
    {
        TestEventPublisher.Events.Clear();
        await provider.EnsureSqlReadyAsync();

        var db = provider.Provider.GetRequiredService<DddContext>();

        var p = new Root("P1","Steven");

        p.AddEntity("A1");

        db.Add(p);
        await db.SaveChangesAsync();

        // Wait for background processing to complete
        await Task.Delay(2000);

        TestEventPublisher.Events.Count.ShouldBeGreaterThan(0);
        TestEventPublisher.Events.Any(e=> e is EntityAddedEvent).ShouldBeTrue();
    }

    [Fact]
    public async Task AfterSaveEventTypeTestAsync()
    {
        TestEventPublisher.Events.Clear();
        await provider.EnsureSqlReadyAsync();

        var db = provider.Provider.GetRequiredService<DddContext>();

        var p = new Root("P1","Steven");
        p.SetOwnedBy("Steven");

        p.AddEvent<TypeEvent>();

        db.Add(p);
        await db.SaveChangesAsync();

        // Wait for background processing to complete
        await Task.Delay(2000);

        TestEventPublisher.Events.Any(e=> e is TypeEvent).ShouldBeTrue();
    }
    
    // [Fact]
    // public async Task BeforeSaveTestAsync()
    // {
    //     TestEventPublisher.Events.Clear();
    //
    //     var db = provider.Provider.GetRequiredService<DddContext>();
    //
    //     var p = new Root("P1");
    //     p.AddEntity("A1");
    //
    //     db.Add(p);
    //     await db.SaveChangesAsync();
    //
    //     TestEventPublisher.Events.Any(e=> e is EntityAddedEvent).ShouldBeTrue();
    // }
}