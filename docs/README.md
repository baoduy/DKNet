# DKNet Framework Documentation

Welcome to the comprehensive documentation for the DKNet Framework - a powerful collection of .NET libraries designed to enhance and simplify enterprise application development using Domain-Driven Design (DDD) principles and Onion Architecture patterns.

## 📚 Quick Navigation

### 🚀 Getting Started
- **[Getting Started Guide](Getting-Started.md)** - Installation, setup, and first steps
- **[Configuration & Setup](Configuration.md)** - Detailed configuration options
- **[Architecture Guide](Architecture.md)** - Understanding DDD and Onion Architecture

### 📖 Core Documentation
- **[API Reference](API-Reference.md)** - Index into the per-package API documentation
- **[Examples & Recipes](Examples/README.md)** - Practical implementation examples
- **[FAQ](FAQ.md)** - Frequently asked questions and troubleshooting

### 📋 Project Information
- **[Changelog](CHANGELOG.md)** - Version history and release notes
- **[Migration Guide](Migration-Guide.md)** - Upgrading between versions
- **[Contributing Guide](Contributing.md)** - How to contribute to the project
- **[Testing Strategy](Testing-Strategy.md)** - Testing approach and coverage goals
- **[Security Policy](Security.md)** - Security practices and reporting

## 🏗️ Component Documentation

This documentation is organized by functional areas to help you understand how each component contributes to the overall architecture:

### 🔧 [Core Framework](./Core/README.md)
Foundation, dependency-light utilities that sit at the bottom of the dependency graph.

- [DKNet.Fw.Extensions](./Core/DKNet.Fw.Extensions.md) - Extension methods and reflection helpers — string/type/enum/DateTime/async-enumerable/property/attribute extensions, DI registration guards, and fluent assembly/type scanning
- [DKNet.RandomCreator](./Core/DKNet.RandomCreator.md) - Cryptographically secure random string/char generation with digit and symbol quotas, for passwords, tokens, and other secrets

### 🗄️ [Entity Framework Core Extensions](./EfCore/README.md)
Comprehensive EF Core enhancements that implement the specification pattern, domain events, and data access abstractions.

- [DKNet.EfCore.Abstractions](./EfCore/DKNet.EfCore.Abstractions.md) - Entity base classes, domain-event contracts, and shared attributes
- [DKNet.EfCore.Extensions](./EfCore/DKNet.EfCore.Extensions.md) - DI/wiring layer: entity configuration discovery, global query filters, data seeding, GUIDv7 keys, sequences
- [DKNet.EfCore.Specifications](./EfCore/DKNet.EfCore.Specifications.md) - The specification pattern and `IRepositorySpec`, including the Dynamic Predicate Builder — the current, supported way to query and persist
- [DKNet.EfCore.Repos](./EfCore/DKNet.EfCore.Repos.md) - **Retired**, source-only generic repository, superseded by Specifications
- [DKNet.EfCore.Repos.Abstractions](./EfCore/DKNet.EfCore.Repos.Abstractions.md) - **Retired**, source-only repository interfaces implemented by `DKNet.EfCore.Repos`
- [DKNet.EfCore.Hooks](./EfCore/DKNet.EfCore.Hooks.md) - Before/after-SaveChanges interceptor pipeline
- [DKNet.EfCore.Events](./EfCore/DKNet.EfCore.Events.md) - Domain event handling and dispatching
- [DKNet.EfCore.AuditLogs](./EfCore/DKNet.EfCore.AuditLogs.md) - Audit trail of entity changes, with sensitive-data-aware redaction
- [DKNet.EfCore.DataAuthorization](./EfCore/DKNet.EfCore.DataAuthorization.md) - Row-level, ownership-based data authorization via global query filters
- [DKNet.EfCore.Encryption](./EfCore/DKNet.EfCore.Encryption.md) - Transparent column-level encryption via an EF Core value converter
- [DKNet.EfCore.DtoGenerator](./EfCore/DKNet.EfCore.DtoGenerator.md) - Compile-time DTO generation from entities via a Roslyn source generator
- [DKNet.EfCore.Relational.Helpers](./EfCore/DKNet.EfCore.Relational.Helpers.md) - Small provider-aware `DbContext` helpers

### 📨 [Messaging & CQRS](./Messaging/README.md)
SlimMessageBus integration for implementing CQRS patterns and event-driven architecture.

- [DKNet.SlimBus.Extensions](./Messaging/DKNet.SlimBus.Extensions.md) - SlimMessageBus extensions for EF Core

### 🔧 [Service Layer](./Services/README.md)
Application services including blob storage abstractions and data transformation utilities.

- [DKNet.Svc.BlobStorage.Abstractions](./Services/DKNet.Svc.BlobStorage.Abstractions.md) - File storage service abstractions
- [DKNet.Svc.BlobStorage.AwsS3](./Services/DKNet.Svc.BlobStorage.AwsS3.md) - AWS S3 storage adapter
- [DKNet.Svc.BlobStorage.AzureStorage](./Services/DKNet.Svc.BlobStorage.AzureStorage.md) - Azure Blob storage adapter
- [DKNet.Svc.BlobStorage.Local](./Services/DKNet.Svc.BlobStorage.Local.md) - Local file system storage
- [DKNet.Svc.Transformation](./Services/DKNet.Svc.Transformation.md) - Data transformation services
- [DKNet.Svc.PdfGenerators](./Services/DKNet.Svc.PdfGenerators.md) - Documentation-grade PDF generation toolkit
- [DKNet.Svc.Encryption](./Services/DKNet.Svc.Encryption.md) - Cryptographic helpers (AES, RSA, HMAC, hashing)

### ☁️ [Aspire Integrations](./Aspire/README.md)
Infrastructure orchestration helpers for .NET Aspire AppHost projects.

- [Aspire.Hosting.ServiceBus](./Aspire/Aspire.Hosting.ServiceBus.md) - Azure Service Bus resource builder extensions

### ⚙️ [ASP.NET Core Utilities](./AspNetCore/README.md)
Startup orchestration, minimal-API, and idempotency utilities for web/API workloads.

- [DKNet.AspCore.Tasks](./AspNetCore/DKNet.AspCore.Tasks.md) - Application start-up background job orchestration
- [DKNet.AspCore.Extensions](./AspNetCore/DKNet.AspCore.Extensions.md) - Minimal-API glue: claim-based request population, endpoint discovery/mapping, paged responses, Result/ProblemDetails conversion
- [DKNet.AspCore.Idempotency](./AspNetCore/DKNet.AspCore.Idempotency.md) - Endpoint filter that makes minimal-API operations safe to retry, backed by a pluggable key store
- [DKNet.AspCore.Idempotency.Relational](./AspNetCore/DKNet.AspCore.Idempotency.Relational.md) - Shared EF Core building blocks for a relational idempotency store
- [DKNet.AspCore.Idempotency.MsSqlStore](./AspNetCore/DKNet.AspCore.Idempotency.MsSqlStore.md) - SQL Server-backed idempotency key store
- [DKNet.AspCore.Idempotency.NpgsqlStore](./AspNetCore/DKNet.AspCore.Idempotency.NpgsqlStore.md) - PostgreSQL-backed idempotency key store
- [DKNet.AspCore.Idempotency.RedisStore](./AspNetCore/DKNet.AspCore.Idempotency.RedisStore.md) - Redis-backed idempotency key store, no schema/migrations required

## 🏗️ Architecture Overview

The DKNet Framework is built around **Domain-Driven Design (DDD)** principles and implements the **Onion Architecture** pattern. Each component is designed to support specific layers of this architecture:

![Diagram](https://raw.githubusercontent.com/baoduy/DKNet/e84b5ba3c035d5f12d03ba348e396976d1b0219b/Diagram.png)

```
┌─────────────────────────────────────────────────────────────────┐
│                      🌐 Presentation Layer                       │
│                     (API Controllers, UI)                       │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🎯 Application Layer                            │
│              (Application Services, CQRS Handlers)              │
│                                                                 │
│  📨 DKNet.SlimBus.Extensions                                    │
│  🔧 DKNet.Svc.* (Services)                                      │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                              │
│                (Entities, Aggregates, Domain Events)            │
│                                                                 │
│  🏗️ Core business logic and rules                               │
│  📋 Domain Events via DKNet.EfCore.Events                       │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🗄️ Infrastructure Layer                       │
│           (Data Access, External Services, Persistence)         │
│                                                                 │
│  🗃️ DKNet.EfCore.* (Repository patterns, Data access)           │
│  🔒 DKNet.EfCore.DataAuthorization                               │
│  ⚙️ DKNet.Fw.Extensions (Cross-cutting concerns)                │
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