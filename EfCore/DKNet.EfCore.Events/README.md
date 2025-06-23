# DKNet.EfCore.Events

`DKNet.EfCore.Events` is part of the WX suite, designed to enhance Entity Framework Core (EF Core) applications by providing event-based functionality. This library enables developers to trigger events based on data changes (e.g., `onCreate`, `onUpdate`, or `onDelete`) and handle them using custom logic.

## Overview

The goal of `DKNet.EfCore.Events` is to simplify the implementation of domain-driven design (DDD) principles in EF Core applications. It provides a centralized way to manage events and their handlers, allowing developers to implement business rules and validation logic without tightly coupling it with the data access layer.

## Key Concepts

- **EventPublisher**: A central hub for publishing events. All events are routed through this publisher.

- **IEventEntity**: An interface that defines methods for adding domain events to a queue and retrieving/clearing them.

- **Custom Event Handlers**: Developers can implement custom handlers to respond to specific events, such as validating data or triggering external services.

## Features

### 1. **Event Triggers**
- Automatically fire events in response to changes in your data (e.g., `onCreate`, `onUpdate`, or `onDelete`).
- Events are queued and published through the `IEventPublisher`.

### 2. **Custom Event Handlers**
- Write custom handlers to implement specific business logic for any event.
- Handlers can be registered as decorators or directly in your application.

### 3. **Pre & Post Save Actions**
- Hook pre-save and post-save actions during database operations.
- Perform validation, data transformation, or external API calls before or after saving changes.

### 4. **Integration with EF Core**
- Seamless integration with Entity Framework Core for event-driven database operations.
- Supports both domain events and application-specific events.

## How to Use

1. **Add the Package**

   Include `DKNet.EfCore.Events` in your project by adding the following reference to your `.csproj` file:

   ```xml
   <PackageReference Include="DKNet.EfCore.Events" Version="x.y.z" />
   ```

2. **Implement IEventEntity**

   Use `IEventEntity` to add events to entities:

   ```csharp
   public class MyDomainEntity : IEventEntity
   {
       // Implement business logic that triggers AddEvent calls.
   }
   ```

3. **Register Event Handlers**

   Register custom event handlers in your application startup or configuration:

   ```csharp
   services.AddScoped<IEventHandler<MyCustomEvent>, CustomEventHandler>();
   ```

4. **Publish Events**

   Use the `IEventPublisher` to publish events:

   ```csharp
   await _eventPublisher.PublishAsync(new MyDomainEvent());
   ```

## Advanced Usage

### Domain Events vs Application Events
- **Domain Events**: Represent business concepts and are raised by entities (`IEventEntity`).
- **Application Events**: Used for application-level operations, such as sending emails or notifications.

### Event Handling Location
- **Decorators**: Implement handlers as decorators around your domain logic.
- **Registered Services**: Register handlers directly in your dependency injection container.

### Best Practices
- Use events to decouple data changes from the logic that responds to those changes.
- Keep event handlers focused on a single responsibility (e.g., validation, logging).
- Handle errors in event handlers using appropriate error handling strategies.

## Troubleshooting

If you encounter issues:
1. Check if your entity implements `IEventEntity` and properly triggers events.
2. Ensure that your event handlers are registered or decorated correctly.
3. Verify that the `IEventPublisher` is being injected and used appropriately.

## Contributions

Contributions are welcome! Familiarize yourself with the WX design principles before making changes. Submit pull requests with detailed explanations of your changes.

## License

`DKNet.EfCore.Events` is MIT licensed, allowing you to use, modify, and distribute it freely.

## References

- **EF Core**: [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore)
- **FluentAssertions**: [FluentAssertions NuGet Package](https://www.nuget.org/packages/FluentAssertions)