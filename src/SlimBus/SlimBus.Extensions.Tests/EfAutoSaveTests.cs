using IMessageBus = SlimMessageBus.IMessageBus;

namespace SlimBus.Extensions.Tests;

public class EfAutoSaveTests(Fixture fixture) : IClassFixture<Fixture>
{
    [Fact]
    public async Task SaveChangeShouldBeCalled()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestRequest { Name = "HBD" });

        rs.ShouldNotBe(Guid.Empty);
        TestDbContext.Called.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveChangeShouldNotBeCalled()
    {
        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var id = await m.Send(new TestRequest { Name = "HBD" });

        TestDbContext.Called = false;
        var rs = await m.Send(new TestQuery { Id = id });

        rs.ShouldNotBeNull();
        TestDbContext.Called.ShouldBeFalse();
    }
}