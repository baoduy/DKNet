# Research: DKNet.AspCore.Idempotents

## Research Summary

This document captures research findings and decisions made during the planning phase.

---

## 1. Industry Standards for Idempotency

### Header Naming Conventions

| Provider | Header Name | Notes |
|----------|-------------|-------|
| **Stripe** | `Idempotency-Key` | Industry standard, 24h TTL |
| **PayPal** | `PayPal-Request-Id` | Vendor-specific |
| **AWS** | `X-Amz-Idempotency-Token` | Service-specific |
| **Square** | `Idempotency-Key` | Follows Stripe |
| **Adyen** | `Idempotency-Key` | Follows Stripe |

**Decision**: Use `Idempotency-Key` as default (most widely adopted). Allow configuration for custom header names.

**Rationale**: Stripe's pattern has become the de facto standard. Most API clients and documentation use this convention.

### TTL Standards

| Provider | Default TTL | Max TTL |
|----------|-------------|---------|
| Stripe | 24 hours | 24 hours |
| PayPal | 72 hours | 72 hours |
| Square | 24 hours | 45 days |

**Decision**: Default 24 hours, configurable per-endpoint.

**Rationale**: 24 hours covers most retry scenarios while limiting storage growth.

---

## 2. ASP.NET Core Implementation Patterns

### Middleware vs Endpoint Filter vs Action Filter

| Approach | Pros | Cons | DKNet Fit |
|----------|------|------|-----------|
| **Middleware** | Global, early in pipeline | No endpoint metadata, always runs | ❌ |
| **Endpoint Filter** | Per-endpoint config, Minimal API native | .NET 7+ only | ✅ |
| **Action Filter** | MVC native, attribute-based | MVC only, different pipeline | ⚠️ |

**Decision**: Use Endpoint Filter as primary mechanism with MVC adapter via `IFilterFactory`.

**Rationale**: 
- DKNet extensively uses Minimal APIs (`FluentEndpointMapperExtensions`)
- Endpoint filters allow per-endpoint configuration via fluent API
- `IFilterFactory` on the attribute enables MVC support without code duplication

### Response Capture Technique

**Challenge**: Endpoint filters run after response is written; need to capture response for caching.

**Options**:
1. ❌ Replace `HttpContext.Response.Body` with `MemoryStream` - fragile, issues with chunked encoding
2. ✅ Use `IResult` inspection for Minimal APIs - clean, type-safe
3. ✅ Use `IActionResult` wrapper for MVC - established pattern

**Decision**: Capture at the result level, not stream level.

**Implementation**:
```csharp
// Minimal API: Wrap the endpoint result
var result = await next(context);
if (result is IValueHttpResult valueResult)
{
    // Serialize valueResult.Value
}

// MVC: Use ObjectResult inspection
if (context.Result is ObjectResult objectResult)
{
    // Serialize objectResult.Value
}
```

---

## 3. Storage Backend Analysis

### In-Memory Store

**Use Case**: Single-server deployments, development, testing.

**Implementation**:
```csharp
ConcurrentDictionary<string, CachedResponse> _cache;
ConcurrentDictionary<string, SemaphoreSlim> _locks;
Timer _cleanupTimer; // Remove expired entries every minute
```

**Pros**: Zero dependencies, fastest performance  
**Cons**: Lost on restart, no horizontal scaling

### Distributed Cache (IDistributedCache)

**Use Case**: Multi-server deployments, Redis, SQL Server.

**Implementation**:
```csharp
IDistributedCache _cache;
// Key format: "idempotency:{key}"
// Lock format: "idempotency:lock:{key}" with short TTL
```

**Pros**: Survives restarts, scales horizontally  
**Cons**: Network latency, serialization overhead

### Distributed Lock Pattern

**Challenge**: Prevent concurrent execution of same idempotency key across servers.

**Pattern**: Redis-style distributed lock
```
1. SET lock_key unique_id NX PX 30000  // Acquire
2. Execute operation
3. DEL lock_key IF value == unique_id  // Release
```

**IDistributedCache Implementation**:
```csharp
// Acquire: GetAsync, if null then SetAsync with short expiry
// Wait: Polling with exponential backoff
// Release: RemoveAsync (only if we set it)
```

**Decision**: Use simple lock pattern with configurable timeout. Complex Redlock not needed for idempotency use case.

---

## 4. Concurrent Request Handling

### Scenario Analysis

**Scenario 1**: Request A arrives, starts processing. Request B arrives with same key.

| Mode | Behavior | Use Case |
|------|----------|----------|
| **Wait** | B waits for A to complete, then gets cached response | Default, most common |
| **RejectWithConflict** | B immediately gets 409 Conflict | Strict, client handles retry |

**Decision**: Support both modes, default to Wait.

**Wait Mode Implementation**:
```
1. Try acquire lock (immediate)
2. If locked, poll with backoff until timeout
3. After lock acquired, check cache (another request may have completed)
4. If cache hit, release lock and return
5. If cache miss, execute, cache, release
```

**Timeout Handling**:
- Lock timeout exceeded → Return 503 Service Unavailable
- Include `Retry-After` header

---

## 5. Request Body Fingerprinting

### Purpose

Detect when a client accidentally reuses an idempotency key with a different request body.

### Implementation

```csharp
// Compute hash of request body
using var sha256 = SHA256.Create();
var hash = Convert.ToBase64String(sha256.ComputeHash(requestBody));

// Store with cached response
cachedResponse.RequestBodyHash = hash;

// On subsequent request, compare
if (cached.RequestBodyHash != currentHash)
    return 422 Unprocessable Entity;
```

**Decision**: Optional feature, disabled by default.

**Rationale**: 
- Some APIs intentionally support different bodies with same key
- Adds overhead (body must be buffered and hashed)
- Enable only when strict validation required

---

## 6. Response Caching Rules

### What to Cache

| Response Type | Cache? | Reason |
|--------------|--------|--------|
| 2xx Success | ✅ Always | Primary use case |
| 3xx Redirect | ⚠️ Configurable | Usually should cache |
| 4xx Client Error | ⚠️ Configurable | May want to retry with fix |
| 5xx Server Error | ❌ Default off | Transient, should retry |

**Decision**: Cache success by default. Option to cache errors.

### Headers to Exclude from Cache

Hop-by-hop headers should not be replayed:
- `Connection`
- `Keep-Alive`
- `Transfer-Encoding`
- `Upgrade`
- `Proxy-*`

**Implementation**: Filter these when storing and replaying.

---

## 7. Existing Library Comparison

### IdempotentAPI (NuGet)

**Repository**: https://github.com/ikyriak/IdempotentAPI

**Pros**:
- Mature, well-tested
- Supports distributed cache

**Cons**:
- MVC-only (no Minimal API support)
- Heavy dependencies
- Different API style than DKNet

### Hellang.Middleware.ProblemDetails

**Not idempotency-specific but useful for error responses.**

**Decision**: Use ASP.NET Core's built-in `IProblemDetailsService` for error responses.

---

## 8. Security Considerations

### Key Validation

**Threat**: SQL injection, command injection via malicious key values.

**Mitigation**: Strict key format validation
```csharp
// Only allow: a-z, A-Z, 0-9, hyphen, underscore
// Max length: 256 characters
// Reject: whitespace, special characters
```

### Cache Poisoning

**Threat**: Attacker caches malicious response for legitimate key.

**Mitigation**: 
- Keys should be unpredictable (UUIDs)
- Include user context in cache key (optional)
- TTL limits exposure window

### Denial of Service

**Threat**: Attacker fills cache with garbage keys.

**Mitigation**:
- Max key length limit
- Rate limiting (outside scope, use standard middleware)
- Storage limits (configurable max entries for in-memory)

---

## 9. Alternatives Considered

### Database-backed Store

**Considered**: EF Core integration for SQL-based storage.

**Rejected**: 
- Adds EF Core dependency
- IDistributedCache with SQL provider achieves same result
- Keep library lightweight

### Automatic Key Generation

**Considered**: Generate idempotency key from request hash if not provided.

**Rejected**:
- Violates explicit idempotency principle
- Client should own the key
- Different requests could hash to same value

### Response Compression

**Considered**: Compress cached response body.

**Deferred**: v2 feature. Current implementation stores raw bytes.

---

## 10. Final Decisions Summary

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Header name | `Idempotency-Key` | Industry standard |
| Default TTL | 24 hours | Balances coverage and storage |
| Implementation | Endpoint Filter | Minimal API native, per-endpoint config |
| Default store | In-memory | Zero dependencies for dev |
| Production store | IDistributedCache | Standard abstraction |
| Concurrency | Wait mode default | Most user-friendly |
| Fingerprinting | Disabled by default | Performance overhead |
| Error caching | Disabled by default | Prefer retry on errors |
