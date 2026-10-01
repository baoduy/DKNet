using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Extensions.Tests;

/// <summary>
///     DRK-1970: how <see cref="EntityAutoConfigRegister" /> wraps the <see cref="IModelCacheKeyFactory" /> it finds,
///     and the cache key the wrapper builds.
/// </summary>
[Collection(nameof(GlobalModelBuilderCacheCollection))]
public class GlobalModelBuilderCacheKeyFactoryTests
{
    #region Methods

    private static IModelCacheKeyFactory ApplyAndResolve(IServiceCollection services)
    {
        new EntityAutoConfigRegister([]).ApplyServices(services);
        return services.BuildServiceProvider().GetRequiredService<IModelCacheKeyFactory>();
    }

    [Fact]
    public void ApplyServices_KeyFactoryRegisteredByType_WrapsItAndKeepsLifetime()
    {
        // Arrange
        var services = new ServiceCollection().AddScoped<IModelCacheKeyFactory, CountingKeyFactory>();

        // Act
        new EntityAutoConfigRegister([]).ApplyServices(services);

        // Assert
        var descriptor = services.Single(s => s.ServiceType == typeof(IModelCacheKeyFactory));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        using var scope = services.BuildServiceProvider().CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IModelCacheKeyFactory>();
        factory.ShouldBeOfType<GlobalModelBuilderCacheKeyFactory>();
        factory.Create(null!, false).ShouldNotBe(factory.Create(null!, true));
    }

    [Fact]
    public void ApplyServices_KeyFactoryRegisteredAsInstance_WrapsThatInstance()
    {
        // Arrange
        var inner = new CountingKeyFactory();
        var services = new ServiceCollection().AddSingleton<IModelCacheKeyFactory>(inner);

        // Act
        var factory = ApplyAndResolve(services);
        factory.Create(null!, false);

        // Assert
        factory.ShouldBeOfType<GlobalModelBuilderCacheKeyFactory>();
        inner.Calls.ShouldBe(1);
    }

    [Fact]
    public void ApplyServices_KeyFactoryRegisteredByFactory_WrapsTheFactoryResult()
    {
        // Arrange
        var inner = new CountingKeyFactory();
        var services = new ServiceCollection().AddSingleton<IModelCacheKeyFactory>(_ => inner);

        // Act
        var factory = ApplyAndResolve(services);
        factory.Create(null!, false);

        // Assert
        factory.ShouldBeOfType<GlobalModelBuilderCacheKeyFactory>();
        inner.Calls.ShouldBe(1);
    }

    [Fact]
    public void ApplyServices_NoKeyFactoryRegistered_AddsSingletonWrapper()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new EntityAutoConfigRegister([]).ApplyServices(services);

        // Assert
        services.Single(s => s.ServiceType == typeof(IModelCacheKeyFactory)).Lifetime
            .ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Create_DesignTimeDiffers_KeysDiffer()
    {
        // Arrange
        var factory = new GlobalModelBuilderCacheKeyFactory(new CountingKeyFactory());

        // Act
        var runtimeKey = factory.Create(null!, false);
        var designTimeKey = factory.Create(null!, true);

        // Assert
        runtimeKey.ShouldNotBe(designTimeKey);
        factory.Create(null!, false).ShouldBe(runtimeKey);
    }

    [Fact]
    public void Create_BuilderTypeRegistered_KeyChangesOnlyForNewType()
    {
        // Arrange
        var factory = new GlobalModelBuilderCacheKeyFactory(new CountingKeyFactory());
        new ServiceCollection().AddGlobalModelBuilder<KeyProbeBuilder>();
        var before = factory.Create(null!, false);

        // Act
        new ServiceCollection().AddGlobalModelBuilder<KeyProbeBuilder>();
        var afterSameType = factory.Create(null!, false);
        new ServiceCollection().AddGlobalModelBuilder<KeyProbeOtherBuilder>();
        var afterNewType = factory.Create(null!, false);

        // Assert
        afterSameType.ShouldBe(before);
        afterNewType.ShouldNotBe(before);
    }

    [Fact]
    public void Model_AutoConfigBeforeProvider_BuilderRegisteredBetweenContexts_SecondModelAppliesBuilder()
    {
        // Arrange: UseAutoConfigModel before the provider, so no IModelCacheKeyFactory is registered yet.
        var options = new DbContextOptionsBuilder<OrderProbeContext>()
            .UseAutoConfigModel(typeof(IAuditedProperties).Assembly)
            .UseInMemoryDatabase(nameof(OrderProbeContext))
            .Options;

        using var first = new OrderProbeContext(options);
        first.Model.FindEntityType(typeof(OrderProbeEntity))!.GetDeclaredQueryFilters().ShouldBeEmpty();

        // Act
        new ServiceCollection().AddGlobalModelBuilder<OrderProbeBuilder>();
        using var second = new OrderProbeContext(options);

        // Assert
        second.Model.FindEntityType(typeof(OrderProbeEntity))!.GetDeclaredQueryFilters()
            .Select(f => f.Key).ShouldBe(["DRK1970.OrderProbe"]);
    }

    #endregion

    private sealed class CountingKeyFactory : IModelCacheKeyFactory
    {
        #region Properties

        public int Calls { get; private set; }

        #endregion

        #region Methods

        public object Create(DbContext context, bool designTime)
        {
            Calls++;
            return designTime;
        }

        #endregion
    }

    public sealed class KeyProbeBuilder : IGlobalModelBuilder
    {
        #region Methods

        public void Apply(ModelBuilder modelBuilder, DbContext context)
        {
        }

        #endregion
    }

    public sealed class KeyProbeOtherBuilder : IGlobalModelBuilder
    {
        #region Methods

        public void Apply(ModelBuilder modelBuilder, DbContext context)
        {
        }

        #endregion
    }

    public sealed class OrderProbeEntity
    {
        #region Properties

        public int Id { get; set; }

        #endregion
    }

    public sealed class OrderProbeContext(DbContextOptions<OrderProbeContext> options) : DbContext(options)
    {
        #region Properties

        public DbSet<OrderProbeEntity> Entities => Set<OrderProbeEntity>();

        #endregion
    }

    public sealed class OrderProbeBuilder : IGlobalModelBuilder
    {
        #region Methods

        public void Apply(ModelBuilder modelBuilder, DbContext context)
        {
            if (context is not OrderProbeContext) return;
            modelBuilder.Entity<OrderProbeEntity>().HasQueryFilter("DRK1970.OrderProbe", e => e.Id > 0);
        }

        #endregion
    }
}
