---
name: fw-extensions
description: DKNet Framework Extensions - Core utilities, extension methods, and helper functions for .NET applications
license: MIT
---

# Framework Extensions Skill

This skill helps GitHub Copilot generate code using DKNet's Framework Extensions package (`DKNet.Fw.Extensions`) for common utility methods and extension functions.

## 🎯 Package Purpose

`DKNet.Fw.Extensions` provides:
- **String Extensions** - Validation, formatting, and manipulation
- **Type Extensions** - Type checking and conversion utilities
- **Collection Extensions** - LINQ helpers and filtering
- **Validation Helpers** - Common validation patterns
- **Date/Time Utilities** - Date manipulation and formatting

**NuGet Package**: `DKNet.Fw.Extensions`

## 📦 Installation

```bash
dotnet add package DKNet.Fw.Extensions
```

## 🎯 Common Extensions

### String Extensions

```csharp
using DKNet.Fw.Extensions;

// Email validation
var email = "user@example.com";
if (email.IsValidEmail())
{
    // Valid email
}

// URL validation
var url = "https://example.com";
if (url.IsValidUrl())
{
    // Valid URL
}

// Null or whitespace check
if (string.IsNullOrWhiteSpace(name))
{
    // Empty
}

// Truncate string
var truncated = longText.Truncate(100); // Max 100 chars

// To camel case
var camelCase = "HelloWorld".ToCamelCase(); // "helloWorld"

// To pascal case
var pascalCase = "hello_world".ToPascalCase(); // "HelloWorld"

// To snake case
var snakeCase = "HelloWorld".ToSnakeCase(); // "hello_world"

// Remove special characters
var cleaned = dirtyString.RemoveSpecialCharacters();

// Safe substring
var part = text.SafeSubstring(0, 10); // Won't throw if text is shorter
```

### Type Extensions

```csharp
using DKNet.Fw.Extensions;

// Enum conversion with validation
if (typeof(OrderStatus).TryConvertToEnum("Pending", out OrderStatus status))
{
    // status = OrderStatus.Pending
}

// Type checking
if (myObject.IsNumericType())
{
    // It's a number
}

if (typeof(string).IsNullableType())
{
    // Check if nullable
}

// Get default value
var defaultValue = typeof(int).GetDefaultValue(); // 0

// Property exists check
if (typeof(Product).HasProperty("Name"))
{
    // Property exists
}
```

### Collection Extensions

```csharp
using DKNet.Fw.Extensions;

// Remove null or empty strings
var validStrings = strings.WhereNotNullOrEmpty();

// Remove null items
var validItems = items.WhereNotNull();

// Batch processing
foreach (var batch in items.Batch(100))
{
    // Process 100 items at a time
}

// Safe enumeration
foreach (var item in nullableCollection.EmptyIfNull())
{
    // Won't throw if collection is null
}

// Distinct by property
var uniqueProducts = products.DistinctBy(p => p.Name);

// Convert to dictionary safely
var dict = items.ToDictionarySafe(i => i.Id, i => i.Value);
```

### Validation Helpers

```csharp
using DKNet.Fw.Extensions;

// Throw if null
ArgumentNullException.ThrowIfNull(parameter);

// Validate GUID
if (id.IsValidGuid())
{
    var guid = Guid.Parse(id);
}

// Validate email
if (!email.IsValidEmail())
{
    throw new ValidationException("Invalid email");
}

// Range validation
if (age.IsInRange(18, 100))
{
    // Valid age
}
```

## 🎯 Usage Patterns

### Pattern 1: Input Validation
```csharp
public async Task<IActionResult> CreateUser(
    [FromBody] CreateUserRequest request,
    CancellationToken cancellationToken)
{
    // Validate email
    if (!request.Email.IsValidEmail())
    {
        return BadRequest("Invalid email address");
    }
    
    // Validate URL
    if (!string.IsNullOrEmpty(request.Website) && 
        !request.Website.IsValidUrl())
    {
        return BadRequest("Invalid website URL");
    }
    
    // Clean input
    var username = request.Username.RemoveSpecialCharacters();
    
    // Proceed with creation
    var user = await _userService.CreateUserAsync(
        username,
        request.Email,
        cancellationToken);
    
    return Created($"/users/{user.Id}", user);
}
```

### Pattern 2: Safe Enum Parsing
```csharp
public async Task<IActionResult> UpdateOrderStatus(
    Guid orderId,
    string statusString,
    CancellationToken cancellationToken)
{
    // Safe enum conversion
    if (!typeof(OrderStatus).TryConvertToEnum(statusString, out OrderStatus status))
    {
        return BadRequest($"Invalid order status: {statusString}");
    }
    
    await _orderService.UpdateStatusAsync(orderId, status, cancellationToken);
    return Ok();
}
```

### Pattern 3: Collection Processing
```csharp
public async Task<IActionResult> ProcessOrders(
    [FromBody] List<OrderRequest> requests,
    CancellationToken cancellationToken)
{
    // Filter out invalid requests
    var validRequests = requests
        .WhereNotNull()
        .Where(r => !string.IsNullOrWhiteSpace(r.CustomerId))
        .ToList();
    
    // Process in batches of 100
    foreach (var batch in validRequests.Batch(100))
    {
        await _orderService.ProcessBatchAsync(batch, cancellationToken);
    }
    
    return Ok();
}
```

### Pattern 4: String Formatting
```csharp
public class ProductViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public static ProductViewModel FromEntity(Product product)
    {
        return new ProductViewModel
        {
            Id = product.Id,
            Name = product.Name.ToPascalCase(),
            Description = product.Description.Truncate(200) // Max 200 chars
        };
    }
}
```

### Pattern 5: Type-Safe Operations
```csharp
public class GenericService<T>
{
    public T GetDefaultValue()
    {
        // Get default value for type T
        return (T)typeof(T).GetDefaultValue()!;
    }
    
    public bool IsNullableEntity()
    {
        return typeof(T).IsNullableType();
    }
    
    public bool HasIdProperty()
    {
        return typeof(T).HasProperty("Id");
    }
}
```

## 🚨 Critical Rules

### 1. Always Validate User Input
```csharp
// ✅ Good - Validate before use
if (!email.IsValidEmail())
{
    return BadRequest("Invalid email");
}

// ❌ Bad - No validation
var user = new User { Email = email }; // Might be invalid!
```

### 2. Use Safe Methods for Nullable Collections
```csharp
// ✅ Good - Safe enumeration
foreach (var item in collection.EmptyIfNull())
{
    // Safe even if collection is null
}

// ❌ Bad - Null reference exception
foreach (var item in collection) // Crash if null!
{
}
```

### 3. Check Enum Conversion Success
```csharp
// ✅ Good - Check result
if (typeof(Status).TryConvertToEnum(input, out Status status))
{
    // Use status safely
}
else
{
    return BadRequest("Invalid status");
}

// ❌ Bad - No error handling
var status = (Status)Enum.Parse(typeof(Status), input); // Throws!
```

### 4. Use Appropriate String Methods
```csharp
// ✅ Good - Culture-invariant for technical strings
var key = name.ToLowerInvariant();

// ✅ Good - Culture-aware for user-facing strings
var display = name.ToLower();

// ❌ Bad - Culture issues
var key = name.ToLower(); // Might have culture issues
```

## 🚫 Common Mistakes

### 1. Not Handling Null Collections
```csharp
// ❌ Bad
var count = collection.Count(); // Crash if null!

// ✅ Good
var count = collection.EmptyIfNull().Count();
// Or
var count = collection?.Count() ?? 0;
```

### 2. Unsafe Enum Parsing
```csharp
// ❌ Bad - Throws exception
var status = (OrderStatus)Enum.Parse(typeof(OrderStatus), input);

// ✅ Good - Safe parsing
if (!typeof(OrderStatus).TryConvertToEnum(input, out OrderStatus status))
{
    // Handle invalid input
}
```

### 3. String Truncation Without Safety
```csharp
// ❌ Bad - Throws if text is shorter
var truncated = text.Substring(0, 100);

// ✅ Good - Safe truncation
var truncated = text.Truncate(100);
```

## 📝 Real-World Example

```csharp
public class UserService
{
    private readonly IRepository<User> _repository;
    
    public async Task<User> CreateUserAsync(
        string email,
        string? website,
        List<string> tags,
        CancellationToken cancellationToken)
    {
        // Validate email
        if (!email.IsValidEmail())
        {
            throw new ValidationException("Invalid email address");
        }
        
        // Validate URL if provided
        if (!string.IsNullOrEmpty(website) && !website.IsValidUrl())
        {
            throw new ValidationException("Invalid website URL");
        }
        
        // Clean and validate tags
        var cleanTags = tags
            .EmptyIfNull()
            .WhereNotNullOrEmpty()
            .Select(t => t.RemoveSpecialCharacters())
            .Distinct()
            .ToList();
        
        // Create user
        var user = new User
        {
            Email = email.ToLowerInvariant(),
            Website = website,
            Tags = cleanTags
        };
        
        await _repository.AddAsync(user, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        
        return user;
    }
}
```

## 🔗 Related Skills

- `dknet-overview` - Overall architecture
- `efcore-abstractions` - Entity validation
- `aspcore-idempotency` - API patterns

---

**When to Use This Skill**: Reference this skill when working with strings, collections, type conversions, or common utility operations in .NET applications.
