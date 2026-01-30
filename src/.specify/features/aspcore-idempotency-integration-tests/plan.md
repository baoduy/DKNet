# Implementation Plan: AspCore.Idempotency Integration Tests

**Feature**: WebApplicationFactory-based Integration Testing for DKNet.AspCore.Idempotency
**Project**: DKNet Framework - AspNet/AspCore.Idempotency.Tests
**Date**: January 30, 2026

---

## Overview

Implement comprehensive integration tests for the DKNet.AspCore.Idempotency library using WebApplicationFactory to create a real ASP.NET Core application with test endpoints. This will validate idempotency behavior in a realistic environment.

---

## Tech Stack

### Core Framework
- **.NET**: 10.0
- **ASP.NET Core**: 10.0 (Minimal APIs)
- **C#**: 13

### Testing Libraries
- **xUnit**: Test framework (already in project)
- **Shouldly**: Fluent assertions (already in project)
- **Microsoft.AspNetCore.Mvc.Testing**: WebApplicationFactory
- **Microsoft.Extensions.Caching.Memory**: In-memory distributed cache

### Optional Dependencies (P2)
- **TestContainers.Redis**: Real Redis for distributed lock testing

### Production Code Under Test
- **DKNet.AspCore.Idempotency**: Main library
  - `IdempotencyEndpointFilter`
  - `IIdempotencyKeyRepository`
  - `IdempotencyDistributedCacheRepository`
  - `IdempotencyOptions`
  - `.AddIdempotency()` extension
  - `.RequireIdempotency()` extension

---

## Project Structure

```
AspCore.Idempotency.Tests/
├── Fixtures/
│   ├── IdempotencyWebAppFixture.cs          # WebApplicationFactory with IAsyncLifetime
│   └── TestWebApplicationFactory.cs          # Custom factory configuration
├── Integration/
│   ├── IdempotencyEndpointFilterTests.cs    # Core idempotency tests (P0)
│   ├── IdempotencyConcurrencyTests.cs       # Concurrent request tests (P1)
│   └── IdempotencyConfigurationTests.cs     # Configuration override tests (P1)
├── Models/
│   ├── TestCreateRequest.cs                 # Test DTO for requests
│   └── TestCreateResponse.cs                # Test DTO for responses
├── TestProgram/
│   └── TestProgram.cs                       # Test web app configuration
└── Unit/
    └── (existing unit tests)
```

---

## Implementation Approach

### Phase 1: Infrastructure Setup
- Add Microsoft.AspNetCore.Mvc.Testing package
- Add Microsoft.Extensions.Caching.Memory package (if not present)
- Create test DTOs (request/response models)
- Set up file headers and global usings

### Phase 2: Fixture Creation
- Create TestWebApplicationFactory inheriting from WebApplicationFactory
- Implement IAsyncLifetime for proper setup/teardown
- Configure services (IDistributedCache, AddIdempotency)
- Create test endpoints using Minimal API
- Apply .RequireIdempotency() to endpoints

### Phase 3: Core Integration Tests (P0)
- Test successful first request
- Test cached duplicate request
- Test missing idempotency key (400 Bad Request)
- Test invalid idempotency key (400 Bad Request)
- Test response headers (Idempotency-Status: created vs cached)
- Test response body consistency

### Phase 4: Concurrency Tests (P1)
- Test simultaneous requests with same key
- Test distributed locking behavior
- Test ConflictResponse mode (409 Conflict)
- Test ReturnCachedResult mode (wait and return cached)

### Phase 5: Configuration Tests (P1)
- Test per-endpoint TTL override
- Test global vs per-endpoint configuration
- Test different conflict handling strategies
- Test cache expiration

### Phase 6: Advanced Scenarios (P2)
- Test different HTTP methods
- Test error response caching
- Test large response bodies
- Test request fingerprinting
- Optional: TestContainers.Redis integration

---

## Key Design Decisions

### 1. WebApplicationFactory vs In-Memory Test Server
- **Decision**: Use WebApplicationFactory
- **Rationale**: Official Microsoft recommendation, full integration testing, realistic HTTP pipeline

### 2. IDistributedCache Implementation
- **P0**: Use MemoryDistributedCache (fast, no external dependencies)
- **P2**: Add TestContainers.Redis for distributed lock testing

### 3. Test Endpoint Design
- **Pattern**: Realistic POST endpoint that "creates a resource"
- **Example**: POST /api/orders → returns order ID
- **Rationale**: Idempotency most relevant for state-changing operations

### 4. Fixture Lifetime
- **Decision**: One fixture per test class (IClassFixture<T>)
- **Rationale**: Fast test execution, isolated test classes

### 5. Test Organization
- **By Capability**: Core behavior, concurrency, configuration
- **Priority-Based**: P0 (must have), P1 (should have), P2 (could have)

---

## Dependencies

### Package Dependencies
```xml
<!-- Already in project -->
<PackageReference Include="xunit"/>
<PackageReference Include="Shouldly"/>

<!-- To be added -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing"/>

<!-- Optional (P2) -->
<PackageReference Include="TestContainers.Redis"/>
```

### Internal Dependencies
- Project reference to DKNet.AspCore.Idempotency (already present)

---

## Success Criteria

### Must Have (P0)
- [ ] WebApplicationFactory fixture successfully creates test web app
- [ ] At least 1 test endpoint with .RequireIdempotency() working
- [ ] All core idempotency behaviors tested (cache, missing key, invalid key)
- [ ] Response headers validated
- [ ] All tests pass
- [ ] Zero compiler warnings

### Should Have (P1)
- [ ] Concurrent request handling tested
- [ ] Configuration overrides validated
- [ ] Cache expiration tested

### Could Have (P2)
- [ ] TestContainers.Redis integration
- [ ] Error response caching tested
- [ ] Request fingerprinting validated

---

## Testing Strategy

### Test Naming Convention
Follow DKNet standards:
```csharp
[Fact]
public async Task RequireIdempotency_WhenFirstRequest_ReturnsCreatedStatus()
[Fact]
public async Task RequireIdempotency_WhenDuplicateRequest_ReturnsCachedStatus()
[Fact]
public async Task RequireIdempotency_WhenMissingKey_Returns400BadRequest()
```

### Assertion Style
Use Shouldly fluent assertions:
```csharp
response.StatusCode.ShouldBe(HttpStatusCode.OK);
response.Headers.GetValues("Idempotency-Key-Status").Single().ShouldBe("created");
```

### Arrange-Act-Assert
All tests follow AAA pattern:
```csharp
// Arrange
var client = _fixture.CreateClient();
var request = new TestCreateRequest { ProductName = "Widget" };
var idempotencyKey = Guid.NewGuid().ToString();

// Act
var response = await client.PostAsJsonAsync("/api/orders", request, headers: new { "Idempotency-Key" = idempotencyKey });

// Assert
response.StatusCode.ShouldBe(HttpStatusCode.OK);
```

---

## Risk Mitigation

### Risk 1: Test Isolation
- **Risk**: Cached responses from one test affecting another
- **Mitigation**: Use unique idempotency keys (Guid.NewGuid()) per test

### Risk 2: Timing Issues in Concurrency Tests
- **Risk**: Flaky tests due to race conditions
- **Mitigation**: Use proper synchronization (Task.WhenAll), generous timeouts

### Risk 3: WebApplicationFactory Disposal
- **Risk**: Resources not cleaned up properly
- **Mitigation**: Implement IAsyncLifetime, ensure proper disposal

---

## Performance Considerations

- **Fast Tests**: MemoryDistributedCache for P0 tests (no I/O)
- **Realistic Tests**: TestContainers.Redis for P2 (validates distributed locks)
- **Parallel Execution**: Tests can run in parallel (unique keys ensure isolation)

---

## Documentation Requirements

- XML comments NOT required for test code (NoWarn in csproj)
- File headers required (copyright, license)
- Inline comments for complex test scenarios

---

## Next Steps

1. Update AspCore.Idempotency.Tests.csproj with new packages
2. Create test DTOs (Models/)
3. Create WebApplicationFactory fixture (Fixtures/)
4. Create test program with endpoints (TestProgram/)
5. Implement P0 integration tests (Integration/)
6. Implement P1 concurrency tests
7. Implement P1 configuration tests
8. Optional: Implement P2 Redis tests

---

**Status**: Ready for Implementation
**Estimated Effort**: 8-12 hours
**Priority**: High (Critical for library validation)
