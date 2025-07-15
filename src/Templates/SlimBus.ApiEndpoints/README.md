# SlimBus.ApiEndpoints

A modular and scalable framework for building API-driven applications with modern development practices, emphasizing
Domain-Driven Design (DDD), separation of concerns, and extensibility.

---

## Overview

SlimBus.ApiEndpoints is designed to simplify the management of domains, services, and API endpoints while integrating
robust error handling and configurability. It is built using .NET 9.0, ensuring high performance and modern features.

---

## Project Structure

### 1. **SlimBus.Api**

- Contains all API endpoint configurations and controllers.
- **Subfolders:**
    - **`ApiEndpoints`**: Defines endpoint routing and handlers for various modules.
    - **`Configs`**: Houses configurations for middleware, authentication, Swagger, and global exception handling.
    - **`Extensions`**: Provides utility methods and extensions for API services.

### 2. **SlimBus.AppHost**

- Entry point for the application.
- **Responsibilities:**
    - Configures and hosts services.
    - Loads environment-specific configurations.

### 3. **SlimBus.AppServices**

- Implements business logic and core services.
- **Key Components:**
    - **`Extensions/LazyMapper`**: Lazy mapping utilities for optimized data transformation.
    - **`ProcessManagers`**: Manages workflows for long-running processes.
    - **`Profiles/V1`**: Defines actions, queries, and events for the `Profiles` domain.
    - **`Share`**: Shared service implementations.

### 4. **SlimBus.Domains**

- Encapsulates core domain logic using DDD principles.
- **Features:**
    - **`Profiles/Entities`**: Includes entities like `CustomerProfile` and `Employee`.
    - **`Profiles/Repos`**: Repository interfaces for data access.
    - **`Share`**: Provides base classes and utilities (e.g., `AggregateRoot`, `DomainEntity`).

### 5. **SlimBus.Infra**

- Manages data persistence and integrations.
- **Core Services:**
    - **`MembershipService`**: Handles membership-related operations.
    - **`SequenceService`**: Manages sequence generation for IDs.
- **Features:**
    - **`Profiles/Mappers`**: Maps domain objects to database entities.
    - **`Profiles/Repos`**: Implements repository patterns.
    - **`Profiles/StaticData`**: Provides static data configurations.

### 6. **SlimBus.Share**

- Shared resources and constants used across the solution.
- **Options:** Encapsulates configuration options for various modules.

### 7. **SlimBus.App.Tests**

- Contains unit and integration tests for services and API endpoints.
- **Subfolders:**
    - **`Fixtures`**: Test data and mocks.
    - **`Extensions`**: Testing extensions and utilities.

---

## Design Highlights

- **Domain-Driven Design (DDD):**
    - Clear separation between domain, infrastructure, and application layers.
    - Focuses on domain entities and business rules.

- **Command Query Responsibility Segregation (CQRS):**
    - Queries and commands are handled separately for better scalability and readability.

- **Extensibility:**
    - Modular structure allows easy addition of new domains and features.

- **Error Handling and Logging:**
    - Global exception handling and structured logging enhance maintainability.

---

## Technologies and Frameworks

- **Framework:** .NET 9.0
- **Core Libraries:**
    - Entity Framework Core
    - ASP.NET Core (Minimal APIs)
    - AutoMapper
    - MediatR
- **Testing Frameworks:**
    - xUnit
    - Moq

---

## NuGet Packages

### Asp.Versioning.Http (8.1.0)

- **GitHub**: [https://github.com/microsoft/aspnet-api-versioning](https://github.com/microsoft/aspnet-api-versioning)
- **Description**: Provides API versioning for HTTP services.
- **Usage**: Enables version management in APIs, ensuring backward compatibility.

### Azure.Monitor.OpenTelemetry.AspNetCore (1.2.0)

- **GitHub**: [https://github.com/Azure/azure-sdk-for-net](https://github.com/Azure/azure-sdk-for-net)
- **Description**: Integrates OpenTelemetry with Azure Monitor for AspNetCore applications.
- **Usage**: Facilitates telemetry data collection and integration with Azure Monitor.

### ForEvolve.FluentValidation.AspNetCore.Http (1.0.26)

- **GitHub**: [https://github.com/ForEvolve/FluentValidation](https://github.com/ForEvolve/FluentValidation)
- **Description**: Adds FluentValidation support for ASP.NET Core HTTP APIs.
- **Usage**: Simplifies validation of HTTP request payloads.

### Microsoft.AspNetCore.Authentication.JwtBearer (9.0.1)

- **GitHub**: [https://github.com/dotnet/aspnetcore](https://github.com/dotnet/aspnetcore)
- **Description**: Adds JWT Bearer token authentication to ASP.NET Core applications.
- **Usage**: Secures APIs with JSON Web Token-based authentication.

### Microsoft.AspNetCore.OpenApi (9.0.1)

- **GitHub**: [https://github.com/dotnet/aspnetcore](https://github.com/dotnet/aspnetcore)
- **Description**: Provides OpenAPI support for ASP.NET Core.
- **Usage**: Generates Swagger documentation for APIs.

### Microsoft.EntityFrameworkCore.Abstractions (9.0.1)

- **GitHub**: [https://github.com/dotnet/efcore](https://github.com/dotnet/efcore)
- **Description**: EF Core abstractions library for database access.
- **Usage**: Simplifies database operations in applications.

### Microsoft.Extensions.Caching.Hybrid (9.0.0-preview.7.24406.2)

- **GitHub**: [https://github.com/dotnet/extensions](https://github.com/dotnet/extensions)
- **Description**: Enables hybrid caching for applications.
- **Usage**: Combines in-memory and distributed caching.

### Microsoft.Extensions.Caching.StackExchangeRedis (9.0.1)

- **GitHub**: [https://github.com/dotnet/extensions](https://github.com/dotnet/extensions)
- **Description**: StackExchange Redis-based caching for ASP.NET Core.
- **Usage**: Implements distributed caching with Redis.

### Microsoft.Extensions.Diagnostics.HealthChecks (9.0.1)

- **GitHub**: [https://github.com/dotnet/aspnetcore](https://github.com/dotnet/aspnetcore)
- **Description**: Provides health check services for ASP.NET Core.
- **Usage**: Monitors the application's health status.

### Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore (9.0.1)

- **GitHub**: [https://github.com/dotnet/aspnetcore](https://github.com/dotnet/aspnetcore)
- **Description**: EF Core health checks for ASP.NET Core.
- **Usage**: Verifies database connection and EF Core health.

### Microsoft.FeatureManagement.AspNetCore (4.0.0)

- **GitHub
  **: [https://github.com/microsoft/FeatureManagement-Dotnet](https://github.com/microsoft/FeatureManagement-Dotnet)
- **Description**: Feature flags and management for ASP.NET Core.
- **Usage**: Toggles features dynamically without redeploying.

### OpenTelemetry (1.11.1)

- **GitHub
  **: [https://github.com/open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet)
- **Description**: Adds observability and instrumentation to applications.
- **Usage**: Tracks performance and traces across distributed systems.

### OpenTelemetry.Exporter.Console (1.11.1)

- **GitHub
  **: [https://github.com/open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet)
- **Description**: Exports OpenTelemetry traces to the console.
- **Usage**: Debugs and inspects telemetry data locally.

### OpenTelemetry.Exporter.OpenTelemetryProtocol (1.11.1)

- **GitHub
  **: [https://github.com/open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet)
- **Description**: OpenTelemetry Protocol exporter.
- **Usage**: Sends telemetry data to OpenTelemetry-compatible systems.

### OpenTelemetry.Extensions.Hosting (1.11.1)

- **GitHub
  **: [https://github.com/open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet)
- **Description**: OpenTelemetry extensions for .NET Core Hosting.
- **Usage**: Simplifies setup of telemetry collection in .NET hosts.

### OpenTelemetry.Instrumentation.AspNetCore (1.10.1)

- **GitHub
  **: [https://github.com/open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet)
- **Description**: Adds AspNetCore instrumentation for OpenTelemetry.
- **Usage**: Collects telemetry data from ASP.NET Core applications.

### Scalar.AspNetCore (2.0.4)

- **GitHub**: [https://github.com/ScalarKit/Scalar](https://github.com/ScalarKit/Scalar)
- **Description**: Provides scalar type support for ASP.NET Core APIs.
- **Usage**: Enables serialization and validation of scalar types in APIs.

### AutoMapper (13.0.1)

- **GitHub**: [https://github.com/AutoMapper/AutoMapper](https://github.com/AutoMapper/AutoMapper)
- **Description**: For object-to-object mapping.
- **Usage**: Simplifies mapping between DTOs and domain objects.

### SlimMessageBus (2.0.4)

- **GitHub**: [https://github.com/zarusz/SlimMessageBus](https://github.com/zarusz/SlimMessageBus)
- **Description**: Provides message bus support for distributed systems.
- **Usage**: Enables pub/sub messaging and request/response communication.

### SlimMessageBus.Host.AzureServiceBus (2.7.0)

- **GitHub**: [https://github.com/zarusz/SlimMessageBus](https://github.com/zarusz/SlimMessageBus)
- **Description**: SlimMessageBus integration for Azure Service Bus.
- **Usage**: Handles messaging with Azure Service Bus in distributed systems.

### xUnit (2.9.3)

- **GitHub**: [https://github.com/xunit/xunit](https://github.com/xunit/xunit)
- **Description**: Unit testing framework.
- **Usage**: Implements test cases for .NET applications.

### coverlet.collector (6.0.4)

- **GitHub**: [https://github.com/coverlet-coverage/coverlet](https://github.com/coverlet-coverage/coverlet)
- **Description**: Code coverage collector for tests.
- **Usage**: Measures code coverage for .NET test cases.

### FluentValidation (11.11.0)

- **GitHub
  **: [https://github.com/FluentValidation/FluentValidation](https://github.com/FluentValidation/FluentValidation)
- **Description**: Validation library for .NET.
- **Usage**: Validates input data for business rules.

### MediatR.Extensions.FluentValidation.AspNetCore (5.1.0)

- **GitHub**: [https://github.com/jbogard/MediatR](https://github.com/jbogard/MediatR)
- **Description**: Combines FluentValidation with MediatR.
- **Usage**: Enables validation in request processing pipelines.

---

## Conclusion

The SlimBus.ApiEndpoints solution demonstrates a robust architecture for developing modern APIs with scalability and
maintainability at its core. Its modular design, adherence to DDD, and integration of cutting-edge .NET technologies
make it a reliable foundation for enterprise-grade applications.

