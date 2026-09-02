using DKNet.Fw.Extensions.Enums;

namespace Fw.Extensions.Tests.Enums;

public class EnumExtensionsTests
{
    #region Methods

    [Fact]
    public void GetAttribute()
    {
        HbdTypes.DescriptionEnum.GetAttribute<DisplayAttribute>()
            .ShouldNotBeNull();
    }

    [Fact]
    public void GetAttributeReturnsNullForEnumWithoutAttribute()
    {
        // Arrange
        var enumValue = HbdTypes.Enum;

        // Act
        var result = enumValue.GetAttribute<DisplayAttribute>();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetAttributeReturnsNullForNullEnum()
    {
        // Arrange
        HbdTypes? nullEnum = null;

        // Act
        var result = nullEnum.GetAttribute<DisplayAttribute>();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetEnumInfoReturnsCorrectInfoForEnumWithoutDisplay()
    {
        // Arrange
        var enumValue = HbdTypes.Enum;

        // Act
        var result = enumValue.GetEnumInfo();

        // Assert
        result.ShouldNotBeNull();
        result.Key.ShouldBe("Enum");
        result.Name.ShouldBeNull();
        result.Description.ShouldBeNull();
        result.GroupName.ShouldBeNull();
    }

    [Fact]
    public void GetEnumInfoReturnsNullForNullEnum()
    {
        // Arrange
        HbdTypes? nullEnum = null;

        // Act
        var result = nullEnum.GetEnumInfo();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetEnumInfosIncludesEnumWithoutDisplayAttribute()
    {
        // Arrange & Act
        var list = EnumExtensions.GetEnumInfos<HbdTypes>().ToList();

        // Assert
        var enumInfo = list.FirstOrDefault(x => string.Equals(x.Key, "Enum", StringComparison.OrdinalIgnoreCase));
        enumInfo.ShouldNotBeNull();
        enumInfo.Name.ShouldBe("Enum");
        enumInfo.Description.ShouldBeNull();
        enumInfo.GroupName.ShouldBeNull();
    }

    [Fact]
    public void TestGetEnumInfo()
    {
        HbdTypes.DescriptionEnum.GetEnumInfo()?.Name.ShouldBe("HBD");
    }

    [Fact]
    public void TestGetEnumInfos()
    {
        var list = EnumExtensions.GetEnumInfos<HbdTypes>().ToList();
        list.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void GetEnumInfosExcludesBackingFieldForNonIntBackedEnum()
    {
        // Arrange & Act
        var list = EnumExtensions.GetEnumInfos<ByteBackedTypes>().ToList();

        // Assert
        list.Count.ShouldBe(2);
        list.ShouldNotContain(x => x.Key == "value__");
    }

    #endregion
}