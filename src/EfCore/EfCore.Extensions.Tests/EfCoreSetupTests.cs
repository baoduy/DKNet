using DKNet.EfCore.Extensions.Configurations;
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
        var initialCount = EfCoreSetup.GlobalQueryFilters.Count;

        // Act
        services.AddGlobalQueryFilter<TestGlobalQueryFilter>();

        // Assert
        EfCoreSetup.GlobalQueryFilters.Count.ShouldBe(initialCount + 1);
        EfCoreSetup.GlobalQueryFilters.ShouldContain(typeof(TestGlobalQueryFilter));
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
}