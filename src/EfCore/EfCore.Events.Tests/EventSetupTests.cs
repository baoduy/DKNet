using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Events.Internals;

namespace EfCore.Events.Tests;

public class EventSetupTests
{
    #region Methods

    [Fact]
    public void AddEventPublisher_ShouldAllowCustomEventPublisherImplementation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DddContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Act
        services.AddEventPublisher<DddContext, CustomEventPublisher>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var eventPublisher = serviceProvider.GetService<IEventPublisher>();
        eventPublisher.ShouldNotBeNull();
        eventPublisher.ShouldBeOfType<CustomEventPublisher>();
    }

    [Fact]
    public void AddEventPublisher_ShouldRegisterEventPublisherAndHook()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DddContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Act
        services.AddEventPublisher<DddContext, TestEventPublisher>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Should register IEventPublisher
        var eventPublisher = serviceProvider.GetService<IEventPublisher>();
        eventPublisher.ShouldNotBeNull();
        eventPublisher.ShouldBeOfType<TestEventPublisher>();

        // Should register the EventHook
        var hooks = serviceProvider.GetKeyedServices<IHookBaseAsync>(typeof(DddContext).FullName).ToList();
        hooks.ShouldNotBeEmpty();
        hooks.ShouldContain(h => h is EventHook);
    }

    [Fact]
    public void AddEventPublisher_ShouldRegisterServicesWithCorrectLifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DddContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Act
        services.AddEventPublisher<DddContext, TestEventPublisher>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Create multiple scopes to test service lifetime
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var publisher1 = scope1.ServiceProvider.GetService<IEventPublisher>();
        var publisher2 = scope2.ServiceProvider.GetService<IEventPublisher>();

        // Should be scoped, so different instances per scope
        publisher1.ShouldNotBeSameAs(publisher2);

        var hook1 = scope1.ServiceProvider.GetKeyedServices<IHookBaseAsync>(typeof(DddContext).FullName)
            .FirstOrDefault(h => h is EventHook);
        var hook2 = scope2.ServiceProvider.GetKeyedServices<IHookBaseAsync>(typeof(DddContext).FullName)
            .FirstOrDefault(h => h is EventHook);

        // Hooks should also be scoped
        hook1.ShouldNotBeSameAs(hook2);
    }

    [Fact]
    public void AddEventPublisher_ShouldReturnServiceCollection_ForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DddContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Act
        var result = services.AddEventPublisher<DddContext, TestEventPublisher>();

        // Assert
        result.ShouldBeSameAs(services);

        // Should allow method chaining
        var chainedResult = services
            .AddEventPublisher<DddContext, TestEventPublisher>()
            .AddScoped(_ => "test");

        chainedResult.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddEventPublisher_WithDifferentDbContexts_ShouldRegisterSeparateHooks()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DddContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddDbContext<AnotherDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Act
        services.AddEventPublisher<DddContext, TestEventPublisher>();
        services.AddEventPublisher<AnotherDbContext, AnotherTestEventPublisher>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var eventPublishers = serviceProvider.GetServices<IEventPublisher>().ToList();
        eventPublishers.Count.ShouldBe(2);

        var hooks = serviceProvider.GetKeyedServices<IHookBaseAsync>(typeof(DddContext).FullName).OfType<EventHook>()
            .ToList();
        hooks.Count.ShouldBe(1); // Should have separate hooks for each DbContext
    }

    [Fact]
    public void AddEventPublisher_WithMultipleCalls_ShouldRegisterMultiplePublishers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DddContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Act
        services.AddEventPublisher<DddContext, TestEventPublisher>();
        services.AddEventPublisher<DddContext, AnotherTestEventPublisher>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var eventPublishers = serviceProvider.GetServices<IEventPublisher>().ToList();
        eventPublishers.Count.ShouldBe(2);
        eventPublishers.ShouldContain(p => p is TestEventPublisher);
        eventPublishers.ShouldContain(p => p is AnotherTestEventPublisher);
    }

    #endregion

    private class AnotherDbContext(DbContextOptions<AnotherDbContext> options) : DbContext(options)
    {
        // Empty DbContext for testing different contexts
    }

    // Helper classes for testing
    private class AnotherTestEventPublisher : IEventPublisher
    {
        #region Methods

        public Task PublishAsync(object eventObj, CancellationToken cancellationToken = default) => Task.CompletedTask;

        #endregion
    }

    private class CustomEventPublisher : IEventPublisher
    {
        #region Properties

        public string CustomProperty { get; } = "Custom";

        #endregion

        #region Methods

        public Task PublishAsync(object eventObj, CancellationToken cancellationToken = default) => Task.CompletedTask;

        #endregion
    }
}