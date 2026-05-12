# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository. The same applies to any AI coding assistant — for finer-grained DKNet-specific rules, see `src/CLAUDE.md`, `src/AGENTS.md`, and `src/memory-bank/`.

## Repository at a Glance

**DKNet Framework** — a .NET 10 library suite of NuGet packages for building enterprise applications around **Domain-Driven Design (DDD)** and **Onion Architecture**. Published packages include EF Core extensions, ASP.NET Core utilities (Idempotency, Tasks), CQRS/messaging (SlimBus), blob storage adapters, encryption, PDF generation, and Aspire integrations.

- **Solution file**: `src/DKNet.FW.sln`
- **SDK**: pinned by `src/global.json` to `10.0.100` (`rollForward: latestMinor`).
- **Default branch**: `dev` (integration). `main` is release. Push feature work to a topic branch.
- **CI**: `.github/workflows/build-test-coverage.yml` runs build → test → coverage → SonarCloud on every PR to `main`/`dev`. Coverage gate: 80% (project-level targets in `TESTING_STRATEGY.md` are stricter).

## Top-Level Layout

```
/                      Repo root — docs, license, pipelines
├── src/               All code. Run dotnet commands from here.
│   ├── DKNet.FW.sln   Solution aggregating ~40 projects
│   ├── Core/          DKNet.Fw.Extensions, RandomCreator
│   ├── EfCore/        Largest area: Abstractions, Specifications, Repos,
│   │                  Events, Hooks, AuditLogs, Encryption,
│   │                  DataAuthorization, DtoGenerator (+ Roslyn analyzer)
│   ├── AspNet/        AspCore.Extensions, AspCore.Idempotency
│   │                  (+ MsSqlStore), AspCore.Tasks
│   ├── Services/      BlobStorage.{AzureStorage,AwsS3,Local},
│   │                  Encryption, PdfGenerators, Transformation
│   ├── SlimBus/       SlimBus.Extensions (CQRS/messaging glue)
│   ├── Aspire/        Aspire.Hosting.ServiceBus
│   ├── memory-bank/   AI-agent knowledge base (READ FIRST for non-trivial work)
│   ├── Directory.Build.props      Solution-wide MSBuild settings
│   ├── Directory.Packages.props   Central package version management
│   └── coverage.runsettings       Coverage collection settings
├── docs/              User-facing documentation (GitHub Pages)
├── specs/             Spec-Kit feature specifications
├── issues/            Pending issue notes (e.g. `1.md` for DtoGenerator work)
├── .github/           CI workflows + `copilot-instructions.md`
├── azure-pipelines.yml, ai-pr-review.azure-pipelines.yml
└── README.md, CONTRIBUTING.md, SECURITY.md, TESTING_STRATEGY.md
```

Each package project sits next to a sibling `*.Tests` project (e.g. `DKNet.EfCore.Specifications` ↔ `EfCore.Specifications.Tests`).

## Authoritative Context (Load Before Editing)

Treat these as primary sources — Claude should read the relevant ones before generating non-trivial code:

| File | Why it matters |
|---|---|
| `src/CLAUDE.md` | Repo-specific Claude guidance (commands, conventions, signature patterns). |
| `src/AGENTS.md` | Full coding/testing/PR conventions: commit format, naming, anti-patterns. |
| `src/memory-bank/README.md` | Index into the AI knowledge base. |
| `src/memory-bank/activeContext.md` | What is actively being worked on right now. |
| `src/memory-bank/copilot-rules.md` | 8000+ words of project-specific standards. |
| `src/memory-bank/copilot-quick-reference.md` | Code templates for common tasks. |
| `src/memory-bank/systemPatterns.md` | Architectural patterns and component relationships. |
| `src/memory-bank/libraries/README.md` | Scenario → DKNet package routing for API work. |
| `.github/copilot-instructions.md` | Mostly overlaps with AGENTS.md. |
| `docs/Architecture.md`, `docs/Testing-Strategy.md`, `docs/Contributing.md` | User-facing reference. |

After meaningful work: update `src/memory-bank/activeContext.md` and `src/memory-bank/progress.md`.

## Common Commands

Run from `src/` (where `DKNet.FW.sln` lives):

```bash
dotnet restore DKNet.FW.sln
dotnet build   DKNet.FW.sln -c Debug             # must produce zero warnings
dotnet test    DKNet.FW.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
dotnet test    EfCore.Specifications.Tests       # single project
dotnet test    --filter "FullyQualifiedName~DynamicAnd_WithMultipleConditions"
dotnet format                                    # before committing
./nuget.sh pack && ./verify_nuget_package.sh     # build + sanity-check NuGet packages
```

`Directory.Build.props` enables `TreatWarningsAsErrors=true`, `Nullable=enable`, `LangVersion=latest`, and `GenerateDocumentationFile=true` solution-wide. Any new warning, missing XML doc, or nullable mismatch breaks the build.

Integration tests use **TestContainers.MsSql** — Docker is required. Do not switch them to EF Core InMemory.

## Architectural Big Picture

DKNet expresses DDD + Onion Architecture at the package boundaries:

- **Aggregate roots** (`AggregateRoot` in `DKNet.EfCore.Abstractions`) carry domain events. Rich entities mutate via methods (e.g. `Product.UpdatePrice`) that call `AddEvent(...)`. Events are dispatched by `DKNet.EfCore.Events` during `SaveChanges`.
- **Repositories** (`DKNet.EfCore.Repos`) abstract persistence and consume **Specifications** (`DKNet.EfCore.Specifications`) — composable query objects whose `Criteria`, `Includes`, and `OrderBy` compose with LinqKit (`.And()`, `.Or()`).
- **Dynamic Predicate Builder** is the signature feature of `DKNet.EfCore.Specifications`. Builds runtime EF Core predicates from `(propertyName, FilterOperation, value)` triples with type/enum-safe conversion. Required call shape:
  ```csharp
  var predicate = PredicateBuilder.New<Product>()
      .And(p => p.IsActive)
      .DynamicAnd(b => b.With("Price", FilterOperations.GreaterThan, 100m));
  var results = await _db.Products.AsExpandable().Where(predicate).ToListAsync();
  ```
  `.AsExpandable()` is mandatory — LinqKit cannot translate the predicate without it. `DynamicAnd`/`DynamicOr` already null-handle internally; do not reintroduce manual null checks.
- **CQRS via SlimBus** — handlers (`IRequestHandler<TCommand, TResult>`) receive commands, fetch via repos, mutate aggregates, and persist; domain events emit automatically from the aggregate.
- **Hooks + AuditLogs + Encryption + DataAuthorization** are EF Core SaveChanges interceptors layered on the same `DbContext`. Independent and opt-in via DI extensions on the consuming app.
- **Idempotency** (`DKNet.AspCore.Idempotency`) is endpoint middleware backed by a pluggable store (`MsSqlStore`); HTTP response-code caching, composite key validation, and exception handling around the cache are part of the *current active development* (see `activeContext.md`).

## Conventions That Trip Up Generated Code

- **Test naming**: `MethodName_Scenario_ExpectedBehavior` (e.g. `DynamicAnd_WithMultipleConditions_CombinesCorrectly`).
- **Test stack**: xUnit + Shouldly + TestContainers.MsSql; avoid mocking the DB. Use `IAsyncLifetime` fixtures, not shared `IClassFixture` state, when isolation matters.
- **File header**: every `.cs` file gets the copyright block (template lives in the memory-bank). Don't omit when creating new files.
- **XML docs** are mandatory on all public APIs (`<summary>`, `<param>`, `<returns>`, relevant `<exception>`); `GenerateDocumentationFile=true` makes warnings fatal.
- **Naming**: private fields `_camelCase`; async methods end in `Async`; extensions live in static classes under `/Extensions`.
- **EF Core**: always `await`, default to `AsNoTracking()` for reads, push filtering to the DB, prefer `Include`/projections over per-row fetches. For dynamic predicates remember `.AsExpandable()`.
- **Verifying SQL** in tests: use `query.ToQueryString()` and assert against the generated SQL alongside the materialized rows — recurring pattern in `EfCore.Specifications.Tests`.
- **Central package management**: add/upgrade NuGet versions in `src/Directory.Packages.props`, not in individual `.csproj` files.
- **Commit messages** follow Conventional Commits with scopes such as `specifications`, `repository`, `extensions`, `tests`, `docs`. Examples in `src/AGENTS.md`. PRs should call out coverage impact and breaking changes.

## Coverage Targets

| Area | Target |
|---|---|
| Core libraries | 99% line |
| EfCore libraries | 95% line |
| Service libraries | 90% line |
| CI gate (overall) | 80% |

## Workflow Notes Specific to This Repo

- `dev` is the integration branch and the default PR base. Recent history shows many small `up` / fix commits — squash where it makes sense.
- After a meaningful change, update `src/memory-bank/activeContext.md` (and `progress.md` for larger items).
- Diagrams are tracked: `Diagram.drawio` / `Diagram.png` at the repo root and `src/EfCore/Diagrams/`. If you change an architectural relationship, update the relevant diagram or call it out in the PR.
- Generated artefacts — `nupkgs/`, `TestResults/`, `coverage-report*/` — must never be committed.

## Quick Reference for Common Pitfalls

- ❌ Forgetting `.AsExpandable()` with LinqKit dynamic predicates → expression expansion fails.
- ❌ Materializing early (`ToList()` before `.Where(...)`) → wrong correctness + perf.
- ❌ Using EF Core InMemory for integration tests → masks SQL-specific bugs.
- ❌ `.Result` / `.Wait()` on async calls → deadlock risk.
- ❌ Adding NuGet package versions in individual csproj files → use `Directory.Packages.props`.
- ❌ Missing XML docs on a new public API → CI fails (warnings-as-errors).
