using DKNet.EfCore.Extensions.Registers;
using EfCore.TestDataLayer.Mappers;

namespace EfCore.Extensions.Tests;

[TestClass]
public class EntityAutoConfigTests
{
    [TestMethod]
    public void ScanEntites()
    {
        var info =
            new AutoEntityRegistrationInfo(typeof(MyDbContext).Assembly).WithDefaultMappersType(typeof(BaseEntityMapper<>));
        var entities = info.GetAllEntityTypes().ToList();

        entities.ShouldNotBeEmpty();
        entities.Count().ShouldBeGreaterThanOrEqualTo(11);
    }

    [TestMethod]
    public void ScanConfigsShouldIncludeEntitiesThatNotInheritIEntity()
    {
        var info =
            new AutoEntityRegistrationInfo(typeof(MyDbContext).Assembly).WithDefaultMappersType(typeof(BaseEntityMapper<>));
        var configs = info.GetDefinedMappers().ToList();

        configs.ShouldNotBeEmpty();
        configs.Any(c => c == typeof(NotInheritIEntityConfig)).ShouldBeTrue();
    }

    [TestMethod]
    public void ScanGenericConfigs()
    {
        var info =
            new AutoEntityRegistrationInfo(typeof(MyDbContext).Assembly).WithDefaultMappersType(typeof(BaseEntityMapper<>));
        var configs = info.GetGenericMappers().ToList();

        configs.ShouldNotBeEmpty();
        configs.Count.ShouldBe(2);
    }
}