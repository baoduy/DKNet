using EfCore.Extensions.Tests.TestEntities.Mappers;

namespace EfCore.Extensions.Tests;

public class EntityAutoConfigTests
{
    [Fact]
    public void ScanEntites()
    {
        var info =
            new AutoEntityRegistrationInfo(typeof(MyDbContext).Assembly)
                .WithDefaultMappersType(typeof(BaseEntityMapper<>));
        var entities = info.GetAllEntityTypes().ToList();

        entities.ShouldNotBeEmpty();
        entities.Count.ShouldBeGreaterThanOrEqualTo(7);
    }

    [Fact]
    public void ScanConfigsShouldIncludeEntitiesThatNotInheritIEntity()
    {
        var info =
            new AutoEntityRegistrationInfo(typeof(MyDbContext).Assembly).WithDefaultMappersType(
                typeof(BaseEntityMapper<>));
        var configs = info.GetDefinedMappers().ToList();

        configs.ShouldNotBeEmpty();
        configs.Exists(c => c == typeof(NotInheritIEntityConfig)).ShouldBeTrue();
    }

    [Fact]
    public void ScanGenericConfigs()
    {
        var info =
            new AutoEntityRegistrationInfo(typeof(MyDbContext).Assembly).WithDefaultMappersType(
                typeof(BaseEntityMapper<>));
        var configs = info.GetGenericMappers().ToList();

        configs.ShouldNotBeEmpty();
        configs.Count.ShouldBe(1);
    }
}