# Implementation Plan: DKNet.AspCore.Idempotents

**Branch**: `001-aspcore-idempotents` | **Date**: 2026-01-29 | **Spec**: [spec.md](./spec.md)

## Summary

Implement an ASP.NET Core library providing idempotency support for API endpoints. The library uses endpoint filters to intercept requests, validate idempotency keys, cache responses, and return cached responses for duplicate requests. Supports in-memory and distributed cache storage backends.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**: `Microsoft.AspNetCore.Http.Abstractions`, `Microsoft.Extensions.Caching.Abstractions`  
**Storage**: `IDistributedCache` (Redis, SQL) or in-memory `ConcurrentDictionary`  
**Testing**: xUnit, Shouldly, TestContainers (for Redis integration tests)  
**Target Platform**: ASP.NET Core 10.0 (Minimal APIs + MVC)  
**Performance Goals**: < 5ms overhead for in-memory idempotency check  
**Constraints**: Zero external dependencies beyond ASP.NET Core; thread-safe  
**Scale/Scope**: Single library, ~15 public types

## Constitution Check

*GATE: Must pass before implementation. Verified against DKNet Framework Constitution v1.1.0*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Zero Warnings | ✅ PASS | Will use `TreatWarningsAsErrors=true` |
| II. Test-First | ✅ PASS | Unit tests + integration tests with TestContainers.Redis |
| III. Async-Everywhere | ✅ PASS | All store operations are async |
| IV. Documentation | ✅ PASS | XML docs for all public APIs |
| V. Security | ✅ PASS | No secrets; key validation prevents injection |
| VI. Pattern Compliance | ✅ PASS | Follows DKNet extension method patterns |

## Project Structure

### Documentation (this feature)

```text
.specify/specs/001-aspcore-idempotents/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Research findings
├── data-model.md        # Entity definitions
├── contracts/           # API contracts
│   └── idempotency-api.md
└── tasks.md             # Implementation tasks
```

### Source Code

```text
src/
├── DKNet.AspCore.Idempotents/           # Main library
│   ├── DKNet.AspCore.Idempotents.csproj
│   ├── README.md
│   ├── IdempotentAttribute.cs           # [Idempotent] attribute
│   ├── IdempotencyOptions.cs            # Configuration options
│   ├── IdempotencySetups.cs             # DI setup extensions
│   ├── IdempotencyKey.cs                # Value object
│   ├── CachedResponse.cs                # Response storage model
│   ├── IdempotencyConstants.cs          # Header names, defaults
│   ├── Stores/
│   │   ├── IIdempotencyStore.cs         # Storage abstraction
│   │   ├── InMemoryIdempotencyStore.cs  # ConcurrentDictionary impl
│   │   └── DistributedIdempotencyStore.cs # IDistributedCache impl
│   ├── Filters/
│   │   └── IdempotencyEndpointFilter.cs # Core filter logic
│   ├── Extensions/
│   │   └── RouteBuilderIdempotencyExtensions.cs # .RequireIdempotency()
│   └── Internals/
│       ├── IdempotencyKeyValidator.cs   # Key format validation
│       ├── ResponseSerializer.cs        # Response serialization
│       └── LockManager.cs               # Distributed lock handling
│
└── AspCore.Idempotents.Tests/           # Test project
    ├── AspCore.Idempotents.Tests.csproj
    ├── GlobalUsings.cs
    ├── Fixtures/
    │   ├── TestWebAppFixture.cs         # WebApplicationFactory
    │   └── RedisFixture.cs              # TestContainers.Redis
    ├── Unit/
    │   ├── IdempotencyKeyTests.cs
    │   ├── IdempotencyOptionsTests.cs
    │   ├── InMemoryStoreTests.cs
    │   └── ResponseSerializerTests.cs
    └── Integration/
        ├── IdempotencyFilterTests.cs
        ├── ConcurrentRequestTests.cs
        └── DistributedStoreTests.cs
```

## Research Findings

### Industry Standards

1. **Stripe API**: Uses `Idempotency-Key` header, 24-hour TTL, stores full response
2. **PayPal API**: Uses `PayPal-Request-Id` header
3. **AWS**: Uses `X-Amz-Idempotency-Token` for certain operations

**Decision**: Use `Idempotency-Key` header (Stripe pattern) as default, configurable.

### ASP.NET Core Patterns

1. **Endpoint Filters** (preferred for Minimal APIs): `IEndpointFilter` interface
2. **Action Filters** (MVC): `IAsyncActionFilter` interface
3. **Middleware** (global): Not suitable for per-endpoint configuration

**Decision**: Use Endpoint Filter as primary, with MVC filter adapter.

### Existing Libraries

| Library | Pros | Cons |
|---------|------|------|
| `Idempotency.NET` | Feature-rich | Heavy dependencies |
| `IdempotentAPI` | Simple | No Minimal API support |

**Decision**: Build custom to match DKNet patterns and support Minimal APIs natively.

## Data Model

### IdempotencyKey (Value Object)

```csharp
public readonly record struct IdempotencyKey
{
    public string Value { get; }
    
    // Validation: 1-256 chars, alphanumeric + hyphen/underscore
    public static bool TryCreate(string? value, out IdempotencyKey key);
}
```

### CachedResponse (Storage Entity)

```csharp
public sealed record CachedResponse(
    int StatusCode,
    IReadOnlyDictionary<string, string[]> Headers,
    byte[] Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? RequestBodyHash = null
);
```

### IdempotencyOptions (Configuration)

```csharp
public sealed class IdempotencyOptions
{
    public string HeaderName { get; set; } = "Idempotency-Key";
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(24);
    public int MaxKeyLength { get; set; } = 256;
    public bool EnableFingerprinting { get; set; } = false;
    public bool CacheErrorResponses { get; set; } = false;
    public ConcurrencyMode ConcurrencyMode { get; set; } = ConcurrencyMode.Wait;
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public enum ConcurrencyMode { Wait, RejectWithConflict }
```

## API Contracts

### Service Registration

```csharp
// Basic setup with in-memory store
services.AddIdempotency();

// With options
services.AddIdempotency(options =>
{
    options.DefaultTtl = TimeSpan.FromHours(12);
    options.HeaderName = "X-Idempotency-Key";
    options.EnableFingerprinting = true;
});

// With distributed cache (Redis)
services.AddIdempotency(options =>
{
    options.UseDistributedCache();
});

// With custom store
services.AddIdempotency(options =>
{
    options.UseStore<MyCustomStore>();
});
```

### Minimal API Usage

```csharp
app.MapPost("/orders", CreateOrder)
    .RequireIdempotency();

app.MapPost("/payments", CreatePayment)
    .RequireIdempotency(ttl: TimeSpan.FromHours(48));

// Group-level
var api = app.MapGroup("/api/v1").RequireIdempotency();
api.MapPost("/orders", CreateOrder);
```

### MVC Controller Usage

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    [Idempotent]
    public async Task<IActionResult> Create(CreateOrderRequest request) { }
    
    [HttpPost("bulk")]
    [Idempotent(Ttl = "48:00:00", EnableFingerprinting = true)]
    public async Task<IActionResult> CreateBulk(CreateBulkRequest request) { }
}
```

### IIdempotencyStore Interface

```csharp
public interface IIdempotencyStore
{
    Task<CachedResponse?> GetAsync(IdempotencyKey key, CancellationToken ct = default);
    Task SetAsync(IdempotencyKey key, CachedResponse response, CancellationToken ct = default);
    Task<bool> TryAcquireLockAsync(IdempotencyKey key, TimeSpan timeout, CancellationToken ct = default);
    Task ReleaseLockAsync(IdempotencyKey key, CancellationToken ct = default);
}
```

## Sequence Diagram

```
Client          Filter              Store               Endpoint
  │                │                   │                    │
  │──POST /orders──▶                   │                    │
  │ Key: abc123    │                   │                    │
  │                │──TryAcquireLock──▶│                    │
  │                │◀──────true────────│                    │
  │                │──────Get(abc)────▶│                    │
  │                │◀──────null────────│ (cache miss)      │
  │                │                   │                    │
  │                │───────────────────┼────Execute───────▶│
  │                │◀──────────────────┼───Response────────│
  │                │                   │                    │
  │                │──Set(abc,resp)───▶│                    │
  │                │──ReleaseLock─────▶│                    │
  │◀───Response────│                   │                    │
  │ + Status:created                   │                    │
```

## Implementation Phases

### Phase 1: Core Infrastructure (Foundation)
- Project setup with NuGet metadata
- `IdempotencyKey` value object with validation
- `CachedResponse` record
- `IdempotencyOptions` configuration
- `IIdempotencyStore` interface

### Phase 2: Storage Implementations
- `InMemoryIdempotencyStore` with cleanup timer
- `DistributedIdempotencyStore` using `IDistributedCache`
- Lock management for concurrency

### Phase 3: Endpoint Filter
- `IdempotencyEndpointFilter` core logic
- Request/response interception
- Header extraction and validation
- Response serialization/deserialization

### Phase 4: Integration APIs
- `AddIdempotency()` service extension
- `.RequireIdempotency()` route builder extension
- `[Idempotent]` attribute for MVC

### Phase 5: Testing
- Unit tests for all components
- Integration tests with WebApplicationFactory
- Redis integration tests with TestContainers

## Complexity Tracking

| Area | Complexity | Justification |
|------|------------|---------------|
| Endpoint Filter | Medium | Core pattern, well-documented in ASP.NET Core |
| Distributed Lock | Medium | Use existing patterns from IDistributedCache |
| Concurrent Handling | High | Race conditions require careful testing |
| Response Serialization | Low | Simple byte array storage |

## Dependencies to Add to Directory.Packages.props

```xml
<!-- Already present, no new dependencies needed -->
<PackageVersion Include="Microsoft.Extensions.Caching.Abstractions" Version="10.0.2" />
```

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Lock contention at scale | High | Configurable timeout, reject-on-conflict mode |
| Large response bodies | Medium | Configurable max body size, compression option |
| Clock skew in distributed TTL | Low | Use UTC, document behavior |

## Success Criteria

1. ✅ All unit tests pass with 90%+ coverage
2. ✅ Integration tests demonstrate full request lifecycle
3. ✅ Concurrent request handling verified (10 parallel requests, 1 execution)
4. ✅ Redis integration tests pass with TestContainers
5. ✅ Zero warnings on build
6. ✅ XML documentation for all public APIs
7. ✅ README with usage examples

## Next Steps

1. Create `tasks.md` with detailed implementation tasks
2. Set up project structure
3. Implement Phase 1 (TDD approach)
