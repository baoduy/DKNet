using Mapster;
using DKNet.SlimBus.Extensions.LazyMapper;

namespace SlimBus.Extensions.Tests;

public class LazyMapperTests
{
    #region Methods

    private static IMapper NewMapper() => new Mapper(new TypeAdapterConfig());

    [Fact]
    public void LazyMap_WithDifferentType_MapsViaMapster()
        => NewMapper().LazyMap<Target>(new Source("a")).Value.Name.ShouldBe("a");

    [Fact]
    public void LazyMap_WithSameType_ReturnsSameInstance()
    {
        var s = new Source("a");
        NewMapper().LazyMap<Source>(s).Value.ShouldBeSameAs(s);
    }

    [Fact]
    public void LazyMap_WithNullValue_ValueThrows_AndValueOrDefaultIsNull()
    {
        var lazy = NewMapper().LazyMap<Target>(null!);
        lazy.ValueOrDefault.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => _ = lazy.Value);
    }

    [Fact]
    public void ResultOf_WithValue_IsSuccessAndMapsValue()
    {
        var rs = NewMapper().ResultOf<Target>(new Source("a"));
        rs.IsSuccess.ShouldBeTrue();
        rs.Value.Name.ShouldBe("a");
    }

    #endregion

    private sealed record Source(string Name);

    private sealed record Target
    {
        public string Name { get; init; } = "";
    }
}
