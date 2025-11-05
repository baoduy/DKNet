# System Patterns

## Architecture Patterns

### 1. Specification Pattern (Primary Pattern)
**Purpose**: Encapsulate query logic in reusable, composable specifications.

**Implementation**:
```csharp
// Abstract base class
public abstract class Specification<TEntity> 
{
    public abstract Expression<Func<TEntity, bool>>? Criteria { get; }
    public List<Expression<Func<TEntity, object>>> Includes { get; } = [];
    public List<(Expression<Func<TEntity, object>> KeySelector, bool Descending)> OrderBy { get; } = [];
}

// Concrete specification
public class ActiveProductsSpecification : Specification<Product>
{
    public override Expression<Func<Product, bool>> Criteria => p => p.IsActive;
}

// Usage with dynamic predicates
var predicate = PredicateBuilder.New<Product>()
    .And(p => p.IsActive)
    .DynamicAnd(builder => builder
        .With("Price", FilterOperations.GreaterThan, 100m)
        .With("StockQuantity", FilterOperations.GreaterThan, 0));
```

**Key Features**:
- Composable with `And()` and `Or()` using LinqKit
- Support for includes (eager loading)
- Support for ordering
- Dynamic predicate building with type safety
- Null-safe: returns original predicate if dynamic expression is null/empty

### 2. Dynamic Predicate Builder Pattern
**Purpose**: Build complex EF Core queries from runtime conditions without losing type safety.

**Components**:
- `DynamicPredicateBuilder<TEntity>`: Fluent builder for filter conditions
- `FilterOperations`: Enum defining supported operations (Equal, GreaterThan, Contains, etc.)
- Extension methods: `DynamicAnd()`, `DynamicOr()`

**Key Algorithms**:

#### Property Type Resolution
```csharp
private static Type? ResolvePropertyType(Type entityType, string propertyPath)
{
    var props = propertyPath.Split('.');
    var currentType = entityType;
    
    foreach (var prop in props)
    {
        var normalizedName = NormalizePropertyName(prop, currentType);
        var propInfo = currentType.GetProperty(normalizedName, 
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propInfo == null) return null;
        currentType = propInfo.PropertyType;
    }
    return currentType;
}
```

#### Operation Adjustment for Value Types
- **String types**: All operations supported (Contains, StartsWith, etc.)
- **Numeric/DateTime/Enum types**: Only comparison operations (Equal, GreaterThan, etc.)
  - Contains/NotContains → Equal/NotEqual
  - StartsWith/EndsWith → Equal
- **Enum validation**: Values are validated and invalid values are skipped silently

#### Null Handling in SQL
```csharp
// For nullable properties
if (val == null)
{
    clause = op == FilterOperations.Equal 
        ? $"{normalizedProp} IS NULL" 
        : $"{normalizedProp} IS NOT NULL";
}
```

### 3. Repository Pattern
**Purpose**: Abstract data access layer and provide testable boundaries.

**Key Principles**:
- Generic repository with specification support
- Async/await for all database operations
- Return `IQueryable<T>` for flexible composition
- Unit of Work through DbContext
- No business logic in repositories

**Example**:
```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> ListAsync(Specification<T> spec);
    IQueryable<T> ApplySpecification(Specification<T> spec);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
```

### 4. Builder Pattern
**Purpose**: Create complex objects with fluent API.

**Used In**:
- `DynamicPredicateBuilder<T>`: Build filter expressions
- `ExpressionStarter<T>`: Compose LINQ expressions
- Test data builders using Bogus

**Conventions**:
- Method chaining (return `this` or builder)
- Descriptive method names (`With`, `And`, `Or`)
- Build/Execute method to finalize

### 5. Factory Pattern
**Purpose**: Create instances with complex initialization logic.

**Used In**:
- Service factories for scoped dependencies
- Test fixture factories (e.g., `TestDbFixture`)
- Dynamic expression parsing

## Coding Conventions

### Naming Conventions
- **Classes**: `PascalCase` (e.g., `DynamicPredicateBuilder`)
- **Interfaces**: `IPascalCase` (e.g., `IBackgroundTask`)
- **Methods**: `PascalCase` with verb prefix (e.g., `GetByIdAsync`, `CreateExpression`)
- **Properties**: `PascalCase` (e.g., `FilterOperations`, `IsActive`)
- **Private Fields**: `_camelCase` with underscore prefix (e.g., `_conditions`, `_db`)
- **Parameters**: `camelCase` (e.g., `entityType`, `propertyName`)
- **Local Variables**: `camelCase` (e.g., `predicate`, `results`)

### Property Name Normalization
```csharp
// Supports camelCase, PascalCase, snake_case input
public static string NormalizePropertyName(string propertyName, Type type)
{
    // Convert to PascalCase (C# standard)
    // Match case-insensitively against actual properties
}
```

### File Organization
```
ProjectName/
├── Abstractions/          # Interfaces and abstract classes
├── Extensions/            # Extension methods
├── Specifications/        # Specification implementations
├── Models/               # Entity classes
├── Services/             # Business logic
└── README.md            # Project documentation
```

### Test Organization
```
ProjectName.Tests/
├── Fixtures/             # Test fixtures (TestDbFixture)
├── TestObjects/          # Test entities and helpers
├── Unit/                # Unit tests (fast, isolated)
├── Integration/         # Integration tests (TestContainers)
└── GlobalUsings.cs      # Global using directives
```

## Common Patterns in Tests

### TestContainers Pattern
```csharp
public class TestDbFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    public TestDbContext? Db { get; private set; }

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder()
            .WithPassword("YourStrong@Passw0rd")
            .Build();
        await _container.StartAsync();
        
        // Create DbContext and seed data
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
```

### Test Data Generation with Bogus
```csharp
var faker = new Faker();
var products = Enumerable.Range(1, 20).Select(i => new Product
{
    Name = faker.Commerce.ProductName(),
    Price = faker.Random.Decimal(10, 1000),
    IsActive = faker.Random.Bool(0.7f) // 70% active
}).ToList();
```

### Arrange-Act-Assert Pattern
```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var predicate = PredicateBuilder.New<Product>()
        .And(p => p.IsActive);
    
    // Act
    predicate = predicate.DynamicAnd(builder => 
        builder.With("Price", FilterOperations.GreaterThan, 100m));
    var results = _db.Products.Where(predicate).ToList();
    
    // Assert
    results.ShouldNotBeEmpty();
    results.ShouldAllBe(p => p.IsActive && p.Price > 100m);
}
```

## Error Handling Patterns

### Null-Safe Operations
- Return original value if operation cannot be performed
- Use nullable reference types (`?`) to indicate optional returns
- Avoid throwing exceptions for expected scenarios (e.g., invalid enum values)

### Validation Pattern
```csharp
// Skip invalid conditions silently
if (!IsValidEnumValue(val, enumType))
    continue; // Skip this condition, don't throw

// Validate and throw for programmer errors
if (!entityType.IsEnumType())
    throw new ArgumentException("Type must be an enum", nameof(entityType));
```

## Performance Patterns

### Query Optimization
1. **Use `.AsNoTracking()`** for read-only queries
2. **Use `.AsExpandable()`** when using LinqKit expressions
3. **Avoid N+1 queries**: Use `.Include()` or projections
4. **Project early**: Select only needed fields
5. **Filter on database**: Push predicates to SQL (avoid `.ToList()` then `.Where()`)

### Example Optimization
```csharp
// ❌ Bad: Loads all data then filters in memory
var results = _db.Products.ToList()
    .Where(p => p.Price > 100m);

// ✅ Good: Filters in database
var results = _db.Products
    .AsNoTracking()
    .Where(p => p.Price > 100m)
    .ToList();

// ✅ Better: With projection
var results = _db.Products
    .AsNoTracking()
    .Where(p => p.Price > 100m)
    .Select(p => new { p.Id, p.Name, p.Price })
    .ToList();
```

## Documentation Patterns

### XML Documentation
All public APIs must have XML documentation:
```csharp
/// <summary>
///     Brief description of what the method does.
/// </summary>
/// <typeparam name="T">Description of type parameter</typeparam>
/// <param name="paramName">Description of parameter</param>
/// <returns>Description of return value</returns>
/// <exception cref="ExceptionType">When this exception is thrown</exception>
/// <example>
///     <code>
///     var example = new Example();
///     example.DoSomething();
///     </code>
/// </example>
```

### File Headers
All source files must include copyright header:
```csharp
// <copyright file="FileName.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>
```
