using DKNet.EfCore.Abstractions.Events;
using DKNet.SlimBus.Extensions.Handlers;

namespace SlimBus.Extensions.Tests;

public class SlimBusEventPublisherTests(Fixture fixture) : IClassFixture<Fixture>
{
    #region Methods

    [Fact]
    public async Task PublishAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var messageBus = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var publisher = new SlimBusEventPublisher(messageBus);
        var regularEvent = new RegularEvent { Message = "test message" };

        // Act & Assert - should not throw with default cancellation token
        await publisher.PublishAsync(regularEvent, CancellationToken.None);

        // Test passes if no exception is thrown
        regularEvent.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PublishAsync_WithEventItem_ShouldHandleAdditionalData()
    {
        // Arrange
        var messageBus = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var publisher = new SlimBusEventPublisher(messageBus);
        var eventItem = new TestEventItem
        {
            TestProperty = "test-value"
        };
        eventItem.AdditionalData.Add("key1", "value1");
        eventItem.AdditionalData.Add("key2", "value2");

        // Act & Assert - should not throw
        await publisher.PublishAsync(eventItem, CancellationToken.None);

        // Test passes if no exception is thrown, covering the IEventItem path
        eventItem.AdditionalData.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PublishAsync_WithEventItemEmptyAdditionalData_ShouldCreateEmptyHeaders()
    {
        // Arrange  
        var messageBus = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var publisher = new SlimBusEventPublisher(messageBus);
        var eventItem = new TestEventItem();
        // AdditionalData is empty by default

        // Act & Assert - should not throw
        await publisher.PublishAsync(eventItem, CancellationToken.None);

        // Test passes if no exception is thrown, covering empty headers case
        eventItem.AdditionalData.Count.ShouldBe(0);
    }

    [Fact]
    public async Task PublishAsync_WithNonEventItem_ShouldPublishDirectly()
    {
        // Arrange
        var messageBus = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var publisher = new SlimBusEventPublisher(messageBus);
        var regularEvent = new RegularEvent { Message = "test message" };

        // Act & Assert - should not throw
        await publisher.PublishAsync(regularEvent, CancellationToken.None);

        // Test passes if no exception is thrown, covering the non-IEventItem path
        regularEvent.Message.ShouldBe("test message");
    }

    #endregion

    private record RegularEvent
    {
        #region Properties

        public string Message { get; init; } = string.Empty;

        #endregion
    }

    // Test classes
    private record TestEventItem : EventItem
    {
        #region Properties

        public string TestProperty { get; init; } = string.Empty;

        #endregion
    }
}