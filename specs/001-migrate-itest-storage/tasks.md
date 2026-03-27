# Tasks: Parallelize SQL Integration Tests

**Input**: Design documents from `/specs/001-migrate-itest-storage/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/itest-parallelization.openapi.yaml, quickstart.md

**Tests**: Tests are REQUIRED for behavior changes. Define failing tests before implementation tasks (red-green-refactor).

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish migration inventory, baseline metrics, and run configuration for parallelization work.

- [ ] T001 Create migration inventory document for SQL-dependent integration projects in `specs/001-migrate-itest-storage/research.md`
- [ ] T002 Capture pre-migration baseline timing and failure data for targeted projects in `specs/001-migrate-itest-storage/quickstart.md`
- [ ] T003 [P] Add test-run notes for repeated stability validation in `specs/001-migrate-itest-storage/quickstart.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build shared test-store profile and isolation conventions required by all stories.

**CRITICAL**: No user story implementation starts before this phase completes.

- [ ] T004 Define store profile matrix (SQLite/InMemory/SQL contract lane) for each targeted project in `specs/001-migrate-itest-storage/data-model.md`
- [ ] T005 Define migration exception criteria and approval flow in `specs/001-migrate-itest-storage/data-model.md`
- [ ] T006 [P] Align contract operations and payload fields with migration workflow in `specs/001-migrate-itest-storage/contracts/itest-parallelization.openapi.yaml`
- [ ] T007 [P] Add shared guidance for unique database naming and teardown guarantees in `specs/001-migrate-itest-storage/quickstart.md`
- [ ] T008 Record constitution-safe rule for retaining SQL provider-specific tests in `specs/001-migrate-itest-storage/research.md`

**Checkpoint**: Foundation is complete and user stories can proceed.

---

## Phase 3: User Story 1 - Migrate SQL-Dependent Test Projects (Priority: P1) 🎯 MVP

**Goal**: Convert SQL-dependent integration tests to parallel-safe isolated stores (SQLite first, InMemory only where relational behavior is not required).

**Independent Test**: Run each migrated project with parallel execution and confirm no shared-state contamination.

### Tests for User Story 1 (REQUIRED)

- [ ] T009 [P] [US1] Add failing isolation tests for fixture database uniqueness in `src/AspNet/AspCore.Idempotency.MsSqlStore.Tests/Integration/IdempotencyIntegrationTests.cs`
- [ ] T010 [P] [US1] Add failing fixture isolation tests for specifications project in `src/EfCore/EfCore.Specifications.Tests/SpecSetupTests.cs`
- [ ] T011 [P] [US1] Add failing parallel isolation tests for relational helpers project in `src/EfCore/EfCore.Relational.Helpers.Tests/DbContextHelpersAdvancedTests.cs`

### Implementation for User Story 1

- [x] T012 [US1] Refactor SQL container fixture to isolated SQL contract-lane profile in `src/AspNet/AspCore.Idempotency.MsSqlStore.Tests/Fixtures/ApiFixture.cs`
- [x] T013 [US1] Update idempotency integration tests to use migrated fixture lifecycle in `src/AspNet/AspCore.Idempotency.MsSqlStore.Tests/Integration/IdempotencyIntegrationTests.cs`
- [x] T014 [US1] Refactor specifications fixture from shared SQL container to isolated profile in `src/EfCore/EfCore.Specifications.Tests/Fixtures/TestDbFixture.cs`
- [x] T015 [US1] Update specifications setup assertions for new provider profile in `src/EfCore/EfCore.Specifications.Tests/SpecSetupTests.cs`
- [x] T016 [US1] Refactor relational helpers fixture to isolated profile in `src/EfCore/EfCore.Relational.Helpers.Tests/Fixtures/SqlServerFixture.cs`
- [x] T017 [US1] Update relational helper tests for migrated fixture behavior in `src/EfCore/EfCore.Relational.Helpers.Tests/DbContextHelperTests.cs`
- [x] T018 [US1] Update advanced helper tests for per-test database identity in `src/EfCore/EfCore.Relational.Helpers.Tests/DbContextHelpersAdvancedTests.cs`
- [ ] T019 [US1] Add explicit per-test teardown and disposal assertions across migrated fixtures in `src/EfCore/EfCore.Specifications.Tests/Fixtures/TestDbFixture.cs`

**Checkpoint**: SQL-dependent integration projects run with isolated parallel-safe stores.

---

## Phase 4: User Story 2 - Preserve Test Intent and Reliability (Priority: P2)

**Goal**: Ensure migrated tests still verify equivalent business behavior and preserve provider-specific contract coverage.

**Independent Test**: Execute representative migrated scenarios and retained SQL contract-lane scenarios; compare outcomes to baseline.

### Tests for User Story 2 (REQUIRED)

- [ ] T020 [P] [US2] Add failing equivalence assertions for idempotency result behavior in `src/AspNet/AspCore.Idempotency.MsSqlStore.Tests/Integration/IdempotencyIntegrationTests.cs`
- [ ] T021 [P] [US2] Add failing equivalence assertions for specifications query behavior in `src/EfCore/EfCore.Specifications.Tests/SpecSetupTests.cs`
- [ ] T022 [P] [US2] Add failing provider-specific contract tests retained for SQL lane in `src/EfCore/EfCore.Relational.Helpers.Tests/DbContextHelpersAdvancedTests.cs`

### Implementation for User Story 2

- [ ] T023 [US2] Implement behavior-equivalence verification helpers for idempotency tests in `src/AspNet/AspCore.Idempotency.MsSqlStore.Tests/Integration/IdempotencyIntegrationTests.cs`
- [ ] T024 [US2] Implement behavior-equivalence verification helpers for specifications tests in `src/EfCore/EfCore.Specifications.Tests/SpecSetupTests.cs`
- [x] T025 [US2] Implement retained SQL contract-lane fixture path for provider-specific tests in `src/EfCore/EfCore.Relational.Helpers.Tests/Fixtures/SqlServerFixture.cs`
- [x] T026 [US2] Document migration exception cases and mitigation in `specs/001-migrate-itest-storage/research.md`
- [x] T027 [US2] Align migration exception schema examples with implemented contract-lane rules in `specs/001-migrate-itest-storage/contracts/itest-parallelization.openapi.yaml`

**Checkpoint**: Reliability and behavioral equivalence are demonstrated with explicit exception handling.

---

## Phase 5: User Story 3 - Enable Faster Parallel CI Feedback (Priority: P3)

**Goal**: Enable stable parallel execution in CI and produce measurable cycle-time improvements.

**Independent Test**: Run targeted projects in parallel mode repeatedly in CI-like execution and confirm throughput and stability metrics.

### Tests for User Story 3 (REQUIRED)

- [ ] T028 [P] [US3] Add failing CI-verification test notes for repeated parallel execution in `specs/001-migrate-itest-storage/quickstart.md`
- [ ] T029 [P] [US3] Add failing acceptance checks for contention-free repeated runs in `specs/001-migrate-itest-storage/plan.md`

### Implementation for User Story 3

- [x] T030 [US3] Update CI pipeline test command strategy for targeted parallel projects in `azure-pipelines.yml`
- [x] T031 [US3] Update PR pipeline parallel test execution for targeted projects in `ai-pr-review.azure-pipelines.yml`
- [x] T032 [US3] Add 10-run stability validation procedure and evidence capture template in `specs/001-migrate-itest-storage/quickstart.md`
- [x] T033 [US3] Record post-migration performance comparison criteria and thresholds in `specs/001-migrate-itest-storage/plan.md`

**Checkpoint**: Parallel CI execution is enabled and measurable improvement tracking is in place.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation consistency, and quality gates.

- [ ] T034 [P] Reconcile spec, plan, research, and quickstart wording for final terminology consistency in `specs/001-migrate-itest-storage/spec.md`
- [ ] T035 [P] Run full quickstart command sequence and record final outcomes in `specs/001-migrate-itest-storage/quickstart.md`
- [ ] T036 Run `dotnet build DKNet.FW.sln --configuration Release` and log zero-warning confirmation in `specs/001-migrate-itest-storage/quickstart.md`
- [ ] T037 Run `dotnet test DKNet.FW.sln --configuration Release --settings coverage.runsettings --collect "XPlat Code Coverage"` and log evidence in `specs/001-migrate-itest-storage/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1): no dependencies.
- Foundational (Phase 2): depends on Setup completion; blocks all user stories.
- User Story phases (Phase 3 to Phase 5): depend on Foundational completion.
- Polish (Phase 6): depends on completion of desired user stories.

### User Story Dependencies

- US1 (P1): starts after Phase 2; no dependency on other stories.
- US2 (P2): starts after Phase 2 and should validate behavior from US1 outputs.
- US3 (P3): starts after Phase 2 but practically depends on US1 and US2 migration outputs for CI proof.

### Within Each User Story

- Write tests first and confirm they fail.
- Implement fixture/store-profile changes.
- Update test logic and assertions.
- Validate story independently before moving forward.

### Parallel Opportunities

- Setup tasks marked [P] can run in parallel.
- Foundational tasks marked [P] can run in parallel.
- In US1, T009 to T011 can run in parallel.
- In US2, T020 to T022 can run in parallel.
- In US3, T028 and T029 can run in parallel.
- Polish tasks marked [P] can run in parallel.

---

## Parallel Example: User Story 1

```bash
# Parallel test creation for US1
Task T009: Isolation tests in AspCore.Idempotency.MsSqlStore.Tests
Task T010: Isolation tests in EfCore.Specifications.Tests
Task T011: Isolation tests in EfCore.Relational.Helpers.Tests

# Parallel fixture migration after tests are in place
Task T012: Migrate ApiFixture
Task T014: Migrate TestDbFixture
Task T016: Migrate SqlServerFixture
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Complete Phase 1 and Phase 2.
2. Complete US1 tasks (T009 to T019).
3. Validate targeted migrated projects in parallel-safe mode.
4. Stop and confirm MVP value before expanding scope.

### Incremental Delivery

1. Deliver US1 migration for core project set.
2. Deliver US2 behavioral equivalence and exception governance.
3. Deliver US3 CI throughput and reliability optimization.
4. Complete polish and final evidence capture.

### Parallel Team Strategy

1. Team aligns on foundational profile rules.
2. Developer A: AspNet test migration tasks.
3. Developer B: EfCore specifications migration tasks.
4. Developer C: EfCore relational helpers and CI pipeline tasks.
