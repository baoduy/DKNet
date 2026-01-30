# Quick Start Guide: AspCore.Idempotency Integration Tests

**Purpose**: Provide step-by-step instructions for running and understanding the integration tests.

---

## Prerequisites

- .NET 10.0 SDK installed
- Visual Studio 2022, Rider, or VS Code
- DKNet.AspCore.Idempotency library compiled

---

## Quick Setup

1. **Navigate to test project**:
   ```bash
   cd /Users/steven/_CODE/DRUNK/DKNet/src/AspNet/AspCore.Idempotency.Tests
   ```

2. **Restore packages**:
   ```bash
   dotnet restore
   ```

3. **Run all tests**:
   ```bash
   dotnet test
   ```

4. **Run specific test class**:
   ```bash
   dotnet test --filter "FullyQualifiedName~IdempotencyEndpointFilterTests"
   ```

---

## Test Scenarios by User Story

### User Story 1: Core Behavior (P1)

**Run**: `dotnet test --filter "FullyQualifiedName~IdempotencyEndpointFilterTests"`

**Key Scenarios**:
1. **First Request Creates Resource**:
   - POST /api/orders with idempotency key
   - Response: 200 OK, Idempotency-Key-Status: created
   - Order ID returned

2. **Duplicate Request Returns Cached**:
   - POST /api/orders with SAME idempotency key
   - Response: 200 OK, Idempotency-Key-Status: cached
   - SAME order ID returned (proves idempotency)

3. **Missing Key Rejected**:
   - POST /api/orders WITHOUT idempotency key
   - Response: 400 Bad Request

4. **Invalid Key Rejected**:
   - POST /api/orders with empty/too-long key
   - Response: 400 Bad Request

**Expected Output**:
```
✓ RequireIdempotency_WhenFirstRequest_ReturnsCreatedStatus
✓ RequireIdempotency_WhenDuplicateRequest_ReturnsCachedStatus
✓ RequireIdempotency_WhenMissingKey_Returns400BadRequest
✓ RequireIdempotency_WhenInvalidKey_Returns400BadRequest
```

---

### User Story 2: Concurrency (P1)

**Run**: `dotnet test --filter "FullyQualifiedName~IdempotencyConcurrencyTests"`

**Key Scenarios**:
1. **Simultaneous Requests - One Executes**:
   - Fire 2 requests at exact same time with same key
   - Only ONE creates order
   - Both return same order ID (cached response)

2. **ReturnCachedResult Mode**:
   - Second request WAITS for first to complete
   - Returns cached response

3. **ConflictResponse Mode**:
   - Second request returns 409 Conflict immediately
   - First request completes normally

**Expected Output**:
```
✓ RequireIdempotency_WhenSimultaneousRequests_OnlyOneExecutes
✓ RequireIdempotency_WhenConcurrentWithReturnCached_BothGetSameResponse
✓ RequireIdempotency_WhenConcurrentWithConflict_SecondGets409
```

---

### User Story 3: Configuration (P1)

**Run**: `dotnet test --filter "FullyQualifiedName~IdempotencyConfigurationTests"`

**Key Scenarios**:
1. **Per-Endpoint TTL Override**:
   - Endpoint A: 1-hour TTL
   - Endpoint B: 1-day TTL
   - Each respects its own TTL

2. **Per-Endpoint Conflict Handling**:
   - Endpoint A: ReturnCachedResult
   - Endpoint B: ConflictResponse
   - Each behaves differently

**Expected Output**:
```
✓ RequireIdempotency_WhenCustomTtl_RespectsPerEndpointExpiration
✓ RequireIdempotency_WhenCustomConflictHandling_RespectsPerEndpointConfig
✓ RequireIdempotency_WhenNoOverride_UsesGlobalConfiguration
```

---

## Test Endpoint Paths

The test application exposes these endpoints:

| Endpoint | Method | Idempotency | Configuration |
|----------|--------|-------------|---------------|
| `/api/orders` | POST | Required | Global config |
| `/api/payments` | POST | Required | Custom TTL (1 hour) |
| `/api/refunds` | POST | Required | ConflictResponse mode |

---

## How to Debug a Failing Test

1. **Open test class** in IDE
2. **Set breakpoint** on test method
3. **Run test in debug mode**
4. **Inspect**:
   - Request headers (especially `Idempotency-Key`)
   - Response status code
   - Response headers (`Idempotency-Key-Status`, `Idempotency-Key-Expires`)
   - Response body

**Common Issues**:
- **401/403**: Authentication/authorization not disabled in test app
- **404**: Endpoint path mismatch
- **500**: Exception in endpoint handler (check logs)
- **Flaky concurrency test**: Increase delay/timeout

---

## Manual Testing with HttpRepl (Optional)

If you want to manually test the endpoints:

1. **Run test application** (if detached from tests):
   ```bash
   # Not applicable - WebApplicationFactory is in-process
   ```

2. **Use curl**:
   ```bash
   # First request
   curl -X POST http://localhost:5000/api/orders \
     -H "Content-Type: application/json" \
     -H "Idempotency-Key: test-key-123" \
     -d '{"productName": "Widget", "quantity": 5}'

   # Duplicate request (same key)
   curl -X POST http://localhost:5000/api/orders \
     -H "Content-Type: application/json" \
     -H "Idempotency-Key: test-key-123" \
     -d '{"productName": "Widget", "quantity": 5}'
   ```

---

## Understanding Test Fixtures

### IdempotencyWebAppFixture

**Purpose**: Provides a test web application with idempotency configured

**Lifecycle**:
- `InitializeAsync()`: Set up in-memory cache, configure app
- Tests run: Share same app instance per test class
- `DisposeAsync()`: Clean up resources

**Usage**:
```csharp
public class IdempotencyEndpointFilterTests : IClassFixture<IdempotencyWebAppFixture>
{
    private readonly IdempotencyWebAppFixture _fixture;

    public IdempotencyEndpointFilterTests(IdempotencyWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyTest()
    {
        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("...", ...);
        // assertions
    }
}
```

---

## Test Data Models

### TestCreateRequest
```csharp
public record TestCreateRequest
{
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
}
```

### TestCreateResponse
```csharp
public record TestCreateResponse
{
    public Guid OrderId { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
```

**Why these models?**
- Simple, realistic
- Serializable to JSON
- Deterministic for testing (except Guid/DateTime)

---

## Troubleshooting

### Test fails: "Services have not been registered"
**Cause**: `.AddIdempotency()` not called in test app startup
**Fix**: Check `IdempotencyWebAppFixture` configures services

### Test fails: "Idempotency key header not found"
**Cause**: Missing `Idempotency-Key` header in request
**Fix**: Ensure test adds header: `request.Headers.Add("Idempotency-Key", "...")`

### Concurrency test is flaky
**Cause**: Timing issues, requests not truly simultaneous
**Fix**: Use `Task.WhenAll()` to fire requests in parallel

### Cache not clearing between tests
**Cause**: Shared fixture reuses cache
**Fix**: Use unique idempotency keys per test (Guid.NewGuid())

---

## Next Steps

1. Run tests: `dotnet test`
2. Review test code for patterns
3. Add new test scenarios as needed
4. Update configuration tests when adding new options

---

**Document Version**: 1.0
**Last Updated**: January 30, 2026
