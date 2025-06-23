# SlimBus.Api Configs

This directory contains modular configuration components for a MediatR-based API built with SlimBus. Each module is responsible for a specific aspect of API infrastructure, promoting maintainability and extensibility.

---

## Table of Contentss

- [SlimBus.Api Configs](#slimbusapi-configs)
  - [Table of Contentss](#table-of-contentss)
  - [Overview](#overview)
  - [Configuration Modules](#configuration-modules)
    - [AppConfig.cs](#appconfigcs)
    - [ServiceConfigs.cs](#serviceconfigscs)
    - [Authentication \& Security](#authentication--security)
      - [Antiforgery (`Antiforgery/`)](#antiforgery-antiforgery)
      - [Auth (`Auth/`)](#auth-auth)
    - [API Documentation](#api-documentation)
      - [Swagger (`Swagger/`)](#swagger-swagger)
    - [API Features](#api-features)
      - [Versioning (`VersioningConfig.cs`)](#versioning-versioningconfigcs)
      - [Endpoints (`Endpoints/`)](#endpoints-endpoints)
      - [Idempotency (`Idempotency/`)](#idempotency-idempotency)
    - [Error Handling (`GlobalExceptions/`)](#error-handling-globalexceptions)
    - [Monitoring \& Health](#monitoring--health)
      - [Healthz (`Healthz/`)](#healthz-healthz)
    - [Performance \& Reliability](#performance--reliability)
      - [CacheConfig.cs](#cacheconfigcs)
  - [Implementation Examples](#implementation-examples)
  - [Best Practices](#best-practices)
  - [Directory Structure](#directory-structure)
  - [Middleware Order](#middleware-order)

---

## Overview

The configuration system is organized into focused modules, each handling a distinct concern such as authentication, versioning, documentation, error handling, and more. This modularity enables easy customization and feature toggling.

---

## Configuration Modules

### AppConfig.cs

- Central orchestrator for all configuration modules.
- Manages feature toggles via `FeatureOptions`.
- Provides extension methods for service registration (`AddAppConfig`) and middleware setup (`UseAppConfig`).
- Enables conditional features based on configuration.

### ServiceConfigs.cs

- Registers core services (HTTP context, principal provider).
- Configures options and database connections.
- Registers infrastructure, application, and service bus integrations.

### Authentication & Security

#### Antiforgery (`Antiforgery/`)

- Implements CSRF protection with configurable cookie/header names.
- Secure cookie policy (HTTP-only, SameSite).
- Middleware for token validation and rotation.

#### Auth (`Auth/`)

- JWT handling for Microsoft Graph and other providers.
- Authorization policy configuration.

### API Documentation

#### Swagger (`Swagger/`)

- OpenAPI/Swagger setup with bearer token support.
- Custom security transformers.
- API versioning in docs.
- Scalar API reference and theming.

### API Features

#### Versioning (`VersioningConfig.cs`)

- URL segment-based versioning (e.g., `/v1/api/resource`).
- Default version fallback.
- Version reporting and OpenAPI integration.
- Deprecation support.

#### Endpoints (`Endpoints/`)

- Route group creation with version prefixing.
- Fluent validation, authorization, and tagging.
- Filter pipeline for user ID and other concerns.

#### Idempotency (`Idempotency/`)

- Header-based idempotency key validation.
- Configurable conflict handling (409 or cached response).
- Response caching and custom key storage.

### Error Handling (`GlobalExceptions/`)

- Centralized exception handling via `GlobalExceptionHandler`.
- Problem Details (RFC 7807) formatting.
- Trace ID correlation and logging.
- Standardized error responses.

### Monitoring & Health

#### Healthz (`Healthz/`)

- Health check endpoints.
- Database and external service checks.

### Performance & Reliability

#### CacheConfig.cs

- Distributed and memory cache setup.
- Cache profile management.

---

## Implementation Examples

**Antiforgery Setup**

```csharp
services.AddAntiforgeryConfig(cookieName: "x-csrf-cookie", headerName: "x-csrf-header");
app.UseAntiforgeryConfig();
```

**Idempotency**

```csharp
services.AddIdempotency(options => {
    options.IdempotencyHeaderKey = "X-Idempotency-Key";
    options.ConflictHandling = IdempotentConflictHandling.ConflictResponse;
});
app.MapPost("/api/resource", handler).AddIdempotencyFilter();
```

**Global Exception Handling**

```csharp
services.AddGlobalException();
app.UseGlobalException();
services.AddProblemDetails(options => {
    options.CustomizeProblemDetails = ctx => {
        ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
        ctx.ProblemDetails.Extensions["trace-id"] = ctx.HttpContext.TraceIdentifier;
    };
});
```

**Endpoint Configuration**

```csharp
public class UserEndpoints : IEndpointConfig
{
    public int Version => 1;
    public string GroupEndpoint => "/users";
    public void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetUsers);
        group.MapPost("/", CreateUser).AddIdempotencyFilter();
    }
}
```

**API Versioning**

```csharp
services.AddAppVersioning();
app.MapGroup($"/v{version}/users")
   .WithApiVersionSet(versionSet)
   .MapToApiVersion(new ApiVersion(1, 0));
```

---

## Best Practices

- **Modularity:** Isolate configuration by concern; use feature flags.
- **Security:** Enable antiforgery, CORS, secure cookies, and authorization.
- **Performance:** Use idempotency and caching; configure health checks.
- **API Design:** Version endpoints, validate input, document with OpenAPI.
- **Error Handling:** Centralize exception handling, use trace IDs, and standardize responses.

---

## Directory Structure

```
Configs/
├── AppConfig.cs
├── ServiceConfigs.cs
├── VersioningConfig.cs
├── Antiforgery/
├── Auth/
├── Endpoints/
├── Idempotency/
├── Swagger/
└── GlobalExceptions/
```

---

## Middleware Order

Register middleware in this order for correct behavior:

1. Exception Handling
2. HTTPS Redirection
3. CORS
4. Authentication
5. Authorization
6. Endpoint Routing
7. Custom Middleware
8. Endpoints

`UseAppConfig()` ensures correct ordering automatically.
