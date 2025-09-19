using DKNet.EfCore.Abstractions.Events;

namespace EfCore.Abstractions.Tests;

public class EventItemTests
{
    [Fact]
    public void EventItem_ShouldHaveCorrectEventType()
    {
        // Arrange & Act
        var eventItem = new TestEventItem();

        // Assert
        eventItem.EventType.ShouldBe(typeof(TestEventItem).FullName);
    }

    [Fact]
    public void EventItem_EventType_ShouldNotBeNull()
    {
        // Arrange & Act
        var eventItem = new TestEventItem();

        // Assert
        eventItem.EventType.ShouldNotBeNull();
        eventItem.EventType.ShouldNotBeEmpty();
    }

    [Fact]
    public void EventItem_AdditionalData_ShouldBeInitialized()
    {
        // Arrange & Act
        var eventItem = new TestEventItem();

        // Assert
        eventItem.AdditionalData.ShouldNotBeNull();
        eventItem.AdditionalData.ShouldBeEmpty();
    }

    [Fact]
    public void EventItem_AdditionalData_ShouldBeCaseInsensitive()
    {
        // Arrange
        var eventItem = new TestEventItem();

        // Act
        eventItem.AdditionalData.Add("Key1", "value1");
        eventItem.AdditionalData["KEY1"] = "value2"; // This should overwrite using indexer

        // Assert
        eventItem.AdditionalData.Count.ShouldBe(1);
        eventItem.AdditionalData["key1"].ShouldBe("value2"); // Case insensitive access
        eventItem.AdditionalData.ContainsKey("KEY1").ShouldBeTrue();
        eventItem.AdditionalData.ContainsKey("key1").ShouldBeTrue();
    }

    [Fact]
    public void EventItem_AdditionalData_ShouldAllowMultipleEntries()
    {
        // Arrange
        var eventItem = new TestEventItem();

        // Act
        eventItem.AdditionalData.Add("key1", "value1");
        eventItem.AdditionalData.Add("key2", "value2");
        eventItem.AdditionalData.Add("key3", "value3");

        // Assert
        eventItem.AdditionalData.Count.ShouldBe(3);
        eventItem.AdditionalData["key1"].ShouldBe("value1");
        eventItem.AdditionalData["key2"].ShouldBe("value2");
        eventItem.AdditionalData["key3"].ShouldBe("value3");
    }

    [Fact]
    public void EventItem_AdditionalData_ShouldHandleNullValues()
    {
        // Arrange
        var eventItem = new TestEventItem();

        // Act
        eventItem.AdditionalData.Add("nullKey", null!);
        eventItem.AdditionalData.Add("emptyKey", "");

        // Assert
        eventItem.AdditionalData.Count.ShouldBe(2);
        eventItem.AdditionalData["nullKey"].ShouldBeNull();
        eventItem.AdditionalData["emptyKey"].ShouldBe("");
    }

    [Fact]
    public void EventItem_WithDifferentType_ShouldReturnCorrectType()
    {
        // Arrange & Act
        var eventItem = new DifferentEventTypeItem();

        // Assert
        eventItem.EventType.ShouldBe(typeof(DifferentEventTypeItem).FullName);
    }

    // Test classes
    private record TestEventItem : EventItem;

    private record DifferentEventTypeItem : EventItem;
}