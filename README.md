# DKNet Stacks

A modular, extensible .NET solution for building scalable, maintainable, and event-driven enterprise applications. DKNet provides a comprehensive set of libraries and patterns for Domain-Driven Design (DDD), CQRS, and advanced data access with Entity Framework Core.

---

## Table of Contents

- [Introduction](#introduction)
- [Features](#features)
- [Project Structure](#project-structure)
- [Architecture Overview](#architecture-overview)
- [Getting Started](#getting-started)
- [License](#license)

---

## Introduction

DKNet is a suite of .NET libraries designed to accelerate the development of robust business applications. It emphasizes clean architecture, separation of concerns, and best practices for DDD, CQRS, and event-driven workflows. The solution is organized into focused packages that can be used independently or together.

---

## Features

- **Domain-Driven Design (DDD):** Rich support for aggregates, value objects, domain events, and repositories.
- **CQRS:** Clear separation of command (write) and query (read) responsibilities.
- **Event-Driven Architecture:** Centralized event publishing and handling, supporting both internal (MediatR) and external (SlimMessageBus) flows.
- **Advanced EF Core Integration:** Abstractions, hooks, events, and helpers to streamline data access and enforce business rules.
- **Flexible Caching:** Hybrid in-memory and distributed caching for performance.
- **Extensible Messaging:** Out-of-the-box integrations for message buses and external eventing.
- **Productivity Utilities:** Core extensions and helpers to reduce boilerplate and accelerate development.

---

## Project Structure

Each project in DKNet targets a specific concern. Below is a catalog with descriptions:

- **DKNet.Fw.Extensions**  
  Core utilities and helpers for .NET development. Includes abstractions for async enumerables, attributes, DateTime, enums, dependency injection, string/type manipulation, and more.

- **DKNet.EfCore.Abstractions**  
  Abstraction layer for Entity Framework Core. Simplifies repository patterns, CRUD, transactions, and caching for maintainable and testable data access.

- **DKNet.EfCore.DataAuthorization**  
  Role-based and fine-grained data authorization for EF Core. Supports RBAC, auditing, and custom policies to ensure secure data workflows.

- **DKNet.EfCore.Events**  
  Event-based extensions for EF Core. Enables DDD-style triggers and event handlers for business rules, validation, and centralized event processing.

- **DKNet.EfCore.Extensions**  
  Utilities and automation for EF Core, including entity configuration, seeding, and helpers for common patterns.

- **DKNet.EfCore.Hooks**  
  Extensible hooks for EF Core operations. Supports pre/post-operation logic, global hooks, audit logging, and performance tracking.

- **DKNet.EfCore.Relational.Helpers**  
  Helper functions and extensions for relational database operations in EF Core, simplifying table/column manipulation and query building.

- **DKNet.EfCore.Repos.Abstractions**  
  Interfaces and base classes for repository pattern implementations. Provides generic CRUD contracts and abstractions for uniform data access.

- **DKNet.EfCore.Repos**  
  Concrete repository implementations for EF Core. Supports generic/custom repositories, unit of work, and query customization.

- **DKNet.Svc.Transformation**  
  Transformation engine for template processing, token extraction, and type-safe data flows. Includes dynamic converters and robust error handling.

- **DKNet.SlimBus.Extensions**  
  SlimMessageBus integrations for EF Core. Automates database persistence for message-based apps and provides abstractions for requests, queries, and notifications.

---

## Architecture Overview

DKNet is built around layered, event-driven, and mediator-based design principles. The architecture supports modularity, scalability, and maintainability.

### Diagram

![Architecture Diagram](Diagram.png)

### Key Layers

- **UI & API:**  
  User interactions via API Controllers. Data is exchanged as DTOs or value objects, ensuring separation between application and domain layers.

- **Application Services:**  
  Coordinates application flows using handlers (Commands, Queries, Events) mediated by MediatR. Includes cross-cutting concerns like caching and process management.

- **CQRS:**  
  Commands mutate state through domain logic; queries project state for reads. Each has dedicated handler chains.

- **Domain Layer:**  
  Contains aggregates, entities, domain services, repositories, and event sources. Implements business rules and rich domain behavior.

- **Infrastructure Layer:**  
  Handles persistence, event publishing, and integrations. Repositories interact with EF Core and its extensions. Events are published internally (MediatR) and externally (SlimBus).

- **Caching & Messaging:**  
  HybridCache (memory/Redis) for performance. SlimMessageBus for external event integrations (Kafka, ServiceBus, MQ, Redis).

- **Event Processing:**  
  Domain/data change events flow through MediatR (internal) or SlimBus (external), handled by dedicated event handler chains.

#### Diagram Legend

- **Green arrows:** Write (command) flows
- **Purple arrows:** Read (query) flows
- **Red arrows:** Event flows (domain/integration)
- **Yellow arrows:** Caching interactions

---

## Getting Started

1. **Clone the Repository**
   ```sh
   git clone https://github.com/your-org/DKNet.git
   ```

2. **Explore Projects**
   - Each project is self-contained and can be referenced independently.
   - Review the sample projects and tests for usage patterns.

3. **Integrate into Your Solution**
   - Reference the desired DKNet packages in your .NET solution.
   - Follow the architecture guidelines and patterns illustrated above.

4. **Documentation**
   - Each package contains XML documentation and usage samples.
   - For advanced scenarios, refer to the source code and tests.

---

## License

MIT Licensed - See [LICENSE](LICENSE) for details.