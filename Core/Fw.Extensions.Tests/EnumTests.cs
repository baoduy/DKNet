using DKNet.Fw.Extensions;
using Fw.Extensions.Tests.TestObjects;

namespace Fw.Extensions.Tests;

[TestClass]
public class EnumExtensionsTests
{
    [TestMethod]
    public void GetAttribute()
    {
        HBDEnum.DescriptionEnum.GetAttribute<DisplayAttribute>()
            .ShouldNotBeNull();
    }

    [TestMethod]
    public void TestGetEnumInfo()
    {
        HBDEnum.DescriptionEnum.GetEumInfo().Name.ShouldBe("HBD");
    }

    [TestMethod]
    public void TestGetEnumInfos()
    {
        var list = EnumExtensions.GetEumInfos<HBDEnum>().ToList();
        list.Count.ShouldBe(3);
    }
}