# Getting Started with DKNet Framework

Welcome to DKNet Framework! This guide will help you get started with the comprehensive .NET framework designed to enhance enterprise application development using Domain-Driven Design (DDD) principles.

## 📋 Prerequisites

- **.NET 9.0 SDK** or later
- **Visual Studio 2022**, **Visual Studio Code**, or **JetBrains Rider**
- **SQL Server** or **SQL Server LocalDB** (for EF Core features)
- Basic understanding of **Domain-Driven Design** concepts

## 🚀 Quick Start

### 1. Installation

Choose the packages you need based on your requirements:

```bash
# Core Framework Extensions
dotnet add package DKNet.Fw.Extensions

# Entity Framework Core Extensions (full suite)
dotnet add package DKNet.EfCore.Extensions
dotnet add package DKNet.EfCore.Repos
dotnet add package DKNet.EfCore.Hooks

# Messaging & CQRS
dotnet add package DKNet.SlimBus.Extensions

# Blob Storage Services
dotnet add package DKNet.Svc.BlobStorage.Abstractions
# Choose your storage provider:
dotnet add package DKNet.Svc.BlobStorage.AzureStorage
# OR
dotnet add package DKNet.Svc.BlobStorage.AwsS3
# OR
dotnet add package DKNet.Svc.BlobStorage.Local
```

### 2. Project Template (Recommended)

For a complete reference implementation, use the SlimBus API template:

```bash
# Clone the template
git clone https://github.com/baoduy/DKNet.git
cd DKNet/src/Templates/SlimBus.ApiEndpoints

# Restore and run
dotnet restore
dotnet run --project SlimBus.Api
```

### 3. Basic Setup

Here's a minimal setup for a new project using DKNet:

```csharp
using DKNet.Fw.Extensions;
using DKNet.EfCore.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add DKNet services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddDKNetRepositories<AppDbContext>();

var app = builder.Build();

// Configure pipeline
app.UseRouting();
app.MapControllers();

app.Run();
```

## 🏗️ Architecture Overview

DKNet follows the **Onion Architecture** pattern with clear separation of concerns:

```
🌐 Presentation Layer (API Controllers, UI)
    ↓
🎯 Application Layer (Services, CQRS Handlers)
    ↓
💼 Domain Layer (Entities, Business Logic)
    ↓
🗄️ Infrastructure Layer (Data Access, External Services)
```

## 📚 Next Steps

1. **[Choose Your Components](README.md)** - Review available packages
2. **[Architecture Guide](Architecture.md)** - Understand DDD/Onion patterns
3. **[Configuration](Configuration.md)** - Setup and configuration options
4. **[Examples](Examples/README.md)** - Practical implementation examples
5. **[API Reference](API-Reference.md)** - Detailed API documentation

## 🎯 Common Use Cases

### Building a CRUD API with CQRS
Perfect for implementing clean architecture with command/query separation.

### Domain Event Handling
Implement event-driven architecture with built-in domain events.

### Data Authorization
Row-level security and data filtering based on user context.

### Multi-tenancy Support
Built-in support for tenant-aware applications.

## 💡 Tips for Success

1. **Start Small**: Begin with core extensions and add components as needed
2. **Follow Patterns**: Use the SlimBus template as a reference
3. **Test-Driven**: Leverage TestContainers for integration tests
4. **Stay Current**: Follow semantic versioning for updates

## 🤝 Getting Help

- **Documentation**: [Complete Documentation](README.md)
- **Issues**: [GitHub Issues](https://github.com/baoduy/DKNet/issues)
- **Discussions**: [GitHub Discussions](https://github.com/baoduy/DKNet/discussions)
- **Contributing**: [Contributing Guide](Contributing.md)

---

> 💡 **Pro Tip**: The SlimBus template provides a complete working example of all DKNet components working together. Use it as your starting point!