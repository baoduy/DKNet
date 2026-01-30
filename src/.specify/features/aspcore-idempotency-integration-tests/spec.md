# Feature Specification: AspCore.Idempotency Integration Tests

**Feature**: WebApplicationFactory-based Integration Testing
**Module**: AspNet/AspCore.Idempotency.Tests
**Status**: Planning
**Priority**: High

---

## User Stories

### User Story 1 (P1): Core Idempotency Behavior Tests

**As a** developer testing the idempotency library  
**I want** integration tests that validate core idempotency behavior  
**So that** I can ensure requests are properly cached and duplicate detection works

**Acceptance Criteria**:
- [ ] First request executes and returns response with "created" status
- [ ] Duplicate request (same key) returns cached response with "cached" status
- [ ] Response body is identical between first and duplicate requests
- [ ] Missing idempotency key returns 400 Bad Request
- [ ] Invalid idempotency key returns 400 Bad Request
- [ ] Response headers include idempotency status information

**Test Scenarios**:
1. POST request with valid idempotency key → 200 OK, created status
2. POST request with same key → 200 OK, cached status, same response body
3. POST request without idempotency key → 400 Bad Request
4. POST request with empty idempotency key → 400 Bad Request
5. POST request with key exceeding max length → 400 Bad Request

**Dependencies**: None (foundational)

---

### User Story 2 (P1): Concurrent Request Handling

**As a** developer testing the idempotency library  
**I want** to validate behavior when multiple requests arrive simultaneously  
**So that** I can ensure distributed locking prevents duplicate execution

**Acceptance Criteria**:
- [ ] Two simultaneous requests with same key execute only once
- [ ] Second request waits for first to complete (ReturnCachedResult mode)
- [ ] Second request returns 409 Conflict (ConflictResponse mode)
- [ ] Distributed lock is properly acquired and released
- [ ] No race conditions occur under concurrent load

**Test Scenarios**:
1. Fire 2 requests simultaneously with same key → only 1 executes
2. ReturnCachedResult mode → both return same response
3. ConflictResponse mode → first succeeds, second gets 409
4. Fire 10 requests simultaneously → only 1 executes, all get consistent response

**Dependencies**: User Story 1 (requires working basic idempotency)

---

### User Story 3 (P1): Configuration Override Tests

**As a** developer testing the idempotency library  
**I want** to validate per-endpoint configuration overrides  
**So that** I can ensure different endpoints can have different TTLs and settings

**Acceptance Criteria**:
- [ ] Per-endpoint TTL override works (.RequireIdempotency(TimeSpan.FromMinutes(30)))
- [ ] Per-endpoint conflict handling can differ from global
- [ ] Global configuration applies when no override specified
- [ ] Per-endpoint configuration takes precedence over global

**Test Scenarios**:
1. Endpoint with custom TTL → response cached for specified duration
2. Endpoint with ConflictResponse → returns 409 on concurrent requests
3. Endpoint without overrides → uses global configuration
4. Multiple endpoints with different configs → each behaves independently

**Dependencies**: User Story 1 (requires working basic idempotency)

---

### User Story 4 (P2): Cache Expiration Validation

**As a** developer testing the idempotency library  
**I want** to validate that cached responses expire correctly  
**So that** I can ensure stale data is not returned indefinitely

**Acceptance Criteria**:
- [ ] Cached response expires after configured TTL
- [ ] New request after expiration executes (not cached)
- [ ] Expiration time is included in response headers
- [ ] Short TTLs (seconds) work correctly

**Test Scenarios**:
1. Request with 5-second TTL → cached for 5 seconds, then expires
2. Request after expiration → executes again, returns "created" status
3. Response headers include accurate expiration timestamp

**Dependencies**: User Story 1

---

### User Story 5 (P2): Error Response Caching

**As a** developer testing the idempotency library  
**I want** to validate error response caching behavior  
**So that** I can ensure errors are (or aren't) cached based on configuration

**Acceptance Criteria**:
- [ ] When CacheErrorResponses = false, 4xx/5xx not cached
- [ ] When CacheErrorResponses = true, 4xx/5xx are cached
- [ ] Successful responses always cached (2xx)
- [ ] Error response caching respects TTL

**Test Scenarios**:
1. Endpoint returns 500 error, CacheErrorResponses = false → not cached
2. Endpoint returns 500 error, CacheErrorResponses = true → cached
3. Endpoint returns 400 validation error → caching follows configuration
4. Endpoint returns 200 → always cached

**Dependencies**: User Story 1

---

### User Story 6 (P3): Request Fingerprinting Validation

**As a** developer testing the idempotency library  
**I want** to validate request body fingerprinting  
**So that** I can ensure duplicate keys with different bodies are rejected

**Acceptance Criteria**:
- [ ] When EnableFingerprinting = true, body hash is validated
- [ ] Same key with different body returns 422 Unprocessable Entity
- [ ] Same key with same body returns cached response
- [ ] When EnableFingerprinting = false, body not validated

**Test Scenarios**:
1. First request with body A → cached
2. Second request with same key, body B → 422 Unprocessable Entity
3. Third request with same key, body A → cached response returned
4. Fingerprinting disabled → different bodies allowed

**Dependencies**: User Story 1

---

### User Story 7 (P3): TestContainers Redis Integration

**As a** developer testing the idempotency library  
**I want** to test with real Redis using TestContainers  
**So that** I can validate distributed locking behavior in production-like environment

**Acceptance Criteria**:
- [ ] TestContainers.Redis spins up real Redis container
- [ ] Idempotency repository uses Redis cache
- [ ] Distributed locks work across test runs
- [ ] Container properly cleaned up after tests

**Test Scenarios**:
1. Start Redis container → idempotency works
2. Concurrent requests → Redis locks prevent duplicates
3. Cache expiration → Redis TTL works correctly
4. Stop container → no resource leaks

**Dependencies**: User Story 1, User Story 2

---

## Priority Summary

### P1 - Must Have (MVP)
- **User Story 1**: Core idempotency behavior (foundational)
- **User Story 2**: Concurrent request handling (critical feature)
- **User Story 3**: Configuration overrides (API validation)

### P2 - Should Have
- **User Story 4**: Cache expiration (important edge case)
- **User Story 5**: Error response caching (important feature)

### P3 - Could Have
- **User Story 6**: Request fingerprinting (advanced feature)
- **User Story 7**: TestContainers Redis (nice-to-have validation)

---

## Story Dependencies Graph

```
Foundational:
├── US1 (Core Behavior) ← START HERE
    ├── US2 (Concurrency) [depends on US1]
    ├── US3 (Configuration) [depends on US1]
    ├── US4 (Expiration) [depends on US1]
    ├── US5 (Error Caching) [depends on US1]
    ├── US6 (Fingerprinting) [depends on US1]
    └── US7 (Redis) [depends on US1, US2]
```

**Independence**: Stories US2-US6 are independent of each other (can be implemented in parallel after US1)

---

## MVP Scope

For initial implementation, focus on:
1. **User Story 1** (Core Behavior) - MUST complete first
2. **User Story 2** (Concurrency) - Critical for production use
3. **User Story 3** (Configuration) - Validates API design

Stories 4-7 can be added incrementally.

---

## Technical Notes

### Test Data
- Use predictable test DTOs (TestCreateRequest, TestCreateResponse)
- Use Guid.NewGuid().ToString() for idempotency keys (ensures isolation)
- Use realistic endpoint paths (/api/orders, /api/payments)

### WebApplicationFactory Pattern
```csharp
public class IdempotencyWebAppFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Configure services and endpoints
}
```

### Test Endpoint Example
```csharp
app.MapPost("/api/orders", (TestCreateRequest request) =>
{
    var orderId = Guid.NewGuid();
    return Results.Ok(new TestCreateResponse 
    { 
        OrderId = orderId, 
        Message = $"Order created: {request.ProductName}" 
    });
})
.RequireIdempotency();
```

### Assertion Examples
```csharp
// Shouldly assertions
response.StatusCode.ShouldBe(HttpStatusCode.OK);
response.Headers.GetValues("Idempotency-Key-Status").Single().ShouldBe("created");

var body = await response.Content.ReadFromJsonAsync<TestCreateResponse>();
body.ShouldNotBeNull();
body.OrderId.ShouldNotBe(Guid.Empty);
```

---

## Out of Scope

- Performance/load testing (separate concern)
- MVC Controller attribute testing (focus on Minimal API first)
- Custom IIdempotencyKeyRepository implementations (test default only)
- Multi-instance distributed scenarios (single app instance)

---

**Document Status**: Complete
**Ready for Task Generation**: Yes
**Estimated Total Effort**: 8-12 hours
