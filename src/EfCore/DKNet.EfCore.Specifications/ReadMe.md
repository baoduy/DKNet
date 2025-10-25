# DKNet.EfCore.Specifications

A flexible and reusable implementation of the Specification Pattern for Entity Framework Core, providing clean data
access without the overhead of traditional repository patterns.

## Overview

This library provides a powerful way to build flexible and reusable database queries in .NET projects. Define reusable
filters, includes, and sorting as specification classes, avoiding the problems that come with large, hard-to-maintain
repositories.

## Features

- **Specification Pattern**: Build complex queries using composable specification objects
- **Repository Pattern**: Optional `IRepositorySpec` interface for common CRUD operations
- **Type-Safe Queries**: Strongly-typed query specifications with IntelliSense support
- **Flexible Filtering**: Define reusable filter criteria
- **Include Support**: Eagerly load related entities
- **Sorting**: Support for both ascending and descending ordering
- **Query Filter Control**: Ability to ignore global query filters (e.g., soft delete, multi-tenancy)
- **Projection Support**: Map entities to DTOs using Mapster integration

## Installation

```bash
dotnet add package DKNet.EfCore.Specifications
```

## Core Components

### ISpecification<TEntity>

The base specification interface that defines:

- `FilterQuery`: Filtering function to test each element for a condition
- `IncludeQueries`: Collection of functions describing included entities
- `OrderByQueries`: Functions for ascending ordering
- `OrderByDescendingQueries`: Functions for descending ordering
- `IgnoreQueryFilters`: Flag to ignore global query filters

### Specification<TEntity>

Abstract base class for creating custom specifications:

```csharp
public class ActiveUsersSpecification : Specification<User>
{
    public ActiveUsersSpecification()
    {
        WithFilter(u => u.IsActive);
        AddInclude(u => u.Profile);
        AddOrderBy(u => u.LastName);
    }
}
```

### IRepositorySpec

Interface for repository operations with specification support:

#### Query Operations

- `Query<TEntity>(spec)`: Query entities using a specification
- `Query<TEntity, TModel>(spec)`: Query and project to a model type

#### Transaction Management

- `BeginTransactionAsync()`: Start a new database transaction
- `Entry<TEntity>(entity)`: Access entity change tracking information

#### CRUD Operations

- `AddAsync<TEntity>(entity)`: Add a single entity
- `AddRangeAsync<TEntity>(entities)`: Add multiple entities
- `UpdateAsync<TEntity>(entity)`: Update an entity and handle navigation properties
- `UpdateRangeAsync<TEntity>(entities)`: Update multiple entities
- `Delete<TEntity>(entity)`: Mark entity for deletion
- `DeleteRange<TEntity>(entities)`: Mark multiple entities for deletion
- `SaveChangesAsync()`: Persist all changes to the database

### RepositorySpec<TDbContext>

Concrete implementation of `IRepositorySpec` with:

- Automatic handling of new entities from navigation properties during updates
- Mapster integration for entity-to-DTO projections
- Support for query filters and change tracking

## Usage Examples

### Creating a Specification

```csharp
public class UsersByRoleSpecification : Specification<User>
{
    public UsersByRoleSpecification(string role)
    {
        // Define filter criteria
        WithFilter(u => u.Role == role && u.IsActive);
        
        // Include related entities
        AddInclude(u => u.Profile);
        AddInclude(u => u.Orders);
        
        // Define ordering
        AddOrderBy(u => u.LastName);
        AddOrderBy(u => u.FirstName);
    }
}
```

### Using Specifications with IRepositorySpec

```csharp
public class UserService
{
    private readonly IRepositorySpec _repository;
    
    public UserService(IRepositorySpec repository)
    {
        _repository = repository;
    }
    
    public async Task<List<User>> GetActiveAdmins()
    {
        var spec = new UsersByRoleSpecification("Admin");
        return await _repository.Query(spec).ToListAsync();
    }
    
    public async Task<List<UserDto>> GetActiveAdminsAsDto()
    {
        var spec = new UsersByRoleSpecification("Admin");
        return await _repository.Query<User, UserDto>(spec).ToListAsync();
    }
}
```

### CRUD Operations

```csharp
// Add a new entity
var user = new User { FirstName = "John", LastName = "Doe" };
await _repository.AddAsync(user);
await _repository.SaveChangesAsync();

// Update an entity
user.IsActive = false;
await _repository.UpdateAsync(user);
await _repository.SaveChangesAsync();

// Delete an entity
_repository.Delete(user);
await _repository.SaveChangesAsync();

// Transaction support
using var transaction = await _repository.BeginTransactionAsync();
try
{
    await _repository.AddAsync(newUser);
    await _repository.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### Ignoring Query Filters

```csharp
public class AllUsersIncludingSoftDeletedSpecification : Specification<User>
{
    public AllUsersIncludingSoftDeletedSpecification()
    {
        IgnoreQueryFiltersEnabled();
        AddOrderBy(u => u.CreatedDate);
    }
}
```

### Complex Filtering with PredicateBuilder

```csharp
public class DynamicUserSearchSpecification : Specification<User>
{
    public DynamicUserSearchSpecification(string? name, bool? isActive)
    {
        var predicate = CreatePredicate();
        
        if (!string.IsNullOrEmpty(name))
            predicate = predicate.And(u => u.FirstName.Contains(name) || u.LastName.Contains(name));
            
        if (isActive.HasValue)
            predicate = predicate.And(u => u.IsActive == isActive.Value);
            
        WithFilter(predicate);
    }
}
```

## Dependency Injection Setup

```csharp
services.AddDbContext<MyDbContext>(options => 
    options.UseNpgsql(connectionString));

services.AddScoped<IRepositorySpec, RepositorySpec<MyDbContext>>();

// For projection support, register Mapster
services.AddSingleton<IMapper>(new Mapper());
```

## Benefits

1. **Reusability**: Define query logic once, use it everywhere
2. **Testability**: Easy to unit test specifications in isolation
3. **Maintainability**: Query logic is centralized and versioned
4. **Type Safety**: Compile-time checking of query expressions
5. **Separation of Concerns**: Business logic separate from data access
6. **Performance**: Optimized query execution with proper includes and filtering

## Migration Notes

**Breaking Changes in Latest Version:**

- `AndSpecification` and `OrSpecification` classes have been removed
- Use LinqKit's `PredicateBuilder` for combining filter expressions instead
- All methods in `IRepositorySpec` are now properly documented with XML comments

## References

- Original blog
  post: https://antondevtips.com/blog/specification-pattern-in-ef-core-flexible-data-access-without-repositories
- LinqKit Documentation: https://github.com/scottksmith95/LINQKit
- Mapster Documentation: https://github.com/MapsterMapper/Mapster

## License

This library is part of the DKNet.FW framework.
