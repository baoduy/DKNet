# WX.EfCore.Abstraction README

Welcome to **WX.EfCore.Abstraction**! This project provides a convenient abstraction layer for EF Core, designed to streamline common tasks and enhance maintainability and testability in your applications.

## Project Overview

This abstraction layer is built on top of EF Core, offering a set of helper classes that abstract away complex aspects of data access. It simplifies everyday operations, allowing developers to focus on business logic while handling entity configuration, repository patterns, and caching seamlessly.

## Purpose

Our goal is to make working with EF Core more efficient and less error-prone. The abstraction layer ensures that your code remains clean, easy to understand, and straightforward to maintain.

## Features

- **Common Repository Operations**: Handles CRUD (Create, Read, Update, Delete) operations.
- **Entity Configuration Management**: Simplifies entity configuration by wrapping complex setups into reusable classes.
- **Transaction Handling**: Manages transactions efficiently, reducing boilerplate code.
- **Caching Support**: Facilitates caching strategies to improve performance and reduce the database load.

## Requirements

- **.NET Version**: 3.0 or higher
- **EF Core Version**: 7.x or later
- **NuGet Package**: Install [WX.EfCore.Abstraction](https://www.nuget.org/packages/WX.EfCore.Abstraction)

## Installation

1. Clone the repository.
2. Open your project in Visual Studio.
3. Add the NuGet package to your project.

## Usage

Integrate the abstraction layer into your application by using the provided repository classes. Here's an example snippet:

```csharp
using WX.EfCore.Abstraction;

public class MyEntity : EntityBase<MyEntity>
{
    // Define entity properties and configurations in derived classes
}

public class MyRepository<T> : Repository<T, MyContext>
{
    public async Task<T> GetById(int id)
    {
        return await _repository.GetByIdAsync(id);
    }
}
```

---

## Example

```csharp
using WX.EfCore.Abstraction;

// Initialize the context with your EF Core configuration
var dbContext = new MyDbContext();
dbContext.Configure();

// Use the repository to perform operations
var repo = new MyRepository<MyEntity>(dbContext);

// Create an entity and save changes
MyEntity entity = new()
{
    Name = "John"
};
repo.AddAsync(entity);
await dbContext.SaveChangesAsync();
```
---

## Contributing

Contributions are welcome! Here's how you can help:

1. Fork the repository.
2. Create a feature branch for your changes.
3. Commit your code with meaningful messages.
4. Add tests to cover your changes.
5. Submit a pull request.

Please follow our [Code of Conduct](CONDUCT.md) and ensure your contributions are in line with the project's scope.
