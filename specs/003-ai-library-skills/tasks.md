# Tasks: AI Library Skills for DKNet RESTful API Development

**Input**: Design documents from `/specs/003-ai-library-skills/`  
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `quickstart.md`, `contracts/`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare file structure and baseline documentation scaffolding.

- [X] T001 Create the library skills folder and base artifact placeholders in `memory-bank/libraries/`
- [X] T002 Create/align feature documentation artifacts in `specs/003-ai-library-skills/research.md`, `specs/003-ai-library-skills/data-model.md`, `specs/003-ai-library-skills/quickstart.md`, and `specs/003-ai-library-skills/contracts/skill-file-template.md`
- [X] T003 [P] Record canonical DKNet library inventory and source-path mapping in `specs/003-ai-library-skills/research.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build shared guidance that all user stories depend on.

**⚠️ CRITICAL**: No user story work starts before this phase is complete.

- [X] T004 Build the master index skeleton (scenario routing + full library list) in `memory-bank/libraries/README.md`
- [X] T005 Define mandatory skill-file schema and validation rules in `specs/003-ai-library-skills/data-model.md`
- [X] T006 [P] Document contributor workflow for creating/updating skill files in `specs/003-ai-library-skills/quickstart.md`
- [X] T007 [P] Add memory-bank navigation and load order entry for library skills in `memory-bank/README.md`
- [X] T008 Add agent loading requirement for library skills index in `AGENTS.md`
- [X] T009 Add spec-agent context fallback guidance in `.github/agents/copilot-instructions.md`

**Checkpoint**: Foundation complete - user stories can proceed.

---

## Phase 3: User Story 1 - AI Generates Correct CQRS REST Endpoint (Priority: P1) 🎯 MVP

**Goal**: Ensure AI uses `DKNet.SlimBus.Extensions` + `DKNet.AspCore.Extensions` correctly for POST/GET/paged endpoint generation.

**Independent Test**: Ask AI to scaffold one POST endpoint, one GET endpoint, and one paged GET endpoint; output must use the correct Fluents interfaces and one-liner endpoint mapping.

### Tests/Validation for User Story 1

- [X] T010 [P] [US1] Add xUnit/Shouldly/TestContainers validation example for command/query handler generation in `memory-bank/libraries/01-slimbus-extensions.md`
- [X] T011 [P] [US1] Add xUnit/Shouldly/TestContainers validation example for Minimal API mapping behavior in `memory-bank/libraries/02-aspcore-extensions.md`

### Implementation for User Story 1

- [X] T012 [US1] Author the complete `DKNet.SlimBus.Extensions` skill file (purpose, setup, key APIs, anti-patterns, quick decisions) in `memory-bank/libraries/01-slimbus-extensions.md`
- [X] T013 [US1] Author the complete `DKNet.AspCore.Extensions` skill file (MapPost/MapGet/MapGetPage guidance + anti-patterns) in `memory-bank/libraries/02-aspcore-extensions.md`
- [X] T014 [US1] Update CQRS endpoint scenario routing for US1 in `memory-bank/libraries/README.md`

**Checkpoint**: User Story 1 independently testable via AI prompt-to-code checks.

---

## Phase 4: User Story 2 - AI Applies Idempotency to State-Mutating Endpoints (Priority: P2)

**Goal**: Ensure AI applies idempotency middleware/store correctly for POST/PUT/PATCH/DELETE endpoints.

**Independent Test**: Ask AI to add idempotency to an existing POST endpoint and require SQL-store variant; output must include `.WithIdempotency()`, `AddIdempotency(...)`, and `AddIdempotencyMsSqlStore(...)` with config-based connection string.

### Tests/Validation for User Story 2

- [X] T015 [P] [US2] Add duplicate-request replay validation test example in `memory-bank/libraries/03-idempotency.md`
- [X] T016 [P] [US2] Add SQL-backed idempotency persistence validation test example in `memory-bank/libraries/04-idempotency-mssql.md`

### Implementation for User Story 2

- [X] T017 [US2] Author the complete `DKNet.AspCore.Idempotency` skill file (options, endpoint filter usage, anti-patterns) in `memory-bank/libraries/03-idempotency.md`
- [X] T018 [US2] Author the complete `DKNet.AspCore.Idempotency.MsSqlStore` skill file (registration, security, durability guidance) in `memory-bank/libraries/04-idempotency-mssql.md`
- [X] T019 [US2] Add in-memory vs SQL-store quick decision guidance to scenario routing in `memory-bank/libraries/README.md`

**Checkpoint**: User Story 2 independently testable with duplicate-request prompts.

---

## Phase 5: User Story 3 - AI Uses Repositories and Specifications for Data Access (Priority: P3)

**Goal**: Ensure AI generates repository/specification-based query and command data access, including dynamic predicates.

**Independent Test**: Ask AI for filtered Product query + write operation; output must use `IReadRepository<T>/IWriteRepository<T>`, `Specification<T>`, and dynamic predicates with `.AsExpandable()` when required.

### Tests/Validation for User Story 3

- [X] T020 [P] [US3] Add repository abstraction validation examples (read vs write interface usage) in `memory-bank/libraries/05-efcore-repos-abstractions.md`
- [X] T021 [P] [US3] Add concrete repository DI/projection validation examples in `memory-bank/libraries/06-efcore-repos.md`
- [X] T022 [P] [US3] Add specification/dynamic predicate validation examples in `memory-bank/libraries/07-efcore-specifications.md`

### Implementation for User Story 3

- [X] T023 [US3] Author the complete `DKNet.EfCore.Repos.Abstractions` skill file with `IReadRepository<T>`, `IWriteRepository<T>`, and decision guide in `memory-bank/libraries/05-efcore-repos-abstractions.md`
- [X] T024 [US3] Author the complete `DKNet.EfCore.Repos` skill file with registration and usage patterns in `memory-bank/libraries/06-efcore-repos.md`
- [X] T025 [US3] Author the complete `DKNet.EfCore.Specifications` skill file with `Specification<T>`, `DynamicAnd/DynamicOr`, and `.AsExpandable()` rules in `memory-bank/libraries/07-efcore-specifications.md`
- [X] T026 [US3] Update query/data-access scenario routing in `memory-bank/libraries/README.md`

**Checkpoint**: User Story 3 independently testable with data-access generation prompts.

---

## Phase 6: User Story 4 - AI Adds Cross-Cutting Concerns Correctly (Priority: P4)

**Goal**: Ensure AI correctly applies audit logging, data authorization, encryption, events, and supporting platform libraries.

**Independent Test**: Ask AI to add audit logging, then encryption, then row-level authorization to a sample entity/API; output must select the right library and wiring each time.

### Tests/Validation for User Story 4

- [X] T027 [P] [US4] Add structured audit-log validation examples in `memory-bank/libraries/08-efcore-auditlogs.md`
- [X] T028 [P] [US4] Add ownership-filter validation examples in `memory-bank/libraries/09-efcore-dataauthorization.md`
- [X] T029 [P] [US4] Add encrypted-field validation examples and security notes in `memory-bank/libraries/10-efcore-encryption.md`
- [X] T030 [P] [US4] Add domain-event publishing validation examples in `memory-bank/libraries/11-efcore-events.md`

### Implementation for User Story 4

- [X] T031 [P] [US4] Author the complete `DKNet.EfCore.AuditLogs` skill file in `memory-bank/libraries/08-efcore-auditlogs.md`
- [X] T032 [P] [US4] Author the complete `DKNet.EfCore.DataAuthorization` skill file in `memory-bank/libraries/09-efcore-dataauthorization.md`
- [X] T033 [P] [US4] Author the complete `DKNet.EfCore.Encryption` skill file in `memory-bank/libraries/10-efcore-encryption.md`
- [X] T034 [P] [US4] Author the complete `DKNet.EfCore.Events` skill file in `memory-bank/libraries/11-efcore-events.md`
- [X] T035 [P] [US4] Author the complete `DKNet.EfCore.Extensions` skill file in `memory-bank/libraries/12-efcore-extensions.md`
- [X] T036 [P] [US4] Author supporting library skills in `memory-bank/libraries/13-efcore-dtogenerator.md`, `memory-bank/libraries/14-aspcore-tasks.md`, and `memory-bank/libraries/15-fw-extensions.md`
- [X] T037 [P] [US4] Update cross-cutting scenario routing and disambiguation guidance in `memory-bank/libraries/README.md`

**Checkpoint**: User Story 4 independently testable via cross-cutting concern prompts.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency pass across all stories and artifacts.

- [X] T038 Create full multi-library recipe documentation in `memory-bank/libraries/composition-patterns.md`
- [X] T039 [P] Validate all 15 skill files include required template sections from `specs/003-ai-library-skills/contracts/skill-file-template.md` across `memory-bank/libraries/`
- [X] T040 [P] Verify all skill-file test examples use xUnit + Shouldly + TestContainers and avoid `UseInMemoryDatabase` across `memory-bank/libraries/`
- [X] T041 [P] Normalize .NET/version/security wording and quick-decision sections across `memory-bank/libraries/*.md`
- [X] T042 Finalize feature quality checklist notes in `specs/003-ai-library-skills/checklists/requirements.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: Starts immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1 completion; blocks all user stories.
- **Phase 3-6 (User Stories)**: Depend on Phase 2 completion.
- **Phase 7 (Polish)**: Depends on completion of selected user stories.

### User Story Dependencies

- **US1 (P1)**: No dependency on other user stories.
- **US2 (P2)**: Depends on US1 only for shared index consistency; remains independently testable.
- **US3 (P3)**: Independent after foundational phase.
- **US4 (P4)**: Independent after foundational phase.

### Suggested Delivery Order

- MVP: **US1 only**
- Next increments: **US2**, **US3**, **US4**
- Finish with **Phase 7 polish**

---

## Parallel Execution Examples

### User Story 1

- Run T010 and T011 in parallel (different files).
- Run T012 and T013 in parallel after validation tasks.

### User Story 2

- Run T015 and T016 in parallel.
- T017 and T018 can proceed in parallel, then merge via T019.

### User Story 3

- Run T020, T021, T022 in parallel.
- Run T023, T024, T025 in parallel, then merge via T026.

### User Story 4

- Run T027-T030 in parallel.
- Run T031-T036 in parallel (different files), then consolidate via T037.

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 1 and Phase 2.
2. Complete US1 tasks (T010-T014).
3. Validate AI-generated POST/GET/paged endpoint output against US1 independent test.

### Incremental Delivery

1. Deliver US1 (endpoint generation reliability).
2. Deliver US2 (idempotency correctness).
3. Deliver US3 (repository/specification correctness).
4. Deliver US4 (cross-cutting concern correctness).
5. Run Phase 7 quality normalization.

### Team Parallelization

1. One developer owns shared routing/index (`memory-bank/libraries/README.md`).
2. Other developers own separate skill files by story in parallel.
3. Final reviewer performs Phase 7 consistency sweep.

---

## Notes

- Tasks marked **[P]** are safe to execute concurrently.
- User story tasks include **[US1]-[US4]** labels for traceability.
- Every task includes an explicit file path as required.
- The resulting skills set should be immediately usable by AI agents through `memory-bank/libraries/README.md`.
