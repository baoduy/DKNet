using DKNet.EfCore.Extensions.Convertors;

namespace EfCore.Extensions.Tests;

public class GuidV7ValueGeneratorTests
{
    #region Methods

    [Fact]
    public void GeneratesTemporaryValuesIsFalse()
    {
        var generator = new GuidV7ValueGenerator();

        generator.GeneratesTemporaryValues.ShouldBeFalse();
    }

    [Fact]
    public void Next_ReturnsVersion7Guid()
    {
        // Arrange
        var generator = new GuidV7ValueGenerator();

        // Act: Next ignores the EntityEntry parameter entirely (Guid.CreateVersion7() needs no
        // per-entity state), so a null entry is a faithful regression test of the in-box swap.
        var id = generator.Next(null!);

        // Assert: RFC 9562 version 7 - version nibble is 7, variant bits are RFC 4122 (10xx).
        var bytes = id.ToByteArray();
        var version = bytes[7] >> 4;
        var variant = bytes[8] >> 6;
        version.ShouldBe(7);
        variant.ShouldBe(0b10);
    }

    [Fact]
    public void Next_CalledRepeatedly_ReturnsDistinctValues()
    {
        var generator = new GuidV7ValueGenerator();

        var first = generator.Next(null!);
        var second = generator.Next(null!);

        first.ShouldNotBe(second);
    }

    #endregion
}
