using System.Collections.Generic;
using DKNet.EfCore.Events.Handlers;

namespace EfCore.Events.Tests;

public class EventObjectTests
{
    [Fact]
    public void EventObject_Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var entityType = "TestEntity";
        var primaryKey = new Dictionary<string, object?> { { "Id", Guid.NewGuid() }, { "Code", "TEST001" } };
        var events = new object[] { new EntityAddedEvent(), "StringEvent", 42 };

        // Act
        var eventObject = new EventObject(entityType, primaryKey, events);

        // Assert
        eventObject.EntityType.ShouldBe(entityType);
        eventObject.PrimaryKey.ShouldBe(primaryKey);
        eventObject.Events.ShouldBe(events);
    }

    [Fact]
    public void EventObject_WithNullPrimaryKey_ShouldAcceptNull()
    {
        // Arrange
        var entityType = "TestEntity";
        var primaryKey = new Dictionary<string, object?> { { "Id", null } };
        var events = new object[] { new EntityAddedEvent() };

        // Act
        var eventObject = new EventObject(entityType, primaryKey, events);

        // Assert
        eventObject.EntityType.ShouldBe(entityType);
        eventObject.PrimaryKey.ShouldBe(primaryKey);
        eventObject.PrimaryKey["Id"].ShouldBeNull();
        eventObject.Events.ShouldBe(events);
    }

    [Fact]
    public void EventObject_WithEmptyEvents_ShouldAcceptEmptyArray()
    {
        // Arrange
        var entityType = "TestEntity";
        var primaryKey = new Dictionary<string, object?> { { "Id", Guid.NewGuid() } };
        var events = new object[0];

        // Act
        var eventObject = new EventObject(entityType, primaryKey, events);

        // Assert
        eventObject.EntityType.ShouldBe(entityType);
        eventObject.PrimaryKey.ShouldBe(primaryKey);
        eventObject.Events.ShouldBe(events);
        eventObject.Events.Length.ShouldBe(0);
    }

    [Fact]
    public void EventObject_WithComplexPrimaryKey_ShouldPreserveAllKeyParts()
    {
        // Arrange
        var entityType = "ComplexEntity";
        var primaryKey = new Dictionary<string, object?>
        {
            { "TenantId", "tenant-123" },
            { "Id", Guid.NewGuid() },
            { "Version", 1 },
            { "OptionalKey", null }
        };
        var events = new object[] { new EntityAddedEvent { Name = "Complex Event" } };

        // Act
        var eventObject = new EventObject(entityType, primaryKey, events);

        // Assert
        eventObject.EntityType.ShouldBe(entityType);
        eventObject.PrimaryKey.ShouldBe(primaryKey);
        eventObject.PrimaryKey.Count.ShouldBe(4);
        eventObject.PrimaryKey["TenantId"].ShouldBe("tenant-123");
        eventObject.PrimaryKey["Id"].ShouldNotBeNull();
        eventObject.PrimaryKey["Version"].ShouldBe(1);
        eventObject.PrimaryKey["OptionalKey"].ShouldBeNull();
        eventObject.Events.ShouldBe(events);
    }

    [Fact]
    public void EventObject_WithMixedEventTypes_ShouldPreserveAllEventTypes()
    {
        // Arrange
        var entityType = "MixedEntity";
        var primaryKey = new Dictionary<string, object?> { { "Id", Guid.NewGuid() } };
        var events = new object[]
        {
            new EntityAddedEvent { Id = Guid.NewGuid(), Name = "Event 1" },
            "String event",
            42,
            new { CustomProperty = "Custom Event" },
            DateTime.Now,
            true
        };

        // Act
        var eventObject = new EventObject(entityType, primaryKey, events);

        // Assert
        eventObject.EntityType.ShouldBe(entityType);
        eventObject.Events.Length.ShouldBe(6);
        eventObject.Events[0].ShouldBeOfType<EntityAddedEvent>();
        eventObject.Events[1].ShouldBeOfType<string>();
        eventObject.Events[2].ShouldBeOfType<int>();
        eventObject.Events[3].ShouldNotBeNull(); // Anonymous type
        eventObject.Events[4].ShouldBeOfType<DateTime>();
        eventObject.Events[5].ShouldBeOfType<bool>();
    }

    [Fact]
    public void EventObject_Properties_ShouldBeReadOnly()
    {
        // Arrange
        var entityType = "ReadOnlyEntity";
        var originalPrimaryKey = new Dictionary<string, object?> { { "Id", Guid.NewGuid() } };
        var originalEvents = new object[] { new EntityAddedEvent() };

        // Act
        var eventObject = new EventObject(entityType, originalPrimaryKey, originalEvents);

        // Assert - Properties should be the same references (read-only)
        eventObject.EntityType.ShouldBe(entityType);
        eventObject.PrimaryKey.ShouldBeSameAs(originalPrimaryKey);
        eventObject.Events.ShouldBeSameAs(originalEvents);
    }

    [Fact]
    public void EventObject_WithLongEntityTypeName_ShouldHandleCorrectly()
    {
        // Arrange
        var entityType = "Very.Long.Namespace.With.Multiple.Parts.And.A.Very.Long.Entity.Name.That.Exceeds.Normal.Length.TestEntity";
        var primaryKey = new Dictionary<string, object?> { { "Id", Guid.NewGuid() } };
        var events = new object[] { new EntityAddedEvent() };

        // Act
        var eventObject = new EventObject(entityType, primaryKey, events);

        // Assert
        eventObject.EntityType.ShouldBe(entityType);
        eventObject.EntityType.Length.ShouldBeGreaterThan(50); // Verify it's actually long
        eventObject.PrimaryKey.ShouldBe(primaryKey);
        eventObject.Events.ShouldBe(events);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SimpleEntity")]
    [InlineData("Namespace.Entity")]
    [InlineData("Very.Long.Namespace.Entity")]
    public void EventObject_WithVariousEntityTypeNames_ShouldHandleCorrectly(string entityType)
    {
        // Arrange
        var primaryKey = new Dictionary<string, object?> { { "Id", Guid.NewGuid() } };
        var events = new object[] { new EntityAddedEvent() };

        // Act
        var eventObject = new EventObject(entityType, primaryKey, events);

        // Assert
        eventObject.EntityType.ShouldBe(entityType);
        eventObject.PrimaryKey.ShouldBe(primaryKey);
        eventObject.Events.ShouldBe(events);
    }
}