# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository. The same applies to any AI coding assistant — for finer-grained DKNet-specific rules, see `src/CLAUDE.md`

## Repository at a Glance

**DKNet Framework** — a .NET 10 library suite of NuGet packages for building enterprise applications around **Domain-Driven Design (DDD)** and **Onion Architecture**. Published packages include EF Core extensions, ASP.NET Core utilities (Idempotency, Tasks), CQRS/messaging (SlimBus), blob storage adapters, encryption, PDF generation, and Aspire integrations.

- **Solution file**: `src/DKNet.FW.sln`
- **SDK**: pinned by `src/global.json` to `10.0.100` (`rollForward: latestMinor`).
- **Default branch**: `dev` (integration). `main` is release. Push feature work to a topic branch.
- **CI**: `.github/workflows/build-test-coverage.yml` runs build → test → coverage → SonarCloud on every PR to `main`/`dev`. Coverage gate: 80% (per-area targets below are stricter).

## Top-Level Layout

```
/                      Repo root — docs, license, pipelines
├── src/               All code. Run dotnet commands from here.
│   ├── DKNet.FW.sln   Solution aggregating ~55 projects
│   ├── Core/          Fw.Extensions, RandomCreator
│   ├── EfCore/        Largest area: Abstractions, Extensions, Specifications,
│   │                  Events, Hooks, AuditLogs, Encryption, DataAuthorization,
│   │                  Relational.Helpers, DtoGenerator (Roslyn generator)
│   ├── AspNet/        AspCore.Extensions, AspCore.Tasks, AspCore.Idempotency
│   │                  + Relational, MsSqlStore, NpgsqlStore, RedisStore
│   ├── Services/      Svc.BlobStorage.{Abstractions,AzureStorage,AwsS3,Local},
│   │                  Svc.Encryption, Svc.PdfGenerators, Svc.Transformation
│   ├── SlimBus/       SlimBus.Extensions (CQRS/messaging glue),
│   │                  SlimBus.Generators (Roslyn generator for [CrudAction])
│   ├── Aspire/        Aspire.Hosting.ServiceBus
│   ├── Directory.Build.props      Solution-wide MSBuild settings
│   ├── Directory.Packages.props   Central package version management
│   ├── stylecop.json              StyleCop rules + file-header copyright text
│   └── coverage.runsettings       Coverage collection settings
├── docs/              GitHub Pages site AND the reference knowledge base:
│                      one page per package under Core/, EfCore/, AspNetCore/,
│                      Services/, Messaging/, Aspire/
├── .claude/           settings.json + skills/ (DKNet agent skills)
├── specs/             Spec-Kit feature specifications (historical)
├── issues/            Pending issue notes
├── .github/           CI workflows + `copilot-instructions.md`
├── azure-pipelines.yml, ai-pr-review.azure-pipelines.yml
└── README.md, CONTRIBUTING.md, SECURITY.md
```

Each package project sits next to a sibling `*.Tests` project (e.g. `DKNet.EfCore.Specifications` ↔ `EfCore.Specifications.Tests`).

## Authoritative Context (Load Before Editing)

`src/memory-bank/` was **removed** — `docs/` is now the reference knowledge base. Ignore any lingering `memory-bank` pointer in older files.

Treat these as primary sources — read the relevant ones before generating non-trivial code:

| File | Why it matters |
|---|---|
| `src/AGENTS.md` | DKNet-only coding/testing/PR conventions (commit format, test patterns). |
| `docs/<Area>/<Package>.md` | **Per-package reference — the single best source for any package's API, DI setup and options.** e.g. `docs/EfCore/DKNet.EfCore.Specifications.md`, `docs/Messaging/DKNet.SlimBus.Generators.md`. |
| `docs/<Area>/README.md` | Index of the packages in that area. |
| `docs/Architecture.md`, `docs/Testing-Strategy.md`, `docs/Contributing.md` | Cross-cutting reference. |
| `.github/copilot-instructions.md` | Condensed Copilot-facing version of AGENTS.md; kept in sync with it. |

When the user asks for a feature in a specific area, read that package's `docs/` page before generating code rather than guessing the API.

## Common Commands

Run from `src/` (where `DKNet.FW.sln` lives):

```bash
dotnet restore DKNet.FW.sln
dotnet build   DKNet.FW.sln -c Debug             # must produce zero warnings
dotnet test    DKNet.FW.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
dotnet test    EfCore.Specifications.Tests       # single project
dotnet test    --filter "FullyQualifiedName~DynamicAnd_WithMultipleConditions"
dotnet format                                    # before committing
./verify_nuget_package.sh                        # pack solution to ./nupkgs (Release), then verify at nuget.info
```

`Directory.Build.props` enables `TreatWarningsAsErrors=true`, `Nullable=enable`, `LangVersion=latest`, and `GenerateDocumentationFile=true` solution-wide. Any new warning, missing XML doc, or nullable mismatch breaks the build.

Integration tests use **TestContainers.MsSql** — Docker is required. Do not switch them to EF Core InMemory. `mssql/server` ships x64-only images with no ARM64 build.

**SQL Server only runs on x64 and Apple Silicon. On every other ARM device, skip the MsSql-backed tests locally.** `mssql/server` ships x64-only images, so `AspCore.Idempotency.MsSqlStore.Tests`' fixture (`Fixtures/ApiFixture.cs`) picks its image off `RuntimeInformation.ProcessArchitecture` and falls back to `azure-sql-edge` on ARM64. That fallback works on Apple Silicon (Rosetta) but **not** on other ARM64 hosts — Linux/ARM boxes included, where the container fails to launch outright. Keep the arch switch in place when touching that fixture.

On a non-Apple ARM machine, exclude the MsSql tests from local runs and validate them through GitHub Actions instead:

```bash
dotnet test DKNet.FW.sln --filter "FullyQualifiedName!~MsSqlStore"     # whole solution, minus MsSql
dotnet test AspNet/AspCore.Idempotency.NpgsqlStore.Tests               # Postgres and Redis
dotnet test AspNet/AspCore.Idempotency.RedisStore.Tests                # both run fine on ARM64
```

Postgres and Redis containers run natively on ARM64, and `NpgsqlStore` exercises the shared `DKNet.AspCore.Idempotency.Relational` base — so a change to the shared reservation path is still covered locally. Only MsSql-specific SQL goes unverified. **Never delete, `[Skip]`, or otherwise disable the MsSql test project to make a local run go green** — it is the store's only coverage and it passes on CI. Exclude it at the command line, say so in the PR, and re-validate on the x64 runner below.

### Remote test verification (fallback)

Run tests locally first. On x64 and Apple Silicon that covers the whole solution; on other ARM hosts it covers everything except the MsSql-backed tests excluded above, which **must** be re-validated here before merging a change to the idempotency stores. When something genuinely can't run on this machine (Docker down, an image that won't pull, a restricted sandbox), or you want a true x64 second opinion, dispatch the `workflow_dispatch` workflow `.github/workflows/remote-tests.yml` on a GitHub-hosted x64 runner. It gives a clean pass/fail on tests only (no coverage/Sonar gate) and uploads a `test-results` artifact (`*.trx` + `build.log` + `test.log`) plus a failed-test step summary for AI debugging. Note it runs against the *pushed* branch, so commit first.

```bash
gh workflow run remote-tests.yml --ref <branch>                              # whole solution
gh workflow run remote-tests.yml --ref <branch> -f project=EfCore/EfCore.Extensions.Tests
gh workflow run remote-tests.yml --ref <branch> -f filter="FullyQualifiedName~DynamicAnd"
gh run watch <run-id> --exit-status        # or: gh run list --workflow remote-tests.yml
gh run view <run-id> --log-failed          # inline failed-step logs
gh run download <run-id> -n test-results   # pull trx + logs locally to fix code
```

The workflow must exist on the branch you dispatch (`--ref`); it lives on the default branch `dev`, so feature branches need it merged/rebased in. Do **not** rely on `.github/workflows/build-test-coverage.yml` for a pass/fail signal — its test step is `continue-on-error`, so it goes green even when tests fail.

`Svc.PdfGenerators.Tests` is the other case worth naming: if its Chromium download ever fails on a restricted ARM sandbox, the remote runner is the fallback:

```bash
gh workflow run remote-tests.yml --ref <branch> -f project=Services/Svc.PdfGenerators.Tests
```

## Architectural Big Picture

DKNet expresses DDD + Onion Architecture at the package boundaries:

- **Aggregate roots** (`AggregateRoot` in `DKNet.EfCore.Abstractions`) carry domain events. Rich entities mutate via methods (e.g. `Product.UpdatePrice`) that call `AddEvent(...)`. Events are dispatched by `DKNet.EfCore.Events` during `SaveChanges`.
- **Specifications** (`DKNet.EfCore.Specifications`) are the persistence entry point — composable query objects whose `Criteria`, `Includes` and `OrderBy` compose with LinqKit (`.And()`, `.Or()`), served by the spec repository registered via `AddSpecRepo<TDbContext>()`. **`DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions` have been removed** — the packages no longer exist; see `docs/EfCore/Migrating-Repos-To-Specifications.md`.
- **Dynamic Predicate Builder** is the signature feature of `DKNet.EfCore.Specifications`. Builds runtime EF Core predicates from `(propertyName, Ops, value)` triples with type/enum-safe conversion. Required call shape:
  ```csharp
  var predicate = PredicateBuilder.New<Product>()
      .And(p => p.IsActive)
      .DynamicAnd("Price", Ops.GreaterThan, 100m);
  var results = await _db.Products.AsExpandable().Where(predicate).ToListAsync();
  ```
  `.AsExpandable()` is mandatory — LinqKit cannot translate the predicate without it. `DynamicAnd`/`DynamicOr` already null-handle internally; do not reintroduce manual null checks.
- **CQRS via SlimBus** — handlers (`IRequestHandler<TCommand, TResult>`) receive commands, fetch via repos, mutate aggregates, and persist; domain events emit automatically from the aggregate.
- **Source generators** are a first-class surface. `DKNet.SlimBus.Generators` reads `[CrudAction]` on aggregate methods and emits the request + handler + minimal-API endpoint vertical slice; `DKNet.EfCore.DtoGenerator` reads `[GenerateDto]` and emits DTOs. CRUD attributes live in `DKNet.EfCore.Abstractions.Attributes`; `[GenerateDto]` lives in `DKNet.EfCore.DtoGenerator`. **Never hand-write what a generator emits** — see the `dknet-codegen` skill and `docs/Messaging/DKNet.SlimBus.Generators.md`.
- **Hooks + AuditLogs + Encryption + DataAuthorization** are EF Core SaveChanges interceptors layered on the same `DbContext`. Independent and opt-in via DI extensions on the consuming app. `DataAuthorization` **fails closed**: a `DbContext` that does not implement `IDataOwnerDbContext` throws rather than silently skipping the filter.
- **Idempotency** (`DKNet.AspCore.Idempotency`) is endpoint middleware over a pluggable store. Four store packages ship: `Relational` (shared base), `MsSqlStore`, `NpgsqlStore`, `RedisStore`. Idempotency keys are attacker-controlled — they are sanitized before logging and before appearing in 409 bodies; keep that sanitization when touching the cache path.

## Conventions That Trip Up Generated Code

- **Test naming**: `MethodName_Scenario_ExpectedBehavior` (e.g. `DynamicAnd_WithMultipleConditions_CombinesCorrectly`).
- **Test stack**: xUnit + Shouldly + TestContainers.MsSql; avoid mocking the DB. Use `IAsyncLifetime` fixtures, not shared `IClassFixture` state, when isolation matters.
- **File header**: every `.cs` file opens with the copyright block — the canonical `copyrightText` is in `src/stylecop.json`, followed by `// Author:`, `// File:`, `// Description:` lines. Copy the header from a neighbouring file in the same project rather than inventing one.
- **XML docs** are mandatory on all public APIs (`<summary>`, `<param>`, `<returns>`, relevant `<exception>`); `GenerateDocumentationFile=true` makes warnings fatal.
- **Naming**: private fields `_camelCase`; async methods end in `Async`; extensions live in static classes under `/Extensions`.
- **Folder-per-concern**: a type sits in a folder named for the single concern it serves, and **the folder name is the last segment of the type's namespace** (e.g. `DKNet.EfCore.Specifications.Repositories` lives in `Repositories/`). A package's project root holds only its entry surface — the contract and/or DI registration point a consumer touches directly. Exception: a type that deliberately declares an **ambient namespace** (a namespace owned by the framework or library it extends, so its extension methods resolve without an extra import) is exempt and keeps that namespace whether or not it is grouped into a folder — e.g. `DKNet.Fw.Extensions.ServiceCollectionExtensions` (`Microsoft.Extensions.DependencyInjection`) and `DKNet.EfCore.Specifications.Dynamics.DynamicPredicateExtensions` (`LinqKit`).
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
- Diagrams are tracked: `Diagram.drawio` / `Diagram.png` at the repo root and `src/EfCore/Diagrams/`. If you change an architectural relationship, update the relevant diagram or call it out in the PR.
- Generated artefacts — `nupkgs/`, `TestResults/`, `coverage-report*/` — must never be committed.

## Quick Reference for Common Pitfalls

- ❌ Forgetting `.AsExpandable()` with LinqKit dynamic predicates → expression expansion fails.
- ❌ Materializing early (`ToList()` before `.Where(...)`) → wrong correctness + perf.
- ❌ Using EF Core InMemory for integration tests → masks SQL-specific bugs.
- ❌ `.Result` / `.Wait()` on async calls → deadlock risk.
- ❌ Adding NuGet package versions in individual csproj files → use `Directory.Packages.props`.
- ❌ Missing XML docs on a new public API → CI fails (warnings-as-errors).
- ❌ Hand-writing a request/handler/endpoint that `DKNet.SlimBus.Generators` already emits for `[CrudAction]` → duplicate-type build errors.
- ❌ Following a `src/memory-bank/...` pointer in an older file → that directory is gone; use `docs/`.

## Repo Skills

`.claude/skills/` holds DKNet-specific skills that load on demand — use them instead of re-deriving:

| Skill | Use when |
|---|---|
| `dknet-packages` | Choosing which DKNet package solves a scenario, and which `docs/` page to read. |
| `dknet-codegen` | Working with `[CrudAction]`, `[GenerateDto]`, or `[FromClaim]` and the generators behind them. |
| `dknet-testing` | Writing or debugging tests — TestContainers fixtures, ARM64 image fallback, SQL assertions. |
