using DKNet.Fw.Extensions;

namespace Fw.Extensions.Tests;

[TestClass]
public class EnumExtensionsTests
{
    [TestMethod]
    public void GetAttribute()
    {
        HbdTypes.DescriptionEnum.GetAttribute<DisplayAttribute>()
            .ShouldNotBeNull();
    }

    [TestMethod]
    public void TestGetEnumInfo()
    {
        HbdTypes.DescriptionEnum.GetEumInfo().Name.ShouldBe("HBD");
    }

    [TestMethod]
    public void TestGetEnumInfos()
    {
        var list = EnumExtensions.GetEumInfos<HbdTypes>().ToList();
        list.Count.ShouldBe(3);
    }

    [TestMethod]
    public void GetAttributeReturnsNullForNullEnum()
    {
        // Arrange
        HbdTypes? nullEnum = null;

        // Act
        var result = nullEnum.GetAttribute<DisplayAttribute>();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetAttributeReturnsNullForEnumWithoutAttribute()
    {
        // Arrange
        var enumValue = HbdTypes.Enum;

        // Act
        var result = enumValue.GetAttribute<DisplayAttribute>();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetEumInfoReturnsNullForNullEnum()
    {
        // Arrange
        HbdTypes? nullEnum = null;

        // Act
        var result = nullEnum.GetEumInfo();

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetEumInfoReturnsCorrectInfoForEnumWithoutDisplay()
    {
        // Arrange
        var enumValue = HbdTypes.Enum;

        // Act
        var result = enumValue.GetEumInfo();

        // Assert
        result.ShouldNotBeNull();
        result.Key.ShouldBe("Enum");
        result.Name.ShouldBeNull();
        result.Description.ShouldBeNull();
        result.GroupName.ShouldBeNull();
    }

    [TestMethod]
    public void GetEumInfosIncludesEnumWithoutDisplayAttribute()
    {
        // Arrange & Act
        var list = EnumExtensions.GetEumInfos<HbdTypes>().ToList();

        // Assert
        var enumInfo = list.FirstOrDefault(x => string.Equals(x.Key, "Enum", StringComparison.OrdinalIgnoreCase));
        enumInfo.ShouldNotBeNull();
        enumInfo.Name.ShouldBe("Enum");
        enumInfo.Description.ShouldBeNull();
        enumInfo.GroupName.ShouldBeNull();
    }
}