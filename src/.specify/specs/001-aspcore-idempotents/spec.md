# Feature Specification: ASP.NET Core Idempotency Support

**Feature Branch**: `001-aspcore-idempotents`  
**Created**: 2026-01-29  
**Status**: Draft  
**Input**: User request for idempotency key requirement for API endpoints

## Overview

Implement a library (`DKNet.AspCore.Idempotents`) that provides idempotency support for ASP.NET Core Minimal APIs and MVC endpoints. The library ensures that repeated requests with the same idempotency key return the same response without re-executing the operation, preventing duplicate side effects.

## User Scenarios & Testing

### User Story 1 - Basic Idempotency via Attribute (Priority: P1)

As an API developer, I want to mark endpoints as requiring idempotency keys so that clients must provide an `Idempotency-Key` header for mutating operations.

**Why this priority**: Core functionality - without this, the library has no value.

**Independent Test**: Apply `[Idempotent]` attribute to a POST endpoint, send request with idempotency key, verify response is cached and subsequent identical requests return cached response.

**Acceptance Scenarios**:

1. **Given** an endpoint decorated with `[Idempotent]`, **When** a POST request is sent with `Idempotency-Key: abc123`, **Then** the operation executes and response is stored.
2. **Given** a cached response exists for key `abc123`, **When** a second POST request with the same key arrives, **Then** the cached response is returned without re-execution.
3. **Given** an endpoint decorated with `[Idempotent]`, **When** a POST request is sent without `Idempotency-Key` header, **Then** return `400 Bad Request` with appropriate error message.

---

### User Story 2 - Fluent Configuration for Minimal APIs (Priority: P1)

As an API developer using Minimal APIs, I want to use fluent extension methods to require idempotency on specific endpoints.

**Why this priority**: DKNet uses Minimal APIs extensively (see `FluentEndpointMapperExtensions`), this pattern must be supported.

**Independent Test**: Use `.RequireIdempotency()` on a route builder, verify idempotency is enforced.

**Acceptance Scenarios**:

1. **Given** a Minimal API endpoint with `.RequireIdempotency()`, **When** a request with idempotency key is sent, **Then** idempotency behavior is applied.
2. **Given** a Minimal API endpoint with `.RequireIdempotency(TimeSpan.FromHours(24))`, **When** checking after 24 hours, **Then** the cached response has expired.

---

### User Story 3 - Configurable Storage Backend (Priority: P2)

As an infrastructure engineer, I want to configure where idempotency responses are stored (in-memory, Redis, SQL) so that I can choose based on my deployment environment.

**Why this priority**: Different environments need different storage strategies (single server vs distributed).

**Independent Test**: Configure Redis storage, send idempotent request, verify response is stored in Redis.

**Acceptance Scenarios**:

1. **Given** `AddIdempotency(options => options.UseInMemory())` is configured, **When** the application runs, **Then** responses are stored in memory.
2. **Given** `AddIdempotency(options => options.UseDistributedCache())` is configured with Redis, **When** the application runs, **Then** responses are stored in Redis.
3. **Given** `AddIdempotency(options => options.UseCustomStore<MyStore>())` is configured, **When** the application runs, **Then** the custom store is used.

---

### User Story 4 - Concurrent Request Handling (Priority: P2)

As an API developer, I want concurrent requests with the same idempotency key to be handled safely so that only one request executes while others wait.

**Why this priority**: Prevents race conditions in distributed systems.

**Independent Test**: Send 10 concurrent requests with the same key, verify only one execution occurs.

**Acceptance Scenarios**:

1. **Given** two concurrent requests with the same idempotency key, **When** both arrive simultaneously, **Then** only one executes and the other waits for the cached response.
2. **Given** a request is in-flight with key `xyz`, **When** another request with the same key arrives, **Then** return `409 Conflict` with `Retry-After` header OR wait and return cached response (configurable).

---

### User Story 5 - Key Fingerprinting with Request Body (Priority: P3)

As an API developer, I want to optionally include request body in the idempotency fingerprint so that different payloads with the same key are rejected.

**Why this priority**: Prevents accidental key reuse with different data.

**Independent Test**: Enable fingerprinting, send same key with different body, verify rejection.

**Acceptance Scenarios**:

1. **Given** fingerprinting is enabled, **When** a request with key `abc` and body `{a:1}` is cached, **Then** a subsequent request with key `abc` and body `{a:2}` returns `422 Unprocessable Entity`.
2. **Given** fingerprinting is disabled (default), **When** a request with key `abc` and body `{a:1}` is cached, **Then** a subsequent request with key `abc` and body `{a:2}` returns the cached response.

---

### User Story 6 - Response with Idempotency Headers (Priority: P3)

As an API consumer, I want response headers to indicate idempotency status so that I know if a response was cached or freshly executed.

**Why this priority**: Aids debugging and client-side logic.

**Independent Test**: Check response headers for idempotency metadata.

**Acceptance Scenarios**:

1. **Given** a fresh execution, **When** response is returned, **Then** include `Idempotency-Key-Status: created` header.
2. **Given** a cached response, **When** response is returned, **Then** include `Idempotency-Key-Status: cached` and `Idempotency-Key-Expires: <datetime>` headers.

---

### Edge Cases

- What happens when the idempotency key format is invalid (too long, invalid characters)?
  → Return `400 Bad Request` with validation error
- What happens when storage is unavailable?
  → Configurable: fail-open (proceed without idempotency) or fail-closed (return 503)
- What happens when the cached response is for a failed request (4xx/5xx)?
  → Configurable: cache errors or only cache successful responses
- What happens with streaming responses?
  → Not supported initially; return error if endpoint produces stream

## Requirements

### Functional Requirements

- **FR-001**: System MUST provide `[Idempotent]` attribute for MVC controllers and Minimal API endpoints
- **FR-002**: System MUST provide `.RequireIdempotency()` extension method for `RouteHandlerBuilder`
- **FR-003**: System MUST extract idempotency key from `Idempotency-Key` HTTP header (configurable header name)
- **FR-004**: System MUST return `400 Bad Request` when idempotency key is missing on protected endpoints
- **FR-005**: System MUST store response (status code, headers, body) keyed by idempotency key
- **FR-006**: System MUST return cached response for duplicate requests with same idempotency key
- **FR-007**: System MUST support configurable TTL for cached responses (default: 24 hours)
- **FR-008**: System MUST provide `IIdempotencyStore` interface for custom storage implementations
- **FR-009**: System MUST provide in-memory and `IDistributedCache` implementations
- **FR-010**: System MUST handle concurrent requests safely using distributed locking pattern
- **FR-011**: System MUST add idempotency status headers to responses
- **FR-012**: System MUST validate idempotency key format (max length, allowed characters)

### Non-Functional Requirements

- **NFR-001**: Idempotency check overhead MUST be < 5ms for in-memory store
- **NFR-002**: Library MUST be thread-safe for concurrent request handling
- **NFR-003**: Library MUST have zero external dependencies beyond ASP.NET Core
- **NFR-004**: Library MUST support .NET 10.0

### Key Entities

- **IdempotencyKey**: Value object representing the unique key (validated format)
- **CachedResponse**: Stores status code, headers, body bytes, created timestamp, expiry
- **IdempotencyOptions**: Configuration for storage, TTL, header name, fingerprinting
- **IIdempotencyStore**: Interface for response storage/retrieval

## Technical Approach

### Middleware vs Endpoint Filter

**Decision**: Use **Endpoint Filter** (not middleware) because:
1. Integrates with Minimal API route builder pattern
2. Allows per-endpoint configuration
3. Better access to endpoint metadata (attributes)
4. Follows DKNet pattern from `FluentEndpointMapperExtensions`

### Storage Strategy

1. **In-Memory** (default for development): `ConcurrentDictionary` with background cleanup
2. **Distributed Cache**: `IDistributedCache` integration (Redis, SQL, etc.)
3. **Custom**: `IIdempotencyStore` interface for specialized needs

### Concurrency Handling

Use **lock-and-wait** pattern:
1. Try to acquire lock for key
2. If locked by another request, wait with timeout
3. If lock acquired, check cache → execute if miss → store result → release lock

### Response Serialization

Store minimal response data:
```csharp
record CachedResponse(
    int StatusCode,
    Dictionary<string, string[]> Headers,
    byte[] Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? BodyHash  // For fingerprinting
);
```

## Out of Scope (v1.0)

- GraphQL support
- gRPC support
- Automatic retry logic for clients
- Response compression in cache
- Metrics/telemetry integration (future version)
