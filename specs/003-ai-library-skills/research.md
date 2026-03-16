# Research: AI Library Skills for DKNet RESTful API Development

**Phase**: 0 — Resolve unknowns before design  
**Date**: 2026-03-16

---

## Research Questions & Decisions

### 1. Where should skill files live to be auto-loaded by AI agents?

**Decision**: `memory-bank/libraries/`  
**Rationale**: The existing agent loading chain already references `memory-bank/README.md` from both `AGENTS.md` and `.github/copilot-instructions.md`. Adding `libraries/` as a subdirectory with its own `README.md` keeps the discovery path consistent with current conventions — no new tool configuration required.  
**Alternatives considered**: `.github/skills/` (too GitHub-specific, not loaded by Cursor), `.specify/memory/libraries/` (used for spec tooling only, not agent code context).

---

### 2. What is the canonical skill file structure for maximum AI agent parse reliability?

**Decision**: Fixed markdown headings in a predictable order: Purpose → Minimum Requirements → When To Use → When NOT To Use → Installation → Setup → Key API Surface → Usage Pattern → Anti-Patterns → Composition → Test Example.  
**Rationale**: AI agents parse structured markdown well when headings are consistent across all files. A fixed schema allows the agent to skip sections it doesn't need without confusion.  
**Alternatives considered**: Free-form narrative (hard to navigate programmatically), JSON/YAML (not human-friendly for inline code examples).

---

### 3. Which libraries are used exclusively for REST API construction vs. general EF Core use?

**Decision**: All 15 libraries are included. Even "general" EF Core libraries (Encryption, DataAuthorization, Events) are legitimately applied in REST API contexts and must be covered to prevent AI from reaching for third-party alternatives.  
**Rationale**: If a skill file is missing, the AI will invent usage patterns or suggest MediatR / other alternatives. Every library must be documented.

---

### 4. How does `DKNet.AspCore.Extensions` in the `SlimBus/` folder differ from the one in `AspNet/`?

**Decision**: The `SlimBus/DKNet.AspCore.Extensions` package contains the Minimal API endpoint mappers (`FluentsEndpointMapperExtensions` — `MapPost`, `MapGet`, `MapGetPage`, `MapPut`, `MapPatch`, `MapDelete`, `ProducesCommons`). The `AspNet/DKNet.AspCore.Extensions` contains `ResultResponseExtensions`, `ProblemDetailsExtensions`, `PagedResponse` — HTTP result helpers used *inside* the SlimBus mapper. Both are needed for a complete REST API; the `SlimBus/` one is the entry point, the `AspNet/` one is the support library it internally depends on.  
**Rationale**: Source code review of `SlimBus/DKNet.AspCore.Extensions/bin` vs `AspNet/DKNet.AspCore.Extensions/*.cs` confirms this distinction.

---

### 5. What is the concrete `Fluents` interface hierarchy developers must implement?

**Decision** (verified from `Fluents.cs`):

```
Commands (mutate state):
  Fluents.Requests.INoResponse           ← fire-and-forget command marker
  Fluents.Requests.IWitResponse<T>       ← command returning Result<T> marker  
  Fluents.Requests.IHandler<TCmd>        ← handler for INoResponse commands
  Fluents.Requests.IHandler<TCmd, T>     ← handler for IWitResponse<T> commands

Queries (return data):
  Fluents.Queries.IWitResponse<T>        ← single-result query marker
  Fluents.Queries.IWitPageResponse<T>    ← paged query marker
  Fluents.Queries.IHandler<TQ, T>        ← handler for single-result queries
  Fluents.Queries.IPageHandler<TQ, T>    ← handler for paged queries

Events:
  Fluents.EventsConsumers.IHandler<T>    ← event consumer handler
```

---

### 6. What is the correct DI registration for the CQRS + Minimal API stack?

**Decision** (verified from READMEs and source):
```csharp
// 1. Register SlimBus with EF Core auto-save behavior
services.AddSlimBusForEfCore(builder => builder
    .WithProviderMemory()
    .AutoDeclareFrom(typeof(CreateProductHandler).Assembly)
    .AddJsonSerializer());

// 2. Register generic repositories
services.AddGenericRepositories<AppDbContext>();

// 3. Register DbContext
services.AddDbContext<AppDbContext>(...);
```

---

### 7. Do EfCore.Hooks and EfCore.AuditLogs overlap? When to use each?

**Decision**: Use `EfCore.AuditLogs` when you need **structured field-level change records** (who changed what field from what value to what value). Use `EfCore.Hooks` when you need a **general-purpose lifecycle callback** (e.g., set timestamps, dispatch events, invalidate cache) that doesn't need to be a structured audit record. They can coexist.

---

### 8. Best practices for AI agent skill file maintenance

**Decision**: Each skill file includes a `## Version` section stating the minimum package version the documented API surface applies to. Maintainers update this section when the library API changes. The constitution's Documentation Gate (Principle IV) covers this.

---

## Summary

All NEEDS CLARIFICATION items resolved. Ready for Phase 1 design.

