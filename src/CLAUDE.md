# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Authoritative Context

This repo maintains a richer set of AI-agent docs that you should treat as primary sources before any non-trivial change:

- `AGENTS.md` — full coding/testing/PR conventions for this repo (commit format, test patterns, anti-patterns).
- `memory-bank/README.md` — index of project context (architecture, current focus, standards).
- `memory-bank/activeContext.md` — what's actively being worked on right now.
- `memory-bank/copilot-rules.md` and `memory-bank/copilot-quick-reference.md` — detailed standards and code templates.
- `memory-bank/libraries/README.md` — scenario → DKNet package routing for API implementation work.
- `.github/copilot-instructions.md` — Copilot agent rules (largely overlapping with AGENTS.md).

When the user asks for a feature in a specific area, load the relevant memory-bank doc(s) before generating code rather than guessing patterns.

## Common Commands

Run from `/Users/steven/_CODE/DRUNK/DKNet/src` (where `DKNet.FW.sln` lives):

```bash
dotnet restore DKNet.FW.sln
dotnet build   DKNet.FW.sln -c Debug                         # must produce zero warnings
dotnet test    DKNet.FW.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
dotnet test    EfCore.Specifications.Tests                   # single project
dotnet test    --filter "FullyQualifiedName~DynamicAnd_WithMultipleConditions"   # single test
dotnet format                                                # before committing
./nuget.sh pack && ./verify_nuget_package.sh                 # build + sanity-check NuGet packages
```

`global.json` pins SDK `10.0.100` (rollForward `latestMinor`). `Directory.Build.props` enables `TreatWarningsAsErrors=true`, nullable reference types, and `GenerateDocumentationFile=true` solution-wide — any new warning breaks the build.

Integration tests require Docker (TestContainers spins up real SQL Server). Do not switch them to InMemory.

## Repository Layout

The solution is organized by capability area, each with its package projects and a sibling `*.Tests` project:

- **`Core/`** — `DKNet.Fw.Extensions` (core utilities, type extensions), `DKNet.RandomCreator` (Bogus-based test data).
- **`EfCore/`** — the largest area. Aggregate roots and abstractions (`DKNet.EfCore.Abstractions`), specifications + dynamic predicates (`DKNet.EfCore.Specifications`), repos (`DKNet.EfCore.Repos`), domain events (`DKNet.EfCore.Events`), hooks, audit logs, encryption, row-level data authorization, DTO generator (with Roslyn analyzer in `DKNet.EfCore.DtoGenerator.Analyzers`).
- **`AspNet/`** — ASP.NET Core extensions, `DKNet.AspCore.Idempotency` (+ MsSql store), `DKNet.AspCore.Tasks` background tasks. *Current focus area.*
- **`Services/`** — multi-provider blob storage (`Azure`, `AwsS3`, `Local` under `DKNet.Svc.BlobStorage.*`), encryption, PDF generators, transformation.
- **`SlimBus/`** — `DKNet.SlimBus.Extensions` provides CQRS / messaging glue.
- **`Aspire/`** — `Aspire.Hosting.ServiceBus` for .NET Aspire host integration.
- **`DKNet.AspCore.Tasks/`** at the root sits next to `AspCore.Tasks.Tests/` (a top-level test project for the host-style task scheduler).
- **`memory-bank/`** — the AI knowledge base described above. Update `activeContext.md` and `progress.md` after meaningful work.
- **`nupkgs/`**, **`TestResults/`**, **`coverage-report-final/`** — generated artifacts; never commit.

## Architectural Big Picture

DKNet is a layered framework expressing **DDD + Onion Architecture** at the package boundaries:

- **Aggregate roots** (`AggregateRoot` in `DKNet.EfCore.Abstractions`) carry domain events; rich entities mutate via methods (e.g. `Product.UpdatePrice`) that call `AddEvent(...)`. The events are dispatched by `DKNet.EfCore.Events` during SaveChanges.
- **Repositories** (`DKNet.EfCore.Repos`) abstract persistence and consume **Specifications** (`DKNet.EfCore.Specifications`) — composable query objects whose `Criteria`, `Includes`, and `OrderBy` are combined with LinqKit (`.And()`, `.Or()`).
- **Dynamic Predicate Builder** is the signature feature of `DKNet.EfCore.Specifications`. It builds runtime EF Core predicates from `(propertyName, FilterOperation, value)` triples with type/enum-safe conversion. Required call shape:
  ```csharp
  var predicate = PredicateBuilder.New<Product>()
      .And(p => p.IsActive)
      .DynamicAnd(b => b.With("Price", FilterOperations.GreaterThan, 100m));
  var results = _db.Products.AsExpandable().Where(predicate).ToListAsync();
  ```
  `.AsExpandable()` is mandatory — LinqKit cannot translate the predicate without it. `DynamicAnd` / `DynamicOr` already null-handle internally; do not reintroduce manual null checks around them.
- **CQRS via SlimBus** — handlers (`IRequestHandler<TCommand, TResult>`) receive commands, fetch via repos, mutate aggregates, and persist; domain events are emitted automatically from the aggregate.
- **Hooks + AuditLogs + Encryption + DataAuthorization** are EF Core SaveChanges interceptors layered on the same `DbContext`. They are independent and opt-in via DI extensions on the consuming app.
- **Idempotency** (`DKNet.AspCore.Idempotency`) is endpoint middleware backed by a pluggable store (`MsSqlStore`); HTTP response code caching, composite key validation, and exception handling around the cache are part of the *current active development*.

## Conventions That Trip Up Generated Code

- **Test naming**: `MethodName_Scenario_ExpectedBehavior` (e.g. `DynamicAnd_WithMultipleConditions_CombinesCorrectly`). xUnit + Shouldly + TestContainers.MsSql; avoid mocking the DB. Use `IAsyncLifetime` fixtures, not `IClassFixture` shared state.
- **File header**: every `.cs` file gets the copyright block (template in `memory-bank/`). Don't omit when creating new files.
- **XML docs** are mandatory on all public APIs (`<summary>`, `<param>`, `<returns>`, relevant `<exception>`); `GenerateDocumentationFile=true` will warn → error otherwise.
- **Private fields**: `_camelCase`. Async methods end in `Async`. Extensions live in static classes under `/Extensions`.
- **EF Core**: always `await`, default to `AsNoTracking()` for reads, push filtering to the DB, prefer `Include`/projections over per-row fetches. For dynamic predicates remember `.AsExpandable()`.
- **Verifying SQL** in tests: `query.ToQueryString()` and assert against the generated SQL in addition to the materialized rows — this is a recurring pattern in `EfCore.Specifications.Tests`.
- **Commit messages** follow Conventional Commits with scopes such as `specifications`, `repository`, `extensions`, `tests`, `docs` (see `AGENTS.md` for full examples). PRs should call out coverage impact and any breaking changes.

## Workflow Notes Specific to This Repo

- The user's `dev` branch is the integration branch (also the default PR base). Recent history shows many small `up` / fix commits — squash where it makes sense.
- After merging a meaningful change, update `memory-bank/activeContext.md` (and `progress.md` for larger items) so future Copilot/Claude sessions stay oriented.
- Diagrams are tracked: `Diagram.drawio` / `Diagram.png` at the repo root and `EfCore/Diagrams/`. If you change an architectural relationship, update the relevant diagram or call it out in the PR.
