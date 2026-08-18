using System.Reflection;
using DKNet.EfCore.Abstractions.Events;

namespace EfCore.Abstractions.Tests;

public class GeneratedEventAttributeTests
{
    #region Methods

    [Fact]
    public void Constructor_WithEntityEventAndOperations_StoresAllArguments()
    {
        // Arrange & Act
        var attribute = new GeneratedEventAttribute(typeof(Order), typeof(OrderCreatedEvent), EventOperations.Created);

        // Assert
        attribute.EntityType.ShouldBe(typeof(Order));
        attribute.EventType.ShouldBe(typeof(OrderCreatedEvent));
        attribute.Operations.ShouldBe(EventOperations.Created);
    }

    [Fact]
    public void Constructor_WithNarrowingProperties_StoresPropertyListInOrder()
    {
        // Arrange & Act
        var attribute = new GeneratedEventAttribute(
            typeof(Order), typeof(OrderUpdatedEvent), EventOperations.Updated, "Status", "CompanyName");

        // Assert
        attribute.Properties.ShouldBe(new[] { "Status", "CompanyName" });
    }

    [Fact]
    public void Constructor_WithoutProperties_StoresEmptyList()
    {
        // Arrange & Act
        var attribute = new GeneratedEventAttribute(typeof(Order), typeof(OrderUpdatedEvent), EventOperations.Updated);

        // Assert
        attribute.Properties.ShouldNotBeNull();
        attribute.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void Attribute_IsAssemblyLevelAndMultiUse()
    {
        // Arrange & Act
        var usage = typeof(GeneratedEventAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        // Assert
        usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Assembly);
        usage.AllowMultiple.ShouldBeTrue();
        usage.Inherited.ShouldBeFalse();
    }

    #endregion

    private sealed class Order;

    private sealed record OrderCreatedEvent;

    private sealed record OrderUpdatedEvent;
}
