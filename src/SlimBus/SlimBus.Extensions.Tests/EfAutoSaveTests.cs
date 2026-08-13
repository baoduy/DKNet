using IMessageBus = SlimMessageBus.IMessageBus;

namespace SlimBus.Extensions.Tests;

public class EfAutoSaveTests(Fixture fixture) : IClassFixture<Fixture>
{
    #region Methods

    [Fact]
    public async Task OnHandle_WriteRequestWithResponse_ShouldAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestRequest { Name = "HBD" });

        rs.IsSuccess.ShouldBeTrue();
        rs.Value.ShouldNotBe(Guid.Empty);
        TestDbContext.Called.ShouldBeTrue();
    }

    [Fact]
    public async Task OnHandle_WriteRequestWithoutResponse_ShouldAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestNoResponseRequest { Name = "HBD" });

        rs.IsSuccess.ShouldBeTrue();
        TestDbContext.Called.ShouldBeTrue();
    }

    [Fact]
    public async Task OnHandle_Query_ShouldNotAutoSave()
    {
        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var id = (await m.Send(new TestRequest { Name = "HBD" })).Value;

        TestDbContext.Called = false;
        var rs = await m.Send(new TestQuery { Id = id });

        rs.ShouldNotBeNull();
        TestDbContext.Called.ShouldBeFalse();
    }

    [Fact]
    public async Task OnHandle_QueryReturningNull_ShouldNotAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestQuery { Id = Guid.NewGuid() });

        rs.ShouldBeNull();
        TestDbContext.Called.ShouldBeFalse();
    }

    [Fact]
    public async Task OnHandle_PagedQuery_ShouldNotAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestPageQuery { Id = Guid.NewGuid() });

        rs.ShouldNotBeNull();
        TestDbContext.Called.ShouldBeFalse();
    }

    [Fact]
    public async Task OnHandle_RawRequest_ShouldNotAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        await m.Send(new TestRawRequest { Name = "HBD" });

        TestDbContext.Called.ShouldBeFalse();
    }

    [Fact]
    public async Task OnHandle_FailedResult_ShouldNotAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestFailRequest { Name = "HBD" });

        rs.IsFailed.ShouldBeTrue();
        TestDbContext.Called.ShouldBeFalse();
    }

    #endregion
}
