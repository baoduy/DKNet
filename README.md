# DKNet Framework

[![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![GitHub release](https://img.shields.io/github/release/baoduy/DKNet.svg)](https://github.com/baoduy/DKNet/releases)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)

A comprehensive .NET framework designed to enhance enterprise application development using **Domain-Driven Design (DDD)
** principles and **Onion Architecture** patterns.

## 🚀 Quick Start

### Installation

```bash
# Core framework extensions
dotnet add package DKNet.Fw.Extensions

# Entity Framework Core extensions
dotnet add package DKNet.EfCore.Extensions
dotnet add package DKNet.EfCore.Repos

# Messaging & CQRS
dotnet add package DKNet.SlimBus.Extensions

# Blob storage (choose your provider)
dotnet add package DKNet.Svc.BlobStorage.AzureStorage
```

### Get Started with Template

```bash
# Use the complete SlimBus API template
git clone https://github.com/baoduy/DKNet.git
cd DKNet/src/Templates/SlimBus.ApiEndpoints
dotnet run --project SlimBus.Api
```

## 🏗️ Key Features

- **🎯 Domain-Driven Design**: Rich domain models with business logic encapsulation
- **🧅 Onion Architecture**: Clean separation of concerns with dependency inversion
- **⚡ CQRS Pattern**: Command/Query separation for scalable applications
- **🔄 Event-Driven**: Domain events for loose coupling and integration
- **🗄️ Repository Pattern**: Abstracted data access with specifications
- **🗃️ Multi-Storage**: Azure Blob, AWS S3, and local file storage
- **🧪 Test-Ready**: 99% code coverage with TestContainers integration

## 📋 Core Packages

| Package                      | Description                             | NuGet                                                                                                                         |
|------------------------------|-----------------------------------------|-------------------------------------------------------------------------------------------------------------------------------|
| **DKNet.Fw.Extensions**      | Core framework utilities and extensions | [![NuGet](https://img.shields.io/nuget/v/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)           |
| **DKNet.EfCore.Extensions**  | Entity Framework Core enhancements      | [![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Extensions)](https://www.nuget.org/packages/DKNet.EfCore.Extensions/)   |
| **DKNet.SlimBus.Extensions** | CQRS and messaging integration          | [![NuGet](https://img.shields.io/nuget/v/DKNet.SlimBus.Extensions)](https://www.nuget.org/packages/DKNet.SlimBus.Extensions/) |

[**→ View All Packages**](docs/README.md#component-documentation)

## 📖 Documentation

| Section                                              | Description                                        |
|------------------------------------------------------|----------------------------------------------------|
| **[📚 Complete Documentation](docs/README.md)**      | Comprehensive guides organized by functional areas |
| **[🚀 Getting Started](docs/Getting-Started.md)**    | Installation, setup, and first steps               |
| **[🏗️ Architecture Guide](docs/Architecture.md)**   | Understanding DDD and Onion Architecture           |
| **[⚙️ Configuration](docs/Configuration.md)**        | Setup and configuration options                    |
| **[📝 Examples & Recipes](docs/Examples/README.md)** | Practical implementation examples                  |
| **[📖 API Reference](docs/API-Reference.md)**        | Complete API documentation                         |
| **[❓ FAQ](docs/FAQ.md)**                             | Frequently asked questions                         |

## 🔧 Example Usage

### Domain Entity with Events

```csharp
public class Product : AggregateRoot
{
    public Product(string name, decimal price, string createdBy)
        : base(Guid.NewGuid(), createdBy)
    {
        Name = name;
        Price = price;
    }

    public string Name { get; private set; }
    public decimal Price { get; private set; }

    public void UpdatePrice(decimal newPrice, string userId)
    {
        var oldPrice = Price;
        Price = newPrice;
        SetUpdatedBy(userId);
        
        AddEvent(new ProductPriceChangedEvent(Id, oldPrice, newPrice));
    }
}
```

### CQRS Command Handler

```csharp
public class CreateProductHandler : IRequestHandler<CreateProductCommand, ProductResult>
{
    private readonly IProductRepository _repository;

    public async Task<ProductResult> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product(request.Name, request.Price, request.UserId);
        await _repository.AddAsync(product, cancellationToken);
        return _mapper.Map<ProductResult>(product);
    }
}
```

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](docs/Contributing.md) for details on:

- Development setup
- Coding standards
- Pull request process
- Testing requirements

## 📄 License

This project is licensed under the [MIT License](LICENSE) - see the LICENSE file for details.

---

> 💡 **New to DKNet?** Start with our [Getting Started Guide](docs/Getting-Started.md) or explore
> the [SlimBus Template](src/Templates/SlimBus.ApiEndpoints/README.md) for a complete working example!