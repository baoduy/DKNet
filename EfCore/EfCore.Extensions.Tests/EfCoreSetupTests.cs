using DKNet.EfCore.Extensions.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Extensions.Tests;

// Test global query filter register
public class TestGlobalQueryFilterRegister : IGlobalQueryFilterRegister
{
    public void Apply(ModelBuilder modelBuilder, DbContext context)
    {
        // Implementation for testing
    }
}


public class EfCoreSetupTests : SqlServerTestBase
{
    [Fact]
    public void AddGlobalModelBuilderRegister_ShouldAddToGlobalQueryFilters()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialCount = EfCoreSetup.GlobalQueryFilters.Count;

        // Act
        services.AddGlobalModelBuilderRegister<TestGlobalQueryFilterRegister>();

        // Assert
        EfCoreSetup.GlobalQueryFilters.Count.ShouldBe(initialCount + 1);
        EfCoreSetup.GlobalQueryFilters.ShouldContain(typeof(TestGlobalQueryFilterRegister));
    }

    [Fact]
    public void UseAutoConfigModel_GenericDbContext_ShouldReturnTypedBuilder()
    {
        // Arrange
        var container = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            .WithAutoRemove(true)
            .Build();

        var connectionString = "Server=localhost;Database=TestDb;Integrated Security=true;";
        var builder = new DbContextOptionsBuilder<MyDbContext>()
            .UseSqlServer(connectionString);

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
            ((DbContextOptionsBuilder)null!).UseAutoConfigModel());
    }

    [Fact]
    public void UseAutoConfigModel_WithAction_ShouldInvokeAction()
    {
        // Arrange
        var builder = new DbContextOptionsBuilder()
            .UseSqlServer("Server=localhost;Database=TestDb;Integrated Security=true;");
        
        var actionInvoked = false;

        // Act
        builder.UseAutoConfigModel(op =>
        {
            actionInvoked = true;
            op.ShouldNotBeNull();
        });

        // Assert
        actionInvoked.ShouldBeTrue();
    }

    [Fact]
    public void GetOrCreateExtension_ShouldReturnExtension()
    {
        // Arrange
        var builder = new DbContextOptionsBuilder()
            .UseSqlServer("Server=localhost;Database=TestDb;Integrated Security=true;");

        // Act
        var extension1 = builder.GetOrCreateExtension();
        var extension2 = builder.GetOrCreateExtension();

        // Assert
        extension1.ShouldNotBeNull();
        extension2.ShouldNotBeNull();
        extension1.ShouldBeSameAs(extension2); // Should return the same instance
    }
}