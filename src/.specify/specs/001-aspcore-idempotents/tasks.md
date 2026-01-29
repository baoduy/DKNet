# Tasks: DKNet.AspCore.Idempotents

**Input**: Design documents from `/specs/001-aspcore-idempotents/`  
**Prerequisites**: plan.md (required), spec.md (required)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1-US6)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create `DKNet.AspCore.Idempotents/DKNet.AspCore.Idempotents.csproj` with NuGet metadata
- [ ] T002 [P] Create `AspCore.Idempotents.Tests/AspCore.Idempotents.Tests.csproj` test project
- [ ] T003 [P] Create `DKNet.AspCore.Idempotents/README.md` with usage documentation
- [ ] T004 Add projects to `DKNet.FW.sln` under AspNet solution folder
- [ ] T005 [P] Create `DKNet.AspCore.Idempotents/IdempotencyConstants.cs` with header names and defaults
- [ ] T006 [P] Create `AspCore.Idempotents.Tests/GlobalUsings.cs` with common imports

**Checkpoint**: Projects compile, solution builds

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests First (TDD)

- [ ] T007 Create `AspCore.Idempotents.Tests/Unit/IdempotencyKeyTests.cs` with validation scenarios
- [ ] T008 [P] Create `AspCore.Idempotents.Tests/Unit/IdempotencyOptionsTests.cs` with configuration tests
- [ ] T009 [P] Create `AspCore.Idempotents.Tests/Unit/CachedResponseTests.cs` with serialization tests

### Implementation

- [ ] T010 Create `DKNet.AspCore.Idempotents/IdempotencyKey.cs` - value object with validation
  - Max length: 256 chars
  - Allowed chars: alphanumeric, hyphen, underscore
  - `TryCreate()` static factory method
- [ ] T011 [P] Create `DKNet.AspCore.Idempotents/CachedResponse.cs` - response storage record
- [ ] T012 [P] Create `DKNet.AspCore.Idempotents/IdempotencyOptions.cs` - configuration class
- [ ] T013 Create `DKNet.AspCore.Idempotents/Stores/IIdempotencyStore.cs` - storage interface

**Checkpoint**: Foundation ready - T007-T009 tests pass

---

## Phase 3: User Story 1 - Basic Idempotency via Attribute (Priority: P1) 🎯 MVP

**Goal**: Enable `[Idempotent]` attribute on endpoints, cache and return responses

**Independent Test**: Decorate endpoint with `[Idempotent]`, verify request caching works

### Tests for User Story 1

- [ ] T014 Create `AspCore.Idempotents.Tests/Unit/InMemoryStoreTests.cs`
  - `GetAsync_WhenKeyNotExists_ReturnsNull`
  - `SetAsync_WhenCalled_StoresResponse`
  - `GetAsync_WhenKeyExists_ReturnsCachedResponse`
  - `GetAsync_WhenExpired_ReturnsNull`
- [ ] T015 Create `AspCore.Idempotents.Tests/Unit/IdempotencyEndpointFilterTests.cs`
  - `InvokeAsync_WhenNoIdempotencyKey_Returns400`
  - `InvokeAsync_WhenCacheMiss_ExecutesAndCaches`
  - `InvokeAsync_WhenCacheHit_ReturnsCachedResponse`
- [ ] T016 Create `AspCore.Idempotents.Tests/Integration/IdempotencyFilterTests.cs`
  - `Post_WithIdempotencyKey_ExecutesOnce`
  - `Post_WithSameKey_ReturnsCachedResponse`
  - `Post_WithoutKey_Returns400BadRequest`

### Implementation for User Story 1

- [ ] T017 Create `DKNet.AspCore.Idempotents/Stores/InMemoryIdempotencyStore.cs`
  - `ConcurrentDictionary<string, CachedResponse>` storage
  - Background timer for expired entry cleanup
  - Implement `IIdempotencyStore`
- [ ] T018 Create `DKNet.AspCore.Idempotents/Internals/ResponseSerializer.cs`
  - Serialize `HttpResponse` to `CachedResponse`
  - Deserialize `CachedResponse` to write back to `HttpResponse`
- [ ] T019 Create `DKNet.AspCore.Idempotents/Internals/IdempotencyKeyValidator.cs`
  - Extract key from request headers
  - Validate format and length
- [ ] T020 Create `DKNet.AspCore.Idempotents/Filters/IdempotencyEndpointFilter.cs`
  - Implement `IEndpointFilter`
  - Extract idempotency key from header
  - Check cache, execute or return cached
  - Add response headers
- [ ] T021 Create `DKNet.AspCore.Idempotents/IdempotentAttribute.cs`
  - `[Idempotent]` attribute with optional TTL
  - Mark endpoints for idempotency
- [ ] T022 Create `DKNet.AspCore.Idempotents/IdempotencySetups.cs`
  - `AddIdempotency()` extension method
  - Register `IIdempotencyStore` (default: in-memory)
  - Register filter factory

**Checkpoint**: US1 complete - basic idempotency works with attribute

---

## Phase 4: User Story 2 - Fluent Configuration for Minimal APIs (Priority: P1)

**Goal**: Enable `.RequireIdempotency()` on route builders

**Independent Test**: Use fluent API on Minimal API endpoint, verify idempotency enforced

### Tests for User Story 2

- [ ] T023 Create `AspCore.Idempotents.Tests/Integration/MinimalApiIdempotencyTests.cs`
  - `RequireIdempotency_OnEndpoint_EnforcesIdempotency`
  - `RequireIdempotency_WithCustomTtl_UsesConfiguredTtl`
  - `RequireIdempotency_OnGroup_AppliesToAllEndpoints`

### Implementation for User Story 2

- [ ] T024 Create `DKNet.AspCore.Idempotents/Extensions/RouteBuilderIdempotencyExtensions.cs`
  - `RequireIdempotency()` for `RouteHandlerBuilder`
  - `RequireIdempotency(TimeSpan ttl)` overload
  - `RequireIdempotency()` for `RouteGroupBuilder`
- [ ] T025 Update `IdempotencySetups.cs` to support filter factory pattern

**Checkpoint**: US2 complete - Minimal API fluent configuration works

---

## Phase 5: User Story 3 - Configurable Storage Backend (Priority: P2)

**Goal**: Support in-memory, distributed cache, and custom storage

**Independent Test**: Configure Redis storage, verify responses stored in Redis

### Tests for User Story 3

- [ ] T026 Create `AspCore.Idempotents.Tests/Unit/DistributedStoreTests.cs`
  - `GetAsync_UsesDistributedCache`
  - `SetAsync_UsesDistributedCache`
  - `GetAsync_WhenCacheMiss_ReturnsNull`
- [ ] T027 Create `AspCore.Idempotents.Tests/Fixtures/RedisFixture.cs`
  - TestContainers.Redis setup
  - `IAsyncLifetime` implementation
- [ ] T028 Create `AspCore.Idempotents.Tests/Integration/DistributedStoreIntegrationTests.cs`
  - `Idempotency_WithRedis_StoresInRedis`
  - `Idempotency_WithRedis_SurvivesAppRestart` (simulated)

### Implementation for User Story 3

- [ ] T029 Create `DKNet.AspCore.Idempotents/Stores/DistributedIdempotencyStore.cs`
  - Implement `IIdempotencyStore` using `IDistributedCache`
  - JSON serialization for `CachedResponse`
- [ ] T030 Update `IdempotencyOptions.cs`
  - Add `UseDistributedCache()` method
  - Add `UseStore<TStore>()` method
  - Add `UseInMemory()` method (explicit)
- [ ] T031 Update `IdempotencySetups.cs` for storage selection

**Checkpoint**: US3 complete - multiple storage backends supported

---

## Phase 6: User Story 4 - Concurrent Request Handling (Priority: P2)

**Goal**: Handle concurrent requests with same key safely

**Independent Test**: Send 10 concurrent requests, verify only one execution

### Tests for User Story 4

- [ ] T032 Create `AspCore.Idempotents.Tests/Integration/ConcurrentRequestTests.cs`
  - `ConcurrentRequests_WithSameKey_OnlyOneExecutes`
  - `ConcurrentRequests_WaitMode_AllGetSameResponse`
  - `ConcurrentRequests_RejectMode_Returns409`
- [ ] T033 Create `AspCore.Idempotents.Tests/Unit/LockManagerTests.cs`
  - `TryAcquireLock_WhenAvailable_ReturnsTrue`
  - `TryAcquireLock_WhenLocked_WaitsAndRetries`
  - `ReleaseLock_WhenHeld_ReleasesLock`

### Implementation for User Story 4

- [ ] T034 Create `DKNet.AspCore.Idempotents/Internals/LockManager.cs`
  - In-memory: `SemaphoreSlim` per key
  - Distributed: Use `IDistributedCache` with lock pattern
- [ ] T035 Update `InMemoryIdempotencyStore.cs` with lock methods
- [ ] T036 Update `DistributedIdempotencyStore.cs` with distributed lock
- [ ] T037 Update `IdempotencyEndpointFilter.cs` to use locking

**Checkpoint**: US4 complete - concurrent requests handled safely

---

## Phase 7: User Story 5 - Key Fingerprinting (Priority: P3)

**Goal**: Optionally validate request body hash matches

**Independent Test**: Enable fingerprinting, reject mismatched body

### Tests for User Story 5

- [ ] T038 Create `AspCore.Idempotents.Tests/Unit/FingerprintingTests.cs`
  - `WithFingerprint_SameBodyHash_ReturnsCached`
  - `WithFingerprint_DifferentBodyHash_Returns422`
  - `WithoutFingerprint_DifferentBody_ReturnsCached`

### Implementation for User Story 5

- [ ] T039 Create `DKNet.AspCore.Idempotents/Internals/RequestFingerprinter.cs`
  - Compute SHA256 hash of request body
- [ ] T040 Update `IdempotencyEndpointFilter.cs` for fingerprint validation
- [ ] T041 Update `IdempotentAttribute.cs` with `EnableFingerprinting` property

**Checkpoint**: US5 complete - fingerprinting works

---

## Phase 8: User Story 6 - Response Headers (Priority: P3)

**Goal**: Add idempotency status headers to responses

**Independent Test**: Check response headers for idempotency metadata

### Tests for User Story 6

- [ ] T042 Create `AspCore.Idempotents.Tests/Unit/ResponseHeaderTests.cs`
  - `FreshExecution_AddsCreatedHeader`
  - `CachedResponse_AddsCachedHeader`
  - `CachedResponse_AddsExpiresHeader`

### Implementation for User Story 6

- [ ] T043 Update `IdempotencyEndpointFilter.cs` to add status headers
- [ ] T044 Update `IdempotencyConstants.cs` with response header names

**Checkpoint**: US6 complete - response headers present

---

## Phase 9: Documentation & Polish

**Purpose**: Final documentation and cleanup

- [ ] T045 Update `README.md` with complete usage examples
- [ ] T046 Add XML documentation to all public types
- [ ] T047 Create `CHANGELOG.md` for version 1.0.0
- [ ] T048 Run `dotnet format` and fix any issues
- [ ] T049 Verify zero warnings with `dotnet build -c Release`
- [ ] T050 Generate test coverage report, verify 85%+ coverage

**Checkpoint**: Ready for PR

---

## Summary

| Phase | Tasks | Dependencies |
|-------|-------|--------------|
| 1. Setup | T001-T006 | None |
| 2. Foundational | T007-T013 | Phase 1 |
| 3. US1 (P1 MVP) | T014-T022 | Phase 2 |
| 4. US2 (P1) | T023-T025 | Phase 3 |
| 5. US3 (P2) | T026-T031 | Phase 4 |
| 6. US4 (P2) | T032-T037 | Phase 5 |
| 7. US5 (P3) | T038-T041 | Phase 6 |
| 8. US6 (P3) | T042-T044 | Phase 7 |
| 9. Polish | T045-T050 | Phase 8 |

**Total Tasks**: 50  
**Estimated Effort**: 3-5 days  
**Critical Path**: T001 → T010 → T017 → T020 → T022 (MVP)
