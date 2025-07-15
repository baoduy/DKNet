using SlimBus.App.Tests.Data;
using SlimBus.AppServices.Extensions.LazyMapper;

namespace SlimBus.App.Tests.Unit.LazyMapper;

public class LazyResultTests(LazyMapFixture fixture) : IClassFixture<LazyMapFixture>
{
    [Fact]
    public void LazyMapTest()
    {
        var mapper = fixture.ServiceProvider.GetRequiredService<IMapper>();
        var m = new TestDataModel("Steven");
        var v = mapper.ResultOf<View>(m);

        v.ValueOrDefault.ShouldNotBeNull();
        //v.ValueOrDefault.ShouldNotBe(m);
        v.ValueOrDefault!.Id.ShouldBe(m.Id);
        v.ValueOrDefault!.Name.ShouldBe(m.Name);
    }

    [Fact]
    public void LazyMapTheSameTypeTest()
    {
        var mapper = fixture.ServiceProvider.GetRequiredService<IMapper>();
        var m = new TestDataModel("Steven");
        var v = mapper.ResultOf<TestDataModel>(m);

        v.ValueOrDefault.ShouldNotBeNull();
        v.ValueOrDefault.ShouldBe(m);
    }

    [Fact]
    public void LazyMapNullValueTest()
    {
        var mapper = fixture.ServiceProvider.GetRequiredService<IMapper>();
        var v = () => mapper.ResultOf<TestDataModel>(null!);
        v.ShouldNotThrow();
    }
}