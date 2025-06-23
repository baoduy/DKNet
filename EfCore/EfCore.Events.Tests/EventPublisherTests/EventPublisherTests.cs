namespace EfCore.Events.Tests.EventPublisherTests;

public class TestEventPublisherTests(EvenPublisherFixture provider) : IClassFixture<EvenPublisherFixture>
{
    [Fact]
    public async Task AfterSaveTestAsync()
    {
        TestEventPublisher.Events.Clear();

        var db = provider.Provider.GetRequiredService<DddContext>();

        var p = new Root("P1");
        p.AddEntity("A1");

        db.Add(p);
        await db.SaveChangesAsync();

        TestEventPublisher.Events.Count.ShouldBeGreaterThan(0);
        TestEventPublisher.Events.Any(e=> e is EntityAddedEvent).ShouldBeTrue();
    }

    [Fact]
    public async Task AfterSaveEventTypeTestAsync()
    {
        TestEventPublisher.Events.Clear();

        var db = provider.Provider.GetRequiredService<DddContext>();

        var p = new Root("P1");
        p.AddEvent<TypeEvent>();

        db.Add(p);
        await db.SaveChangesAsync();

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