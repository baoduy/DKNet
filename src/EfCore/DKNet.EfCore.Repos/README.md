# DKNet.EfCore.Repos

A collection of repository patterns built on Entity Framework Core (EF Core) designed to simplify data access and business logic separation.

## Overview

DKNet.EfCore.Repos provides a set of reusable repository classes that encapsulate common data access operations. This allows developers to focus on business logic while maintaining consistent and efficient database interactions.

## Features

- **Generic Repository Base Class**: A base class for implementing custom repositories, providing core CRUD (Create, Read, Update, Delete) operations.

- **Custom Repositories**: Pre-built repository classes tailored for specific entity types, offering additional functionality beyond the basic CRUD operations.

- **Unit Of Work Pattern Support**: Integrates seamlessly with the Unit of Work pattern to manage transactions and batch operations efficiently.

- **Query Customization**: Extends EF Core's query capabilities, allowing developers to filter, sort, and paginate data with ease.

## Getting Started

### Installation

To integrate DKNet.EfCore.Repos into your project, install the NuGet package:

```bash
dotnet add package DKNet.EfCore.Repos --version 1.0.0-alpha
```

Replace `--version 1.0.0-alpha` with the appropriate version number when available.

### Basic Usage

Here's a simple example of how to use the repository classes in your EF Core application:

```csharp
public class Program
{
    public static void Main()
    {
        var optionsBuilder = new DbContextOptionsBuilder<YourDbContext>();
        
        // Add repositories to the context configuration
        RepositoriesConfiguration.AddRepositories(optionsBuilder);
        
        var dbContext = new YourDbContext(optionsBuilder.Options);
        
        // Use the repository as needed
        var userRepository = dbContext.GetService<IRepository<User>>();
        var user = await userRepository.GetByIdAsync(1);
        ...
    }
}
```

## Detailed Features

### Generic Repository Base Class

The `GenericRepository` class provides essential CRUD operations and can be extended to add custom functionality.

**Example:**

```csharp
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    // Additional methods as needed
}

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public async Task<User> GetByEmailAsync(string email)
    {
        return await base.GetAll()
            .FirstOrDefaultAsync(u => u.Email == email);
    }
}
```

### Custom Repositories

These are tailored for specific entity types, offering specialized functionality.

**Example:**

```csharp
public interface IProductRepository : IRepository<Product>
{
    Task<List<Product>> GetProductsByCategoryAsync(string category);
}

public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public async Task<List<Product>> GetProductsByCategoryAsync(string category)
    {
        return await base.GetAll()
            .Where(p => p.Category == category)
            .ToListAsync();
    }
}
```

### Unit Of Work Pattern Support

The repository classes integrate with the Unit of Work pattern to manage transactions and batch operations.

**Example:**

```csharp
public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<Product> Products { get; }
    
    Task<int> SaveChangesAsync();
}

public class EfCoreUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
{
    private readonly TContext _context;

    public EfCoreUnitOfWork(TContext context)
    {
        _context = context;
    }

    public IRepository<User> Users => new UserRepository(_context);
    public IRepository<Product> Products => new ProductRepository(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
```

### Query Customization

The repository classes allow for flexible and powerful querying.

**Example:**

```csharp
public interface IProductRepository : IRepository<Product>
{
    Task<List<Product>> GetAllPagedAsync(int pageIndex, int pageSize);
}

public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public async Task<List<Product>>> GetAllPagedAsync(int pageIndex, int pageSize)
    {
        return await base.GetAll()
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
```

## Contributing

Contributions are welcome and encouraged! If you'd like to contribute to the project, please follow these steps:

1. **Fork the repository**: Create your own copy of the project on GitHub.
2. **Create a feature branch**: Develop new features or bug fixes in a dedicated branch.
3. **Commit changes**: Keep your commits clear and descriptive.
4. **Push to the branch**: Share your changes with the remote repository.
5. **Create a Pull Request**: Submit your changes for review and merging.

## License

This project is licensed under [MIT License](LICENSE).

## Acknowledgments

- Thanks to the Entity Framework Core team for providing a robust framework.
- Special thanks to contributors who have enhanced this library.
