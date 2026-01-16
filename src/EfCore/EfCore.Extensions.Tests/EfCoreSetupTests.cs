using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Extensions.Tests;

// Test global query filter register
public class TestGlobalQueryFilter : IGlobalModelBuilder
{
    #region Methods

    public void Apply(ModelBuilder modelBuilder, DbContext context)
    {
        // Implementation for testing
    }

    #endregion
}

public class EfCoreSetupTests
{
    #region Methods

    [Fact]
    public void AddGlobalModelBuilderRegister_ShouldAddToGlobalQueryFilters()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialCount = EfCoreSetup.GlobalModelBuilders.Count;

        // Act
        services.AddGlobalModelBuilder<TestGlobalQueryFilter>();

        // Assert
        EfCoreSetup.GlobalModelBuilders.Count.ShouldBe(initialCount + 1);
        EfCoreSetup.GlobalModelBuilders.ShouldContain(typeof(TestGlobalQueryFilter));
    }

    /// <summary>
    ///     Verifies AddSlimBusEfCoreExceptionHandler registers the correct handler keyed by DbContext type.
    /// </summary>
    [Fact]
    public void AddSlimBusEfCoreExceptionHandler_RegistersKeyedHandler_CanResolveByKey()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEfCoreExceptionHandler<MyDbContext, TestExceptionHandler>();
        var provider = services.BuildServiceProvider();
        var key = typeof(MyDbContext).FullName;

        // Act
        var handler = provider.GetKeyedService<IEfCoreExceptionHandler>(key!);

        // Assert
        handler.ShouldNotBeNull();
        handler.ShouldBeOfType<TestExceptionHandler>();
    }

    [Fact]
    public void UseAutoConfigModel_GenericDbContext_ShouldReturnTypedBuilder()
    {
        // Arrange
        var builder = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase("TestDb2");

        // Act
        var result = builder.UseAutoConfigModel();

        // Assert
        result.ShouldBeOfType<DbContextOptionsBuilder<MyDbContext>>();
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseAutoConfigModel_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((DbContextOptionsBuilder)null!).UseAutoConfigModel([typeof(MyDbContext).Assembly]));
    }

    #endregion

    private class TestExceptionHandler : IEfCoreExceptionHandler
    {
        #region Methods

        public Task<EfConcurrencyResolution> HandlingAsync(DbContext context, DbUpdateConcurrencyException exception,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EfConcurrencyResolution.IgnoreChanges);

        #endregion
    }
}