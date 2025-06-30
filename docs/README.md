# DKNet Framework Documentation

Welcome to the comprehensive documentation for the DKNet Framework - a powerful collection of .NET libraries designed to enhance and simplify enterprise application development using Domain-Driven Design (DDD) principles and Onion Architecture patterns.


## 📚 Documentation Structure

This documentation is organized by functional areas to help you understand how each component contributes to the overall architecture:

### 🔧 [Core Framework](./Core/README.md)
Foundation utilities and extensions that support all other components.

- [DKNet.Fw.Extensions](./Core/DKNet.Fw.Extensions.md) - Framework-level extensions and utilities

### 🗄️ [Entity Framework Core Extensions](./EfCore/README.md)
Comprehensive EF Core enhancements that implement repository patterns, domain events, and data access abstractions.

- [DKNet.EfCore.Abstractions](./EfCore/DKNet.EfCore.Abstractions.md) - Core abstractions and interfaces
- [DKNet.EfCore.DataAuthorization](../EfCore/DKNet.EfCore.DataAuthorization/README.md) - Data authorization and access control
- [DKNet.EfCore.Events](../EfCore/DKNet.EfCore.Events/README.md) - Domain event handling and dispatching
- [DKNet.EfCore.Extensions](../EfCore/DKNet.EfCore.Extensions/README.md) - EF Core functionality enhancements
- [DKNet.EfCore.Hooks](../EfCore/DKNet.EfCore.Hooks/README.md) - Lifecycle hooks for EF Core operations
- [DKNet.EfCore.Relational.Helpers](../EfCore/DKNet.EfCore.Relational.Helpers/README.md) - Relational database utilities
- [DKNet.EfCore.Repos](../EfCore/DKNet.EfCore.Repos/README.md) - Repository pattern implementations
- [DKNet.EfCore.Repos.Abstractions](./EfCore/DKNet.EfCore.Repos.Abstractions.md) - Repository abstractions

### 📨 [Messaging & CQRS](./Messaging/README.md)
SlimMessageBus integration for implementing CQRS patterns and event-driven architecture.

- [DKNet.SlimBus.Extensions](./Messaging/DKNet.SlimBus.Extensions.md) - SlimMessageBus extensions for EF Core

### 🔧 [Service Layer](./Services/README.md)
Application services including blob storage abstractions and data transformation utilities.

- [DKNet.Svc.BlobStorage.Abstractions](./Services/DKNet.Svc.BlobStorage.Abstractions.md) - File storage service abstractions
- [DKNet.Svc.BlobStorage.AwsS3](../Services/DKNet.Svc.BlobStorage.AwsS3/README.md) - AWS S3 storage adapter
- [DKNet.Svc.BlobStorage.AzureStorage](../Services/DKNet.Svc.BlobStorage.AzureStorage/README.md) - Azure Blob storage adapter
- [DKNet.Svc.BlobStorage.Local](../Services/DKNet.Svc.BlobStorage.Local/README.md) - Local file system storage
- [DKNet.Svc.Transformation](../Services/DKNet.Svc.Transformation/README.md) - Data transformation services

## 🏗️ Architecture Overview

The DKNet Framework is built around **Domain-Driven Design (DDD)** principles and implements the **Onion Architecture** pattern. Each component is designed to support specific layers of this architecture:

![Diagram](https://raw.githubusercontent.com/baoduy/DKNet/e84b5ba3c035d5f12d03ba348e396976d1b0219b/Diagram.png)

```
┌─────────────────────────────────────────────────────────────────┐
│                        🌐 Presentation Layer                    │
│                     (API Controllers, UI)                      │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                     🎯 Application Layer                        │
│              (Application Services, CQRS Handlers)             │
│                                                                 │
│  📨 DKNet.SlimBus.Extensions                                   │
│  🔧 DKNet.Svc.* (Services)                                     │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                      💼 Domain Layer                           │
│                (Entities, Aggregates, Domain Events)           │
│                                                                 │
│  🏗️ Core business logic and rules                              │
│  📋 Domain Events via DKNet.EfCore.Events                      │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🗄️ Infrastructure Layer                      │
│           (Data Access, External Services, Persistence)        │
│                                                                 │
│  🗃️ DKNet.EfCore.* (Repository patterns, Data access)         │
│  🔒 DKNet.EfCore.DataAuthorization                            │
│  ⚙️ DKNet.Fw.Extensions (Cross-cutting concerns)               │
└─────────────────────────────────────────────────────────────────┘
```

### Key Architectural Principles

1. **Dependency Inversion**: Inner layers don't depend on outer layers
2. **Separation of Concerns**: Each component has a single, well-defined responsibility
3. **Domain-Centricity**: Business logic is isolated in the domain layer
4. **Event-Driven Architecture**: Domain events enable loose coupling between bounded contexts
5. **Repository Pattern**: Abstracts data access and enables testability

## 🚀 Getting Started

To get started with the DKNet Framework:

1. **Choose Your Components**: Review the documentation for each component to understand which ones fit your needs
2. **Review Architecture Patterns**: Understand how each component fits into the DDD/Onion architecture
3. **Follow Implementation Guides**: Each component includes detailed usage examples and best practices
4. **Explore Templates**: Check out the [SlimBus.ApiEndpoints template](https://github.com/baoduy/DKNet/tree/dev/z_Templates/SlimBus.ApiEndpoints) for a complete reference implementation

## 🤝 Contributing to Documentation

We welcome contributions to improve this documentation! If you find areas that need clarification or have suggestions for additional content, please:

1. Open an issue describing the documentation improvement needed
2. Submit a pull request with your proposed changes
3. Follow the existing documentation structure and style

---

> 💡 **Tip**: This documentation is designed to be published as GitHub Pages were generated by the 'copilot' 100%. 
> If any feedback please raise an issue in the [DKNet repository](https://github.com/baoduy/DKNet).
> Each section provides comprehensive guidance on implementing DDD and Onion Architecture patterns using the DKNet Framework components.