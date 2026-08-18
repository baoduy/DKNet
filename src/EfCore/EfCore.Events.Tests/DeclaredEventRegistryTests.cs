using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Events.Internals;

namespace EfCore.Events.Tests;

public class DeclaredEventRegistryTests
{
    #region Methods

    [Fact]
    public void GetDeclaredEvents_ForDeclaredEntity_ReturnsRegisteredEvents()
    {
        // Act
        var declared = DeclaredEventRegistry.GetDeclaredEvents(typeof(Order)).ToArray();

        // Assert
        declared.ShouldNotBeEmpty();
        declared.Select(d => d.EventType.Name).ShouldContain("OrderCreatedEvent");
        declared.Select(d => d.EventType.Name).ShouldContain("OrderUpdatedEvent");
        declared.Select(d => d.EventType.Name).ShouldContain("OrderDeletedEvent");
    }

    [Fact]
    public void GetDeclaredEvents_ForDeclaredEntity_RegistersExpectedOperations()
    {
        // Act
        var declared = DeclaredEventRegistry.GetDeclaredEvents(typeof(Order)).ToArray();

        // Assert
        declared.Single(d => d.EventType.Name == "OrderCreatedEvent").Operations.ShouldBe(EventOperations.Created);
        declared.Single(d => d.EventType.Name == "OrderDeletedEvent").Operations.ShouldBe(EventOperations.Deleted);
    }

    [Fact]
    public void GetDeclaredEvents_ForNarrowedDeclaration_CarriesPropertyList()
    {
        // Act
        var declared = DeclaredEventRegistry.GetDeclaredEvents(typeof(Order)).ToArray();

        // Assert
        var narrowed = declared.Single(d => d.EventType.Name == "OrderStatusChangedEvent");
        narrowed.Properties.ShouldBe(new[] { "Status" });
    }

    [Fact]
    public void GetDeclaredEvents_ForUndecoratedEntity_ReturnsNothing()
    {
        // Act
        var declared = DeclaredEventRegistry.GetDeclaredEvents(typeof(Product));

        // Assert
        declared.ShouldBeEmpty();
    }

    #endregion
}
