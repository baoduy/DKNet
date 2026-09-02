# DKNet Framework — AI Agent Guidelines

Repo-wide context, architecture and pitfalls live in the repo-root **`CLAUDE.md`** (symlinked here as `src/CLAUDE.md`). That file is authoritative for the folder-and-namespace convention, the package map, and the common-pitfall list. This file adds only the conventions specific to writing DKNet code.

> **`src/memory-bank/` no longer exists.** It was removed; `docs/` is the reference knowledge base. Read `docs/<Area>/<Package>.md` for any package you touch — e.g. `docs/EfCore/DKNet.EfCore.Specifications.md`, `docs/AspNetCore/DKNet.AspCore.Idempotency.md`. Ignore lingering `memory-bank` pointers in older files.

## Before You Start

1. **`docs/<Area>/README.md`** — index of packages in the area you're touching.
2. **`docs/<Area>/<Package>.md`** — the package's API, DI setup, options and examples.
3. **`.claude/skills/`** — `dknet-packages` (scenario → package routing), `dknet-codegen` (`[CrudAction]` / `[GenerateDto]`), `dknet-testing` (TestContainers, SQL assertions).
4. **`docs/Architecture.md`**, **`docs/Testing-Strategy.md`** — cross-cutting reference.

## Layout

Projects live under `Core/`, `EfCore/`, `AspNet/`, `Services/`, `SlimBus/`, `Aspire/`. Every package project has a sibling test project in the same area folder (`EfCore/DKNet.EfCore.Specifications` ↔ `EfCore/EfCore.Specifications.Tests`). Keep shared abstractions in `Core` and feature-specific code next to its consumer.

## Build, Test & Development Commands

Run from `src/`:

```bash
dotnet restore DKNet.FW.sln
dotnet build   DKNet.FW.sln -c Debug     # must produce zero warnings
dotnet test    DKNet.FW.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
dotnet test    EfCore/EfCore.Specifications.Tests            # single project
dotnet test    --filter "FullyQualifiedName~DynamicAnd_WithMultipleConditions"
dotnet format                             # before opening a PR
./verify_nuget_package.sh                 # pack to ./nupkgs (Release), then verify
```

`Directory.Build.props` sets `TreatWarningsAsErrors=true`, `Nullable=enable`, `LangVersion=latest`, `GenerateDocumentationFile=true` solution-wide. Any new warning, missing XML doc, or nullable mismatch breaks the build.

## Naming & Style

- StyleCop (`stylecop.json`) + `.editorconfig` govern layout; `dotnet format` enforces it.
- Private fields `_camelCase`; parameters/locals `camelCase`; types and members `PascalCase`; interfaces `I`-prefixed.
- Async methods end in `Async`. Extension methods go in static classes under `/Extensions`.
- **File header**: every `.cs` file opens with the `copyrightText` block from `stylecop.json`, then `// Author:`, `// File:`, `// Description:`. Copy the header from a neighbouring file rather than inventing one.
- **XML docs** are mandatory on every public API (`<summary>`, `<param>`, `<returns>`, and `<exception>` where it throws).
- **Folder-per-concern**: a type's folder name is the last segment of its namespace. Ambient-namespace types are exempt — see `CLAUDE.md` for the full rule and the exception list.

## DKNet Patterns

**Dynamic predicates** (`DKNet.EfCore.Specifications`) — `.AsExpandable()` is mandatory; `DynamicAnd`/`DynamicOr` null-handle internally, so do not add manual null checks:

```csharp
var predicate = PredicateBuilder.New<Product>()
    .And(p => p.IsActive)
    .DynamicAnd(b => b
        .With("Price", FilterOperations.GreaterThan, 100m)
        .With("CategoryId", FilterOperations.Equal, categoryId));

var results = await _db.Products.AsNoTracking().AsExpandable()
    .Where(predicate).ToListAsync();
```

**Specifications** inherit `Specification<TEntity>` and expose `Criteria`, `Includes`, `OrderBy`; the spec repository (`AddSpecRepo<TDbContext>()`) consumes them rather than raw LINQ. `DKNet.EfCore.Repos` / `Repos.Abstractions` are **retired** — do not build new code on them (`docs/EfCore/Migrating-Repos-To-Specifications.md`).

**Aggregates** derive from `AggregateRoot` (`DKNet.EfCore.Abstractions`), mutate through methods that call `AddEvent(...)`, and let `DKNet.EfCore.Events` dispatch during `SaveChanges`.

**Generated code**: `[CrudAction]` and `[GenerateDto]` drive Roslyn generators. Never hand-write a type the generator emits — it produces duplicate-type build errors. Use the `dknet-codegen` skill.

## Testing

- **Stack**: xUnit + Shouldly + TestContainers (real SQL Server) + Bogus for data. Mock as little as possible; never mock the `DbContext`.
- **Naming**: `MethodName_Scenario_ExpectedBehavior` — e.g. `DynamicAnd_WithMultipleConditions_CombinesCorrectly`.
- **Never** use EF Core InMemory for integration tests; it masks SQL-specific bugs.
- **Isolation**: prefer `IAsyncLifetime` over shared `IClassFixture` state when tests can interfere.
- **SQL verification**: assert on `query.ToQueryString()` alongside the materialized rows — the recurring pattern in `EfCore.Specifications.Tests`.
- **ARM64**: `mssql/server` has no ARM image. Fixtures switch on `RuntimeInformation.ProcessArchitecture` and fall back to `azure-sql-edge`. Keep that switch when editing a fixture.
- Full detail and the remote x64 fallback: `dknet-testing` skill and `CLAUDE.md`.

### Coverage Targets

| Area | Target |
|---|---|
| Core libraries | 99% line |
| EfCore libraries | 95% line |
| Service libraries | 90% line |
| CI gate (overall) | 80% |

## Commits & Pull Requests

Conventional Commits: `<type>(<scope>): <subject>`.

- **Types**: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `perf`
- **Scopes**: the package area — `specifications`, `repository`, `extensions`, `efcore`, `aspcore`, `idempotency`, `dataauthorization`, `encryption`, `slimbus-generators`, `tests`, `docs`
- Reference the tracking ID in the subject or body (`[DRK-123]`) — matches existing history.

```
feat(specifications): add enum validation to dynamic predicates [DRK-123]

- Validate enum values before applying filters
- Skip invalid enum values with graceful fallback
- Add 15 unit tests for TryConvertToEnum

BREAKING CHANGE: invalid enum filters now no-op instead of throwing
```

### Pre-PR Checklist

- [ ] `dotnet build DKNet.FW.sln -c Debug` — zero warnings
- [ ] `dotnet test DKNet.FW.sln` — all green
- [ ] `dotnet format` clean
- [ ] XML docs on every new public API
- [ ] `docs/<Area>/<Package>.md` updated if the public surface changed
- [ ] Diagram updated (`Diagram.drawio`, `src/EfCore/Diagrams/`) if an architectural relationship changed
- [ ] Coverage held or improved; breaking changes called out in the PR body
- [ ] No generated artefacts committed (`nupkgs/`, `TestResults/`, `coverage-report*/`)

PRs state the problem, the change, the validation (test results, coverage, SQL strings where relevant), and any breaking change. Base branch is `dev`.
