# Implementation Plan: AI Library Skills for DKNet RESTful API Development

**Branch**: `003-ai-library-skills` | **Date**: 2026-03-16 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `/specs/003-ai-library-skills/spec.md`

---

## Summary

Create structured AI skill files — one per DKNet library — stored under `memory-bank/libraries/`, plus a master index and composition patterns. Update `AGENTS.md` and `memory-bank/README.md` to reference them. The skill files enable AI agents (GitHub Copilot, Cursor, etc.) to correctly choose and apply the right DKNet library when helping developers build ASP.NET Core Minimal API REST endpoints.

---

## Technical Context

**Language/Version**: C# 13 / .NET 10+  
**Primary Dependencies**: DKNet library suite (15 libraries)  
**Storage**: N/A — documentation feature  
**Testing**: N/A — docs only; verified by reviewing against real source API surface  
**Target Platform**: AI agent context loading (GitHub Copilot, Cursor, Claude Code)  
**Project Type**: Documentation / AI context library  
**Performance Goals**: N/A  
**Constraints**: Files must be under `memory-bank/` to be picked up by the existing agent loading chain  
**Scale/Scope**: 15 skill files + 1 master index + 1 composition patterns document = 17 files total

---

## Constitution Check

- [x] **Runtime baseline**: All code examples target .NET 10+ / `net10.0`. No .NET 9 examples.
- [x] **Zero warnings**: All code snippets reviewed for `TreatWarningsAsErrors=true` compliance.
- [x] **Nullability contracts**: All examples use nullable reference types with explicit null handling.
- [x] **Documentation contract**: Skill files are self-documenting markdown. No new `.cs` public API introduced.
- [x] **Test-first gate**: Each skill file includes at least one test example using xUnit + Shouldly + TestContainers.
- [x] **Real DB integration**: All test examples use TestContainers, never `UseInMemoryDatabase`.
- [x] **Pattern integrity**: Specification/Repository, dynamic predicates, `.AsExpandable()` shown consistently.
- [x] **Security/secrets**: All examples use `configuration["ConnectionStrings:Default"]` — no hardcoded credentials.

---

## Project Structure

### Documentation (this feature)

```text
specs/003-ai-library-skills/
├── plan.md              ← This file
├── research.md          ← Phase 0 research output
├── data-model.md        ← Skill file schema / structure contract
├── quickstart.md        ← How to create and maintain skill files
└── contracts/
    └── skill-file-template.md   ← Reusable template for new library skill files
```

### Deliverable Files (repository root)

```text
memory-bank/
└── libraries/
    ├── README.md                         ← Master index: scenario → library mapping
    ├── 01-slimbus-extensions.md          ← DKNet.SlimBus.Extensions
    ├── 02-aspcore-extensions.md          ← DKNet.AspCore.Extensions (SlimBus)
    ├── 03-idempotency.md                 ← DKNet.AspCore.Idempotency
    ├── 04-idempotency-mssql.md           ← DKNet.AspCore.Idempotency.MsSqlStore
    ├── 05-efcore-repos-abstractions.md   ← DKNet.EfCore.Repos.Abstractions
    ├── 06-efcore-repos.md                ← DKNet.EfCore.Repos
    ├── 07-efcore-specifications.md       ← DKNet.EfCore.Specifications
    ├── 08-efcore-auditlogs.md            ← DKNet.EfCore.AuditLogs
    ├── 09-efcore-dataauthorization.md    ← DKNet.EfCore.DataAuthorization
    ├── 10-efcore-encryption.md           ← DKNet.EfCore.Encryption
    ├── 11-efcore-events.md               ← DKNet.EfCore.Events
    ├── 12-efcore-extensions.md           ← DKNet.EfCore.Extensions
    ├── 13-efcore-dtogenerator.md         ← DKNet.EfCore.DtoGenerator
    ├── 14-aspcore-tasks.md               ← DKNet.AspCore.Tasks
    ├── 15-fw-extensions.md               ← DKNet.Fw.Extensions
    └── composition-patterns.md           ← Multi-library recipe patterns

AGENTS.md                                 ← Updated to reference memory-bank/libraries/
memory-bank/README.md                     ← Updated navigation entry for libraries/
```

---

## Complexity Tracking

No constitution violations. This feature is purely additive documentation with no code changes.

---

## Post-Design Constitution Re-Check

- [x] **Runtime baseline**: All new skill file examples remain .NET 10+ aligned.
- [x] **Zero warnings**: No compilable source code added; examples follow warning-safe conventions.
- [x] **Nullability contracts**: All examples use nullable-aware signatures and checks.
- [x] **Documentation contract**: Public API docs unaffected; feature output is documentation only.
- [x] **Test-first + real infra**: Skill files include xUnit/Shouldly/TestContainers patterns and explicitly reject `UseInMemoryDatabase`.
- [x] **Pattern integrity**: Specs/repositories/dynamic predicate guidance remains consistent.
- [x] **Security/secrets**: No secrets were introduced; configuration-based examples only.

## Agent Context Update Notes

- Attempted to run `.specify/scripts/bash/update-agent-context.sh copilot` per workflow.
- Script failed in this workspace because it expects a template outside the current workspace boundary (`/Users/steven/_CODE/DRUNK/DKNet/.specify/templates/agent-file-template.md`).
- Fallback applied: created `src/.github/agents/copilot-instructions.md` manually with the new planning context so downstream agent loading still works.
