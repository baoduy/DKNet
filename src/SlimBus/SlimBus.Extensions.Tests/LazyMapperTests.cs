using FluentResults;
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

    [Fact]
    public void Errors_ReadMultipleTimes_ReturnsSameCachedInstance()
    {
        var result = new LazyResult<Target>(new Source("a"), NewMapper())
        {
            Reasons = [new Error("boom"), new Success("ok")],
        };

        var first = result.Errors;
        var second = result.Errors;

        second.ShouldBeSameAs(first);
        first.Count.ShouldBe(1);
    }

    [Fact]
    public void Successes_ReadMultipleTimes_ReturnsSameCachedInstance()
    {
        var result = new LazyResult<Target>(new Source("a"), NewMapper())
        {
            Reasons = [new Error("boom"), new Success("ok")],
        };

        var first = result.Successes;
        var second = result.Successes;

        second.ShouldBeSameAs(first);
        first.Count.ShouldBe(1);
    }

    [Fact]
    public void IsFailed_WithErrorReason_ReturnsTrue()
    {
        var result = new LazyResult<Target>(new Source("a"), NewMapper())
        {
            Reasons = [new Error("boom")],
        };

        result.IsFailed.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void LazyMap_WithStructTarget_MapsSource()
        => NewMapper().LazyMap<Point>(new PointSource(3, 4)).Value.ShouldBe(new Point { X = 3, Y = 4 });

    [Fact]
    public void LazyMap_WithStructSourceAlreadyTarget_ReturnsItWithoutMapping()
    {
        var mapper = new CountingMapper();

        mapper.LazyMap<Point>(new Point { X = 7, Y = 8 }).Value.ShouldBe(new Point { X = 7, Y = 8 });
        mapper.Calls.ShouldBe(0);
    }

    [Fact]
    public void LazyMap_WithPrimitiveTarget_ReturnsSource()
        => NewMapper().LazyMap<int>(42).Value.ShouldBe(42);

    [Fact]
    public void LazyMap_WithNullValueAndStructTarget_ValueThrows_AndValueOrDefaultIsDefault()
    {
        var mapper = new CountingMapper();
        var lazy = mapper.LazyMap<Point>(null!);

        lazy.ValueOrDefault.ShouldBe(new Point { X = 0, Y = 0 });
        Should.Throw<InvalidOperationException>(() => _ = lazy.Value);
        mapper.Calls.ShouldBe(0);
    }

    [Fact]
    public void LazyMap_WithStructTarget_ReadThreeTimes_MapsOnce()
    {
        var mapper = new CountingMapper();
        var lazy = mapper.LazyMap<Point>(new PointSource(3, 4));

        lazy.Value.ShouldBe(new Point { X = 3, Y = 4 });
        lazy.Value.ShouldBe(new Point { X = 3, Y = 4 });
        lazy.Value.ShouldBe(new Point { X = 3, Y = 4 });
        mapper.Calls.ShouldBe(1);
    }

    [Fact]
    public void LazyMap_WithClassTargetMappedToNull_ReadThreeTimes_MapsOnce()
    {
        var mapper = new CountingMapper(returnsNull: true);
        var lazy = mapper.LazyMap<Target>(new Source("a"));

        lazy.ValueOrDefault.ShouldBeNull();
        lazy.ValueOrDefault.ShouldBeNull();
        lazy.ValueOrDefault.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => _ = lazy.Value);
        mapper.Calls.ShouldBe(1);
    }

    [Fact]
    public void ResultOf_WithStructTarget_IsSuccessAndMapsValue()
    {
        var rs = NewMapper().ResultOf<Point>(new PointSource(3, 4));
        rs.IsSuccess.ShouldBeTrue();
        rs.Value.ShouldBe(new Point { X = 3, Y = 4 });
    }

    [Fact]
    public void LazyMap_WithStructTargetMappedToAllDefault_ReadThreeTimes_MapsOnce()
    {
        var mapper = new CountingMapper();
        var lazy = mapper.LazyMap<Point>(new PointSource(0, 0));

        lazy.Value.ShouldBe(new Point { X = 0, Y = 0 });
        lazy.Value.ShouldBe(new Point { X = 0, Y = 0 });
        lazy.Value.ShouldBe(new Point { X = 0, Y = 0 });
        mapper.Calls.ShouldBe(1);
    }

    #endregion

    private sealed record Source(string Name);

    private sealed record Target
    {
        public string Name { get; init; } = "";
    }

    private sealed record PointSource(int X, int Y);

    private struct Point
    {
        public int X { get; set; }
        public int Y { get; set; }

        public override readonly string ToString() => $"X={X} Y={Y}";
    }

    // Counts Map<TDestination>(object) calls, the overload LazyMap uses; returnsNull simulates a mapper that yields null.
    private sealed class CountingMapper(bool returnsNull = false) : Mapper(new TypeAdapterConfig())
    {
        public int Calls { get; private set; }

        public override TDestination Map<TDestination>(object source)
        {
            Calls++;
            return returnsNull ? default! : base.Map<TDestination>(source);
        }
    }
}
