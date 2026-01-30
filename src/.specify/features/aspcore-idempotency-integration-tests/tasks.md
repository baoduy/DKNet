# Tasks: AspCore.Idempotency Integration Tests

**Feature**: AspCore.Idempotency Integration Tests
**Module**: AspNet/AspCore.Idempotency.Tests
**Branch**: 001-idempotent-key
**Target**: .NET 10+

## Task List (Dependency Ordered)

### 1. Baseline Setup
- [ ] Verify target framework in solution props supports .NET 10.
- [ ] Add required package references to `AspCore.Idempotency.Tests` (if missing):
  - `Microsoft.AspNetCore.Mvc.Testing`
  - `Microsoft.Extensions.Caching.Memory`

### 2. Test Infrastructure
- [ ] Add test DTOs in `Models/`:
  - `TestCreateRequest`
  - `TestCreateResponse`
- [ ] Add `TestProgram` with Minimal API endpoints for idempotency testing.
- [ ] Add `TestWebApplicationFactory` to configure test services.
- [ ] Add `IdempotencyWebAppFixture` implementing `IAsyncLifetime`.

### 3. P0 Integration Tests (Core Behavior)
- [ ] Add `Integration/IdempotencyEndpointFilterTests.cs`.
- [ ] Validate:
  - First request returns created status.
  - Duplicate request returns cached status.
  - Missing/invalid key returns 400.
  - Response headers include idempotency status.

### 4. P1 Integration Tests (Concurrency)
- [ ] Add `Integration/IdempotencyConcurrencyTests.cs`.
- [ ] Validate:
  - Parallel requests execute only once.
  - ReturnCachedResult behavior.
  - ConflictResponse behavior (409).

### 5. P1 Integration Tests (Configuration)
- [ ] Add `Integration/IdempotencyConfigurationTests.cs`.
- [ ] Validate:
  - Per-endpoint TTL override.
  - Per-endpoint conflict handling.
  - Global vs per-endpoint precedence.

### 6. Verification
- [ ] Run `dotnet test` for `AspCore.Idempotency.Tests`.
- [ ] Confirm zero warnings.

## Notes
- Do not add XML docs to test classes.
- Use file headers and standard DKNet naming conventions.
- Use `.AsExpandable()` where LinqKit predicates are involved (if applicable).
