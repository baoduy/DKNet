# API Configuration Documentation

This directory contains the configuration setup for a MediatR-based API application. The configuration is modularly organized to handle different aspects of the API infrastructure.

## Core Configuration Files

### AppConfig.cs
The main configuration orchestrator that:
- Manages feature toggles through `FeatureOptions`
- Coordinates the initialization of all other configuration modules
- Provides extension methods for both service registration (`AddAppConfig`) and middleware setup (`UseAppConfig`)
- Implements conditional feature enabling based on configuration

### ServiceConfigs.cs
Handles service registration including:
- Options configuration through `AddOptions`
- Core services registration (HTTP context, Principal provider)
- Database connection setup using configuration string
- Infrastructure and application services
- Service bus integration with assembly scanning

## Feature-Specific Configurations

### Authentication & Security

#### Antiforgery Protection (`Antiforgery/`)
Implements CSRF protection with:
- Configurable cookie and header names
- Secure cookie policy with HTTP-only and SameSite strict settings
- Custom middleware (`AntiforgeryCookieMiddleware`) that:
  - Validates tokens for POST, PUT, PATCH, DELETE methods
  - Automatically generates and manages CSRF tokens
  - Handles token rotation and validation

#### Auth Configuration (`Auth/`)
- JWT token handling for Microsoft Graph integration
- Authorization policy configuration
- Integration with identity providers

### API Documentation
#### Swagger Configuration (`Swagger/`)
- OpenAPI documentation setup with bearer token support
- Custom security transformers for authentication
- API versioning support in documentation
- Scalar API reference integration with customizable themes
- Server URL configuration for different environments

### API Features

#### API Versioning (`VersioningConfig.cs`)
Implements comprehensive API versioning:
- URL segment-based version reading (e.g., `/v1/api/resource`)
- Default API version (v1.0) when unspecified
- Automatic version reporting in responses
- Version binding support for endpoints
- Integration with OpenAPI documentation
- Support for multiple versions of the same endpoint
- Version deprecation handling
- Configuration through extension methods:
```csharp
services.AddAppVersioning();  // Adds versioning with default settings
```

#### Endpoint Configuration (`Endpoints/`)
Provides a robust endpoint configuration system:
- Automatic API versioning support
- Route group creation with version prefixing
- Fluent validation integration
- Authorization requirement handling
- Tag-based endpoint organization
- Filter pipeline setup including user ID property handling

#### Idempotency Handling (`Idempotency/`)
Implements robust request deduplication:
- Header-based idempotency key validation
- Configurable conflict handling strategies:
  - Conflict response (409)
  - Return cached response
- Response caching for idempotent operations
- Custom repository pattern for key storage
- Endpoint filter implementation for automatic handling

### Error Handling (`GlobalExceptions/`)
Implements comprehensive exception handling:
- Centralized error handling through `GlobalExceptionHandler`
- Problem Details (RFC 7807) implementation
- Automatic error response formatting with:
  - HTTP status codes
  - Error type classification
  - Detailed error messages
  - Request tracing information
- Custom problem details customization:
  - Request method and path inclusion
  - Trace ID correlation
  - Exception type mapping
- Logging integration for error tracking
- Standardized error response format:
```json
{
  "type": "ExceptionTypeName",
  "title": "Something went wrong!",
  "status": 500,
  "detail": "Detailed error message",
  "instance": "POST /api/resource",
  "trace-id": "unique-trace-identifier"
}
```

### Monitoring & Health
#### Health Checks (`Healthz/`)
- Customizable health check endpoints
- Database connectivity validation
- External service dependency checks
- Health status reporting

### Performance & Reliability
#### Cache Configuration (`CacheConfig.cs`)
- Distributed cache setup
- Memory cache configuration
- Cache profile management

## Implementation Examples

### 1. Antiforgery Setup
```csharp
// In Startup/Program.cs
services.AddAntiforgeryConfig(
    cookieName: "x-csrf-cookie",
    headerName: "x-csrf-header"
);

app.UseAntiforgeryConfig();
```

### 2. Idempotency Implementation
```csharp
// Add idempotency support
services.AddIdempotency(options => {
    options.IdempotencyHeaderKey = "X-Idempotency-Key";
    options.ConflictHandling = IdempotentConflictHandling.ConflictResponse;
});

// In endpoint configuration
app.MapPost("/api/resource", handler)
   .AddIdempotencyFilter();
```

### 3. Global Exception Handling
```csharp
// Add global exception handling
services.AddGlobalException();

// In Program.cs
app.UseGlobalException();

// Exception handling with problem details
services.AddProblemDetails(options => {
    options.CustomizeProblemDetails = ctx => {
        ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
        ctx.ProblemDetails.Extensions["trace-id"] = ctx.HttpContext.TraceIdentifier;
    };
});
```

### 4. Endpoint Configuration
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

### 5. API Versioning Setup
```csharp
// Add API versioning
services.AddAppVersioning();

// In endpoint configuration
app.MapGroup($"/v{version}/users")
   .WithApiVersionSet(versionSet)
   .MapToApiVersion(new ApiVersion(1, 0));
```

## Best Practices

1. **Modularity**
   - Each configuration aspect is isolated in its own module
   - Use feature flags for optional components
   - Follow single responsibility principle

2. **Security**
   - Enable antiforgery protection for all state-changing operations
   - Implement proper CORS policies
   - Use secure cookie policies
   - Enable authorization where required

3. **Performance**
   - Implement idempotency for state-changing operations
   - Use appropriate caching strategies
   - Configure health checks for monitoring

4. **API Design**
   - Use versioning for API evolution
   - Implement proper validation
   - Document endpoints using OpenAPI/Swagger
   - Use proper HTTP methods and status codes

5. **Error Handling**
   - Use the global exception handler for centralized error management
   - Include trace IDs for error correlation
   - Implement proper logging
   - Return standardized problem details responses
   - Handle different exception types appropriately

## Configuration Structure

```
Configs/
├── AppConfig.cs                 # Main configuration orchestrator
├── ServiceConfigs.cs            # Service registration
├── VersioningConfig.cs          # API versioning setup
├── Antiforgery/                # CSRF protection
│   ├── AntiforgeryConfig.cs
│   └── AntiforgeryCookieMiddleware.cs
├── Auth/                       # Authentication & Authorization
├── Endpoints/                  # API endpoint configuration
├── Idempotency/               # Request deduplication
├── Swagger/                    # API documentation
└── GlobalExceptions/          # Error handling
    ├── GlobalExceptionConfigs.cs
    └── GlobalExceptionHandler.cs
```

## Middleware Order

The correct order of middleware registration is crucial:

1. Exception Handling (First to catch all exceptions)
2. HTTPS Redirection
3. CORS
4. Authentication
5. Authorization
6. Endpoint Routing
7. Custom Middleware
8. Endpoints

This order is automatically handled by `UseAppConfig()` in the proper sequence.