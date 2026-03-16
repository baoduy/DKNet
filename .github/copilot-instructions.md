# Project Guidelines

## Context First
Before making code changes, load project context in this order:
1. `src/memory-bank/README.md`
2. `src/memory-bank/activeContext.md`
3. `src/memory-bank/copilot-quick-reference.md`
4. `src/memory-bank/systemPatterns.md`
5. `src/memory-bank/copilot-rules.md`

## Architecture
- DKNet is a `.NET 10` library suite for enterprise API development using **DDD** and **Onion Architecture**.
- Keep domain behavior in domain types (aggregate roots, entities, value objects); keep infrastructure concerns in EF Core, messaging, and service packages.
- Prefer Specification + Repository patterns for query composition and reuse.
- For dynamic predicates, compose with LinqKit and use `.AsExpandable()` before `.Where()`.
- Keep boundaries clear across `Core`, `EfCore`, `Services`, `SlimBus`, and `AspNet` modules.

## Build and Test
Run commands from `src/` unless noted.

```bash
dotnet restore DKNet.FW.sln
dotnet build DKNet.FW.sln --configuration Release
dotnet test DKNet.FW.sln --configuration Release --settings coverage.runsettings --collect "XPlat Code Coverage"
dotnet pack DKNet.FW.sln --configuration Release
```

## Code Style
- Treat warnings as errors (`TreatWarningsAsErrors=true`) and keep nullable annotations correct (`Nullable=enable`).
- Add XML documentation for all public APIs.
- Use async I/O end-to-end; avoid sync-over-async (`.Result`, `.Wait()`).
- Follow existing naming conventions and static extension class placement.
- Keep changes minimal and scoped; do not refactor unrelated areas.

## Conventions
- Test stack: xUnit + Shouldly.
- Integration tests should use Testcontainers/real provider behavior (avoid EF Core InMemory for integration scenarios).
- Test naming: `MethodName_Scenario_ExpectedBehavior`.
- For read-only EF queries, prefer `.AsNoTracking()`.
- Filter in database (compose `IQueryable`) before materialization.

## Pitfalls
- Forgetting `.AsExpandable()` with LinqKit dynamic predicates breaks expression expansion.
- Early materialization (`ToList()` before filtering) causes performance and correctness issues.
- Missing XML docs or nullable fixes will fail CI due to strict build settings.

## Key References
- Architecture and DDD: `docs/Architecture.md`
- Contribution workflow and commands: `docs/Contributing.md`
- Testing strategy: `TESTING_STRATEGY.md`, `src/coverage.runsettings`
- Build/analyzer settings: `src/Directory.Build.props`, `src/Directory.Packages.props`, `src/global.json`
- Detailed project guidance: `src/memory-bank/`
