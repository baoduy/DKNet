# DKNet.EfCore.Specifications

A powerful and flexible specification pattern implementation for Entity Framework Core with dynamic LINQ query support.

## Features

- 🎯 **Specification Pattern** - Build reusable, composable query logic
- ⚡ **Dynamic LINQ Queries** - Runtime query construction using string expressions
- 🔗 **Fluent API** - Chainable methods for building complex queries
- 🛠️ **Type-Safe Operations** - Strongly-typed filter operations
- 📊 **LinqKit Integration** - Combine static and dynamic predicates seamlessly

## Installation

```bash
dotnet add package DKNet.EfCore.Specifications
```

## Quick Start

### 1. Basic Specification Usage

```csharp
public class ActivePersonsSpec : Specification<Person>
{
    public ActivePersonsSpec()
    {
        WithFilter(p => p.IsActive && !p.IsDeleted);
        AddInclude(p => p.Address);
        AddOrderBy(p => p.Name);
    }
}

// Usage
var spec = new ActivePersonsSpec();
var persons = await repository.ListAsync(spec);
```

### 2. Dynamic Filter Queries

```csharp
public class PersonSearchSpec : Specification<Person>
{
    public PersonSearchSpec(int minAge, string nameContains)
    {
        // Add dynamic filters
        AddFilter("Age", FilterOperations.GreaterThanOrEqual, minAge);
        AddFilter("Name", FilterOperations.Contains, nameContains);
        
        // Mix with static filters
        WithFilter(p => !p.IsDeleted);
    }
}

// Usage
var spec = new PersonSearchSpec(18, "John");
var results = await repository.ListAsync(spec);
```

### 3. Dynamic Predicate Builder

The `DynamicPredicateBuilder` provides a fluent API for building complex dynamic queries:

```csharp
var builder = new DynamicPredicateBuilder()
    .With("Age", Operation.GreaterThan, 18)
    .With("Department.Name", Operation.Equal, "Engineering")
    .With("Salary", Operation.GreaterThanOrEqual, 50000)
    .With("Name", Operation.Contains, "Smith");

var predicate = builder.Build(out var values);
// Result: "Age > @0 and Department.Name == @1 and Salary >= @2 and Name.Contains(@3)"
// values: [18, "Engineering", 50000, "Smith"]

// Use with EF Core
var employees = await context.Employees
    .Where(predicate, values)
    .ToListAsync();
```

### 4. Dynamic LINQ Extensions

Combine static predicates with dynamic expressions using LinqKit:

```csharp
var predicate = PredicateBuilder.New<Person>()
    .And(p => p.IsActive)
    .And("Age > @0 and Salary >= @1", 25, 60000)
    .Or("Department == @0", "Executive");

// Result: (IsActive AND (Age > 25 and Salary >= 60000)) OR (Department == "Executive")

var query = context.Persons.Where(predicate);
```

## Available Operations

The `Operation` enum supports the following filter operations:

| Operation            | Description                | Example                    |
|----------------------|----------------------------|----------------------------|
| `Equal`              | Equality comparison (==)   | `"Age == @0"`              |
| `NotEqual`           | Inequality comparison (!=) | `"Status != @0"`           |
| `GreaterThan`        | Greater than (>)           | `"Salary > @0"`            |
| `GreaterThanOrEqual` | Greater than or equal (>=) | `"Age >= @0"`              |
| `LessThan`           | Less than (<)              | `"Price < @0"`             |
| `LessThanOrEqual`    | Less than or equal (<=)    | `"Score <= @0"`            |
| `Contains`           | String contains            | `"Name.Contains(@0)"`      |
| `NotContains`        | String does not contain    | `"!Name.Contains(@0)"`     |
| `StartsWith`         | String starts with         | `"Name.StartsWith(@0)"`    |
| `EndsWith`           | String ends with           | `"Name.EndsWith(@0)"`      |
| `Any`                | Collection any             | `"Tags.Any(x => x == @0)"` |
| `All`                | Collection all             | `"Tags.All(x => x == @0)"` |

## Advanced Scenarios

### API Controller with Dynamic Queries

```csharp
[HttpGet]
public async Task<IActionResult> SearchEmployees(
    [FromQuery] string? filter,
    [FromQuery] string? orderBy,
    [FromQuery] int minSalary = 0)
{
    var spec = new Specification<Employee>();
    
    // Always filter deleted records
    spec.WithFilter(e => !e.IsDeleted);
    
    // Add dynamic filters from query string
    if (!string.IsNullOrEmpty(filter))
    {
        var builder = new DynamicPredicateBuilder()
            .With("Salary", Operation.GreaterThanOrEqual, minSalary);
        
        var predicate = builder.Build(out var values);
        // Apply to specification using the extension method
    }
    
    // Dynamic ordering
    if (!string.IsNullOrEmpty(orderBy))
    {
        spec.WithOrderBy(orderBy, isDescending: false);
    }
    
    var employees = await _repository.ListAsync(spec);
    return Ok(employees);
}
```

### Nested Property Filtering

```csharp
var builder = new DynamicPredicateBuilder()
    .With("Address.City", Operation.Equal, "New York")
    .With("Department.Manager.Name", Operation.StartsWith, "John")
    .With("Projects.Any(x => x.IsActive)", Operation.Equal, true);

var predicate = builder.Build(out var values);
```

### Combining Multiple Specifications

```csharp
public class EmployeeSearchSpec : Specification<Employee>
{
    public EmployeeSearchSpec(
        string? department = null,
        int? minAge = null,
        decimal? minSalary = null)
    {
        // Base filter
        WithFilter(e => e.IsActive);
        
        // Dynamic filters
        if (!string.IsNullOrEmpty(department))
        {
            AddFilter("Department.Name", FilterOperations.Equal, department);
        }
        
        if (minAge.HasValue)
        {
            AddFilter("Age", FilterOperations.GreaterThanOrEqual, minAge.Value);
        }
        
        if (minSalary.HasValue)
        {
            AddFilter("Salary", FilterOperations.GreaterThanOrEqual, minSalary.Value);
        }
        
        // Includes
        AddInclude(e => e.Department);
        AddInclude(e => e.Address);
        
        // Ordering
        AddOrderBy(e => e.Department);
        AddOrderByDescending(e => e.Salary);
    }
}
```

## Best Practices

1. **Reusability**: Create named specification classes for common query patterns
2. **Composition**: Build complex queries by combining simple specifications
3. **Type Safety**: Prefer static predicates when possible; use dynamic queries for runtime flexibility
4. **Performance**: Use `AddInclude()` wisely to avoid N+1 queries
5. **Security**: Validate and sanitize user input before using in dynamic queries

## Requirements

- .NET 9.0 or higher
- Entity Framework Core 9.0 or higher
- LinqKit.Microsoft.EntityFrameworkCore
- System.Linq.Dynamic.Core

## License

Licensed under the MIT License. See LICENSE in the project root for license information.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

