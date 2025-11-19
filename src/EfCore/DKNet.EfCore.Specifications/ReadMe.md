# DKNet.EfCore.Specifications

A powerful and flexible specification pattern implementation for Entity Framework Core with dynamic LINQ query support
powered by System.Linq.Dynamic.Core and LinqKit.

## Features

- 🎯 **Specification Pattern** - Build reusable, composable query logic with `Specification<TEntity>`
- ⚡ **Dynamic LINQ Queries** - Runtime query construction using System.Linq.Dynamic.Core
- 🔗 **Fluent API** - Chainable `DynamicAnd()` and `DynamicOr()` extension methods
- 🛠️ **Type-Safe Operations** - Strongly-typed filter operations with automatic type handling
- 📊 **LinqKit Integration** - Seamlessly combine static and dynamic predicates with `PredicateBuilder`
- 🔄 **Property Name Normalization** - Supports camelCase, snake_case, kebab-case, and PascalCase
- 🎭 **Model Projections** - `ModelSpecification<TEntity, TModel>` for DTO/projection scenarios
- 🌊 **Async Streaming** - Page-based async enumeration for large result sets
- ✅ **Null-Safe** - Proper handling of nullable properties and null values in SQL
- 🔍 **Enum Validation** - Automatic enum type validation and conversion

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
        // Static filter
        WithFilter(p => p.IsActive && !p.IsDeleted);
        
        // Include related entities
        AddInclude(p => p.Address);
        
        // Add ordering
        AddOrderBy(p => p.Name);
    }
}

// Usage with repository
var spec = new ActivePersonsSpec();
var persons = await repository.ToListAsync(spec);
```

### 2. Dynamic Predicates with DynamicAnd/DynamicOr

Build dynamic queries at runtime using fluent extension methods:

```csharp
public class PersonSearchSpec : Specification<Person>
{
    public PersonSearchSpec(int? minAge, string? nameContains, string? department)
    {
        // Start with base predicate
        var predicate = PredicateBuilder.New<Person>(p => !p.IsDeleted);
        
        // Add dynamic filters conditionally
        if (minAge.HasValue)
            predicate = predicate.DynamicAnd("Age", DynamicOperations.GreaterThanOrEqual, minAge);
            
        if (!string.IsNullOrEmpty(nameContains))
            predicate = predicate.DynamicAnd("Name", DynamicOperations.Contains, nameContains);
            
        if (!string.IsNullOrEmpty(department))
            predicate = predicate.DynamicOr("Department.Name", DynamicOperations.Equal, department);
        
        WithFilter(predicate);
    }
}

// Usage
var spec = new PersonSearchSpec(minAge: 18, nameContains: "John", department: null);
var results = await repository.ToListAsync(spec);
```

### 3. Property Name Normalization

Property names are automatically normalized to PascalCase, supporting multiple naming conventions:

```csharp
var predicate = PredicateBuilder.New<Employee>()
    // All of these are equivalent and normalize to "FirstName"
    .DynamicAnd("firstName", DynamicOperations.Equal, "John")      // camelCase
    .DynamicAnd("first_name", DynamicOperations.Equal, "John")     // snake_case
    .DynamicAnd("first-name", DynamicOperations.Equal, "John")     // kebab-case
    .DynamicAnd("FirstName", DynamicOperations.Equal, "John");     // PascalCase

// Nested properties also supported
predicate = predicate.DynamicAnd("address.city", DynamicOperations.Equal, "New York");
// Normalizes to: Address.City
```

### 4. Model Specifications for Projections

Use `ModelSpecification<TEntity, TModel>` for scenarios involving DTOs or projections:

```csharp
public class EmployeeListSpec : ModelSpecification<Employee, EmployeeDto>
{
    public EmployeeListSpec(string? departmentFilter)
    {
        var predicate = PredicateBuilder.New<Employee>(e => e.IsActive);
        
        if (!string.IsNullOrEmpty(departmentFilter))
            predicate = predicate.DynamicAnd("Department.Name", DynamicOperations.Equal, departmentFilter);
        
        WithFilter(predicate);
        AddOrderBy(e => e.LastName);
        AddOrderBy(e => e.FirstName);
    }
}

// Usage with automatic projection (using Mapster or AutoMapper)
var spec = new EmployeeListSpec("Engineering");
var dtos = await repository.ToListAsync<Employee, EmployeeDto>(spec);
```

## Available Operations

The `DynamicOperations` enum supports the following operations:

| Operation            | Description                | Example Usage                                    | Auto-Conversion |
|----------------------|----------------------------|--------------------------------------------------|-----------------|
| `Equal`              | Equality comparison (==)   | `.DynamicAnd("Age", Equal, 25)`                  | -               |
| `NotEqual`           | Inequality comparison (!=) | `.DynamicAnd("Status", NotEqual, "Inactive")`    | -               |
| `GreaterThan`        | Greater than (>)           | `.DynamicAnd("Salary", GreaterThan, 50000)`      | -               |
| `GreaterThanOrEqual` | Greater than or equal (>=) | `.DynamicAnd("Age", GreaterThanOrEqual, 18)`     | -               |
| `LessThan`           | Less than (<)              | `.DynamicAnd("Price", LessThan, 100)`            | -               |
| `LessThanOrEqual`    | Less than or equal (<=)    | `.DynamicAnd("Score", LessThanOrEqual, 100)`     | -               |
| `Contains`           | String contains            | `.DynamicAnd("Name", Contains, "Smith")`         | → `Equal`*      |
| `NotContains`        | String does not contain    | `.DynamicAnd("Email", NotContains, "spam")`      | → `NotEqual`*   |
| `StartsWith`         | String starts with         | `.DynamicAnd("Phone", StartsWith, "+1")`         | → `Equal`*      |
| `EndsWith`           | String ends with           | `.DynamicAnd("Email", EndsWith, "@company.com")` | → `Equal`*      |

**\* Auto-Conversion:** For non-string types (int, enum, bool, double, etc.), string operations are automatically
converted to equality operations.

### Null Value Handling

The library properly handles null values in SQL queries:

```csharp
// NULL equality check
predicate = predicate.DynamicAnd("MiddleName", DynamicOperations.Equal, null);
// SQL: WHERE [MiddleName] IS NULL

// NULL inequality check
predicate = predicate.DynamicAnd("MiddleName", DynamicOperations.NotEqual, null);
// SQL: WHERE [MiddleName] IS NOT NULL
```

### Enum Validation

Enum properties are automatically validated. Only `Equal` and `NotEqual` operations are supported for enums:

```csharp
public enum Status { Active, Inactive, Pending }

// Valid enum operations
predicate = predicate.DynamicAnd("Status", DynamicOperations.Equal, Status.Active);
predicate = predicate.DynamicAnd("Status", DynamicOperations.NotEqual, Status.Inactive);

// Invalid enum values are ignored (predicate remains unchanged)
predicate = predicate.DynamicAnd("Status", DynamicOperations.Equal, "InvalidValue");

// Contains/StartsWith/EndsWith are auto-converted to Equal for enums
predicate = predicate.DynamicAnd("Status", DynamicOperations.Contains, Status.Active);
// Automatically becomes: Equal operation
```

## Advanced Scenarios

### Multi-Field Search with OR Logic

```csharp
public class ProductSearchSpec : Specification<Product>
{
    public ProductSearchSpec(string searchTerm)
    {
        var predicate = PredicateBuilder.New<Product>(true);
        
        // Search across multiple fields using OR
        predicate = predicate
            .DynamicOr("Name", DynamicOperations.Contains, searchTerm)
            .DynamicOr("Description", DynamicOperations.Contains, searchTerm)
            .DynamicOr("SKU", DynamicOperations.Equal, searchTerm);
        
        WithFilter(predicate);
    }
}
```

### Complex AND/OR Combinations

```csharp
public class EmployeeFilterSpec : Specification<Employee>
{
    public EmployeeFilterSpec(string? department, decimal? minSalary, bool includeInactive)
    {
        var predicate = PredicateBuilder.New<Employee>(true);
        
        // Department filter (OR across multiple departments)
        if (!string.IsNullOrEmpty(department))
        {
            var deptPredicate = PredicateBuilder.New<Employee>(false);
            foreach (var dept in department.Split(','))
            {
                deptPredicate = deptPredicate.DynamicOr("Department.Name", DynamicOperations.Equal, dept.Trim());
            }
            predicate = predicate.And(deptPredicate);
        }
        
        // Salary filter (AND)
        if (minSalary.HasValue)
            predicate = predicate.DynamicAnd("Salary", DynamicOperations.GreaterThanOrEqual, minSalary);
        
        // Active status (AND)
        if (!includeInactive)
            predicate = predicate.DynamicAnd("IsActive", DynamicOperations.Equal, true);
        
        WithFilter(predicate);
    }
}
```

### API Controller with Dynamic Queries

```csharp
[HttpGet]
public async Task<IActionResult> SearchEmployees(
    [FromQuery] string? search,
    [FromQuery] string? department,
    [FromQuery] string? orderBy,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    var spec = new ModelSpecification<Employee, EmployeeDto>();
    
    var predicate = PredicateBuilder.New<Employee>(e => !e.IsDeleted);
    
    // Dynamic search
    if (!string.IsNullOrEmpty(search))
    {
        predicate = predicate
            .DynamicOr("FirstName", DynamicOperations.Contains, search)
            .DynamicOr("LastName", DynamicOperations.Contains, search)
            .DynamicOr("Email", DynamicOperations.Contains, search);
    }
    
    // Department filter
    if (!string.IsNullOrEmpty(department))
        predicate = predicate.DynamicAnd("Department.Name", DynamicOperations.Equal, department);
    
    spec.WithFilter(predicate);
    
    // Dynamic ordering (supports camelCase, snake_case, etc.)
    if (!string.IsNullOrEmpty(orderBy))
        spec.AddOrderBy(orderBy, ListSortDirection.Ascending);
    else
        spec.AddOrderBy(e => e.LastName);
    
    var pagedResults = await _repository.ToPagedListAsync<Employee, EmployeeDto>(spec, page, pageSize);
    
    return Ok(new
    {
        items = pagedResults,
        totalCount = pagedResults.TotalItemCount,
        pageCount = pagedResults.PageCount
    });
}
```

### Nested Property Filtering

```csharp
var spec = new Specification<Order>();
var predicate = PredicateBuilder.New<Order>(true)
    // Nested property access
    .DynamicAnd("Customer.Address.City", DynamicOperations.Equal, "New York")
    .DynamicAnd("Customer.Address.ZipCode", DynamicOperations.StartsWith, "100")
    
    // Multi-level navigation
    .DynamicAnd("OrderItems.Product.Category.Name", DynamicOperations.Equal, "Electronics")
    
    // Combination with static predicates
    .And(o => o.OrderDate >= DateTime.Today.AddDays(-30));

spec.WithFilter(predicate);
```

### Async Streaming for Large Result Sets

```csharp
var spec = new ModelSpecification<Product, ProductDto>();
spec.WithFilter(p => p.IsActive);
spec.AddOrderBy(p => p.Id);

// Stream results page-by-page
await foreach (var product in _repository.PageAsync<Product, ProductDto>(spec))
{
    // Process each product without loading entire result set into memory
    await ProcessProductAsync(product);
}
```

## Repository Extensions

The library provides rich extension methods for `IRepositorySpec`:

### Entity-Only Operations

```csharp
// Count
int count = await repository.CountAsync(spec);

// Any
bool hasResults = await repository.AnyAsync(spec);

// First
Employee employee = await repository.FirstAsync(spec);

// First or default
Employee? maybeEmployee = await repository.FirstOrDefaultAsync(spec);

// List
IList<Employee> employees = await repository.ToListAsync(spec);

// Paged list
IPagedList<Employee> pagedEmployees = await repository.ToPagedListAsync(spec, pageNumber: 1, pageSize: 20);

// Async enumeration
await foreach (var emp in repository.PageAsync(spec))
{
    // Process
}

// Get raw query (useful for debugging)
IQueryable<Employee> query = repository.Query(spec);
string sql = query.ToQueryString();
```

### Model Projection Operations

```csharp
// First or default with projection
EmployeeDto? dto = await repository.FirstOrDefaultAsync<Employee, EmployeeDto>(spec);

// List with projection
IList<EmployeeDto> dtos = await repository.ToListAsync<Employee, EmployeeDto>(spec);

// Paged list with projection
IPagedList<EmployeeDto> pagedDtos = await repository.ToPagedListAsync<Employee, EmployeeDto>(spec, 1, 20);

// Async enumeration with projection
await foreach (var dto in repository.PageAsync<Employee, EmployeeDto>(spec))
{
    // Process
}

// Get raw projected query
IQueryable<EmployeeDto> query = repository.Query<Employee, EmployeeDto>(spec);
```

## Best Practices

1. **Reusability** - Create named specification classes for common query patterns
2. **Composition** - Build complex queries by combining simple predicates using `And()` and `Or()`
3. **Type Safety** - The library automatically handles type conversions and validates enum values
4. **Null Safety** - Null values are handled correctly (translates to `IS NULL` / `IS NOT NULL` in SQL)
5. **Property Naming** - Use any naming convention you prefer; it will be normalized to PascalCase
6. **Performance** - Use `ModelSpecification<TEntity, TModel>` with projections to reduce data transfer
7. **Large Result Sets** - Use `PageAsync()` for streaming or `ToPagedListAsync()` for pagination
8. **Debugging** - Use `.ToQueryString()` on the query to see generated SQL

## Type-Specific Behavior

### String Properties

- All operations supported: `Equal`, `NotEqual`, `Contains`, `NotContains`, `StartsWith`, `EndsWith`, comparison
  operators
- Null values handled correctly with `IS NULL` / `IS NOT NULL`

### Numeric Properties (int, long, decimal, double, etc.)

- Comparison operations: `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`
- String operations (`Contains`, etc.) auto-converted to `Equal`

### Enum Properties

- Only `Equal` and `NotEqual` supported
- Invalid enum values are ignored (no exception thrown)
- Automatic enum validation and conversion
- String operations auto-converted to `Equal`

### Boolean Properties

- `Equal` and `NotEqual` operations
- String operations auto-converted to `Equal`

### Nullable Types

- Full support for nullable reference types and nullable value types
- Null comparisons translate to proper SQL (`IS NULL` / `IS NOT NULL`)

## Requirements

- .NET 9.0 or higher
- Entity Framework Core 9.0 or higher
- LinqKit.Microsoft.EntityFrameworkCore 8.1.0+
- System.Linq.Dynamic.Core 1.4.0+
- X.PagedList 9.2.0+ (for pagination)

## License

Licensed under the MIT License. See LICENSE in the project root for license information.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues, questions, or feature requests, please open an issue on the GitHub repository.

