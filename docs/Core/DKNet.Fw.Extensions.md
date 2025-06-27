# DKNet.Fw.Extensions

**A comprehensive collection of extension methods and utilities for .NET applications that provide foundational functionality for the entire DKNet framework.**

## What is this project?

DKNet.Fw.Extensions is the foundational library of the DKNet framework that provides a rich set of extension methods and utilities for common .NET types. This library enhances the built-in .NET functionality with practical extensions for strings, types, enums, async operations, and more. It serves as the bedrock for all other DKNet components by providing consistent utilities and helper methods.

### Key Features

- **String Extensions**: Advanced string manipulation, validation, and processing
- **Type Extensions**: Enhanced type checking, reflection, and inheritance utilities
- **Enum Extensions**: Rich enum information extraction with Display attribute support
- **DateTime Extensions**: Date and time manipulation utilities
- **Async Extensions**: Enhanced async enumerable processing
- **Property Extensions**: Reflection-based property manipulation
- **Attribute Extensions**: Attribute processing and metadata extraction
- **Encryption Services**: Secure string encryption and hashing utilities
- **Service Collection Extensions**: Enhanced dependency injection with keyed services

## How it contributes to DDD and Onion Architecture

### Cross-Cutting Concerns Layer

In the Onion Architecture, DKNet.Fw.Extensions provides the **Cross-Cutting Concerns** layer that supports all other layers without creating dependencies:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                                                                 │
│  Uses: String validation, Type checking, Enum info             │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│                                                                 │
│  Uses: Service registration, Async processing, Encryption      │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│                                                                 │
│  Uses: Type extensions, Enum utilities, Property helpers       │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                                                                 │
│  Uses: All extensions for data processing & external APIs      │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│            ⚙️ DKNet.Fw.Extensions (Foundation)                 │
│                                                                 │
│  • No dependencies on other layers                             │
│  • Provides utilities for all layers                           │
│  • Enables clean, readable code                                │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Ubiquitous Language**: Enum extensions with Display attributes help maintain consistent terminology
2. **Value Objects**: String and type validation supports robust value object creation
3. **Domain Events**: Type extensions facilitate dynamic event type discovery
4. **Aggregate Consistency**: Property extensions enable reflection-based validation

### Onion Architecture Benefits

1. **Dependency Inversion**: No dependencies on business logic or infrastructure
2. **Testability**: All extensions are pure functions that are easily testable
3. **Separation of Concerns**: Each extension category handles specific technical concerns
4. **Reusability**: Common functionality is centralized and reusable across layers

## How to use it

### Installation

```bash
dotnet add package DKNet.Fw.Extensions
```

### Basic Usage Examples

#### 1. String Extensions

```csharp
using DKNet.Fw.Extensions;

// String validation
var phoneNumber = "123-456-7890";
bool isNumeric = phoneNumber.IsNumber(); // false (contains dashes)

var price = "99.99";
bool isPriceValid = price.IsNumber(); // true

// Extract digits from formatted strings
var cleanNumber = phoneNumber.ExtractDigits(); // "1234567890"
```

#### 2. Enum Extensions with Display Attributes

```csharp
using System.ComponentModel.DataAnnotations;
using DKNet.Fw.Extensions;

public enum OrderStatus
{
    [Display(Name = "Pending", Description = "Order is waiting for processing")]
    Pending,
    
    [Display(Name = "Processing", Description = "Order is being processed")]
    Processing,
    
    [Display(Name = "Completed", Description = "Order has been completed")]
    Completed
}

// Usage
OrderStatus status = OrderStatus.Pending;
var enumInfo = status.GetEumInfo();
Console.WriteLine($"Status: {enumInfo.Name}"); // "Status: Pending"
Console.WriteLine($"Description: {enumInfo.Description}"); // "Description: Order is waiting for processing"

// Get all enum information
var allStatuses = EnumExtensions.GetEumInfos<OrderStatus>();
foreach (var info in allStatuses)
{
    Console.WriteLine($"{info.Key}: {info.Name} - {info.Description}");
}
```

#### 3. Type Extensions for Domain Logic

```csharp
using DKNet.Fw.Extensions;

// Check if a type implements an interface
public interface IEntity
{
    int Id { get; set; }
}

public class User : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
}

// Type checking
Type userType = typeof(User);
bool implementsEntity = userType.IsImplementOf(typeof(IEntity)); // true

// Useful for repository pattern implementations
public class GenericRepository<T> where T : class
{
    public void ValidateEntityType()
    {
        if (!typeof(T).IsImplementOf(typeof(IEntity)))
        {
            throw new ArgumentException("T must implement IEntity");
        }
    }
}
```

#### 4. Service Collection Extensions for DI

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.Fw.Extensions;

public void ConfigureServices(IServiceCollection services)
{
    // Register multiple implementations with a key
    var handlerTypes = new[]
    {
        typeof(OrderCreatedHandler),
        typeof(OrderUpdatedHandler),
        typeof(OrderDeletedHandler)
    };
    
    services.AsKeyedImplementedInterfaces("OrderHandlers", handlerTypes, ServiceLifetime.Scoped);
    
    // Later resolve by key
    var provider = services.BuildServiceProvider();
    var handlers = provider.GetKeyedServices<IEventHandler>("OrderHandlers");
}
```

#### 5. Property Extensions for Reflection

```csharp
using DKNet.Fw.Extensions;
using System.Reflection;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// Dynamic property manipulation
var product = new Product { Id = 1, Name = "Laptop", Price = 999.99m };
var properties = typeof(Product).GetProperties();

foreach (var property in properties)
{
    if (property.Name.IsNumber()) // Using string extension
    {
        // Handle numeric properties differently
        var value = property.GetValue(product);
        Console.WriteLine($"{property.Name}: {value}");
    }
}
```

#### 6. Encryption Extensions

```csharp
using DKNet.Fw.Extensions;

// Note: Actual encryption classes would need to be examined for exact usage
// This is a conceptual example based on the encryption folder structure

public class UserService
{
    private readonly IStringEncryption _encryption;
    
    public UserService(IStringEncryption encryption)
    {
        _encryption = encryption;
    }
    
    public async Task<User> CreateUserAsync(string email, string password)
    {
        // Encrypt sensitive data before storing
        var encryptedEmail = await _encryption.EncryptAsync(email);
        var hashedPassword = await _encryption.HashAsync(password);
        
        return new User
        {
            Email = encryptedEmail,
            PasswordHash = hashedPassword
        };
    }
}
```

### Advanced Integration Patterns

#### 1. Domain Value Objects

```csharp
using DKNet.Fw.Extensions;

public class Email
{
    private readonly string _value;
    
    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty");
            
        // Use string extensions for validation
        if (!IsValidEmail(value))
            throw new ArgumentException("Invalid email format");
            
        _value = value;
    }
    
    private static bool IsValidEmail(string email)
    {
        // Custom validation using string extensions
        return email.Contains("@") && !email.IsNumber();
    }
    
    public static implicit operator string(Email email) => email._value;
    public override string ToString() => _value;
}
```

#### 2. Generic Repository Pattern

```csharp
using DKNet.Fw.Extensions;

public interface IEntity
{
    int Id { get; set; }
}

public class Repository<T> where T : class, IEntity
{
    public Repository()
    {
        // Validate that T implements IEntity using type extensions
        if (!typeof(T).IsImplementOf(typeof(IEntity)))
        {
            throw new InvalidOperationException($"Type {typeof(T).Name} must implement IEntity");
        }
    }
    
    public async Task<T> GetByIdAsync(int id)
    {
        // Implementation using the validated type
        // ...
    }
}
```

#### 3. Domain Event Discovery

```csharp
using DKNet.Fw.Extensions;
using System.Reflection;

public class EventDispatcher
{
    public void RegisterEventHandlers(Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t.IsImplementOf(typeof(IEventHandler)))
            .ToList();
            
        // Register handlers using service collection extensions
        // services.AsKeyedImplementedInterfaces("EventHandlers", handlerTypes);
    }
}
```

## Best Practices

### 1. Consistent Usage Patterns
- Always use the extensions consistently across your application
- Prefer extension methods over static utility classes for better discoverability

### 2. Performance Considerations
- The extensions are optimized for performance, but be mindful of reflection-based operations in hot paths
- Cache reflection results when possible

### 3. Error Handling
- Extensions provide safe defaults and meaningful error messages
- Always validate inputs when using type extensions

### 4. Testing
- Extensions are pure functions and easily testable
- Mock dependencies when testing code that uses service collection extensions

## Integration with Other DKNet Components

DKNet.Fw.Extensions integrates seamlessly with other DKNet components:

- **EfCore Extensions**: Provides type checking for entity validation
- **Repository Patterns**: Enables generic constraints and type validation
- **Domain Events**: Supports event handler discovery and registration
- **Service Layer**: Provides string processing and validation utilities
- **SlimBus Integration**: Enables message type discovery and registration

---

> 💡 **Pro Tip**: Use the enum extensions with Display attributes to maintain a consistent ubiquitous language throughout your domain model. This helps bridge the gap between technical implementation and business terminology.