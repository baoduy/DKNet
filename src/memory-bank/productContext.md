# Product Context

## Project Overview
**DKNet Framework** is a comprehensive .NET library collection that provides reusable, production-ready components for building modern .NET applications. It focuses on Entity Framework Core extensions, background task management, messaging patterns, and common utilities.

## Purpose & Value Proposition
DKNet exists to:
- **Accelerate Development**: Provide battle-tested components that eliminate boilerplate code
- **Enforce Best Practices**: Built-in patterns for specifications, repository pattern, audit logging, data authorization, and more
- **Type Safety**: Leverage C# type system with nullable reference types enabled across all projects
- **Performance**: Optimized EF Core queries, async/await patterns, and efficient data access
- **Testability**: All components designed for easy unit and integration testing with TestContainers support

## Core Value Streams

### 1. EfCore Extensions
- **Specifications Pattern**: Dynamic predicate building with fluent API for complex queries
- **Audit Logging**: Automatic tracking of entity changes with customizable audit information
- **Data Authorization**: Row-level security and multi-tenancy support
- **Encryption**: Transparent property-level encryption for sensitive data
- **Event Sourcing**: Domain events and event handlers for EF Core entities
- **Repository Pattern**: Generic repositories with specification support
- **DTO Generation**: Automated DTO mapping and generation

### 2. Background Tasks (AspCore.Tasks)
- **IBackgroundTask Interface**: Simple, testable background task abstraction
- **Hosted Service Integration**: Seamless integration with ASP.NET Core hosted services
- **Task Scheduling**: Configure recurring and one-time background operations

### 3. Messaging (SlimBus)
- **Lightweight Message Bus**: In-process message broker for decoupled communication
- **Azure Service Bus Integration**: Production-ready service bus abstraction
- **Aspire Hosting Support**: Container orchestration for Service Bus emulation

### 4. Core Extensions
- **Type Extensions**: Rich set of type checking and conversion utilities (including enum validation)
- **String Extensions**: Common string manipulation and validation
- **DateTime Extensions**: Date/time operations and formatting
- **Service Collection Extensions**: DI registration helpers and factory patterns

## Target Audience
- **Enterprise .NET Developers**: Building line-of-business applications with EF Core
- **ASP.NET Core Teams**: Need background processing, messaging, and data access patterns
- **Architects**: Implementing DDD, CQRS, and clean architecture principles
- **DevOps Teams**: Using Aspire for local development and container orchestration

## Success Metrics
- Code reuse across multiple projects
- Reduced time-to-market for new features
- Improved code quality and maintainability
- High test coverage (target: >80%)
- Active community adoption (NuGet downloads, GitHub stars)
