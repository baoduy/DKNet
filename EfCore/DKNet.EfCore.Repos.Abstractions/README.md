# WX.EfCore.Repos.Abstractions

A library providing abstraction definitions for repository patterns tailored for Entity Framework Core (EF Core). This package includes interfaces and base classes that define the contract for repository implementations, enabling consistent and reusable data access logic across applications.

## Overview

This project defines the core abstractions needed to implement repository patterns in EF Core. It allows developers to separate concerns by encapsulating data access logic behind well-defined interfaces, promoting maintainable and scalable codebases.

## Features

- **Generic Repository Interfaces**: Declarative contracts for common CRUD operations (Create, Read, Update, Delete).

- **Customizable Abstractions**: Extensible interface definitions that can be adapted to specific domain requirements.

- **Consistent API Design**: Ensures uniformity across repository implementations, making it easier to switch or replace repositories as needed.

## Installation

To use WX.EfCore.Repos.Abstractions in your project, install the NuGet package:

```bash
dotnet add package WX.EfCore.Repos.Abstractions --version 1.0.0-alpha
```

Replace `--version 1.0.0-alpha` with the appropriate version number when available.

## Basic Usage

Here's an example of how to use the provided abstractions in your EF Core applications:

### Defining a Repository Interface

```csharp
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    // Additional methods as needed
}
```

### Implementing the Repository Interface

```csharp
public class UserRepository : IRepository<User>
{
    private readonly YourDbContext _dbContext;

    public UserRepository(YourDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User> GetByIdAsync(int id)
    {
        return await _dbContext.Users.FindAsync(id);
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _dbContext.Users.ToListAsync();
    }
}
```

## Detailed Features

### Generic Repository Interfaces

The provided `IRepository<T>` interface defines the core operations necessary for interacting with a typical entity set in EF Core. This includes methods such as:

- **GetByIdAsync**: Retrieves a single entity by its primary key.

- **GetAllAsync**: Retrieves all entities of a specific type.

These interfaces can be extended or specialized to include additional domain-specific methods, ensuring flexibility while maintaining consistency across different repository implementations.

### Extensible Design

The abstractions are designed to allow for extension through the use of base classes and multiple interface inheritance. This makes it easy to add custom functionality without violating the Liskov Substitution Principle.

### Consistent API Across Implementations

By standardizing on these abstractions, all repositories within your application will expose a familiar set of methods and behaviors. This reduces cognitive overhead for developers and simplifies the process of swapping out or updating repository implementations as needed.

## Contributing

Contributions are welcome! If you'd like to contribute to this project:

1. Fork the repository on GitHub.
2. Create a feature branch for your changes.
3. Commit your changes with clear, descriptive messages.
4. Push your branch back to GitHub and create a Pull Request.

Please ensure that any new features or changes adhere to the existing coding standards and design principles of the project.

## License

This project is licensed under the [MIT License](LICENSE).

## Acknowledgments

- Thanks to the Entity Framework Core team for providing a robust framework upon which this library is built.
- Special thanks to contributors who have enhanced the abstractions in this library.