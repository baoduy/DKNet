# Repository Guidelines

## Project Structure & Module Organization
- `DKNet.FW.sln` aggregates the libraries under `Core`, `Services`, `EfCore`, `SlimBus`, and the ASP.NET host in `DKNet.AspCore.Tasks`. Keep shared abstractions in `Core` and feature-specific code near its consumer to limit cross-module coupling.
- Background services and Aspire configurations live in `Aspire`, while reusable templates and NuGet packaging assets sit in `Templates`, `memory-bank`, and `NugetLogo.png`.
- Tests are rooted in `AspCore.Tasks.Tests`; generated artefacts such as `TestResults/` and `nupkgs/` should stay out of source control.

## Build, Test, and Development Commands
```bash
dotnet restore DKNet.FW.sln         # Restore solution dependencies
dotnet build DKNet.FW.sln -c Debug  # Validate compilation before committing
dotnet test DKNet.FW.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
dotnet test AspCore.Tasks.Tests     # Targeted run while iterating
./nuget.sh pack && ./verify_nuget_package.sh  # Produce + sanity-check packages
```

## Coding Style & Naming Conventions
- Follow the solution-wide StyleCop rules (`stylecop.json`) and `.editorconfig` exemptions; private fields may start with `_` and `using` directives stay outside namespaces.
- Use four-space indentation, PascalCase for public members, camelCase for locals, and suffix asynchronous methods with `Async`.
- Run `dotnet format` before opening a PR to enforce analyzer-driven layout and ordering.

## Testing Guidelines
- Tests use xUnit with Shouldly assertions; name them `MethodName_Scenario_Outcome` to signal intent.
- Keep unit tests deterministic and isolated; disable parallelism only when a fixture truly requires shared state.
- Failing tests must capture expected logging/messages; prefer `Should.ThrowAsync<T>` and structured assertions over raw exceptions.
- Aim for the 90%+ coverage seen in `coverage.runsettings`; include coverage diffs in PR discussions when regression is possible.

## Commit & Pull Request Guidelines
- Craft commits in the concise, imperative style visible in the history (e.g., `add ModelSpecification`); group related changes and avoid mixing refactors with behaviour shifts.
- PRs need a problem statement, summary of changes, validation notes (command output, screenshots for UI), and linked issues or TODOs.
- Flag breaking changes and configuration updates in the PR body; request review from owners of affected modules (`Core`, `Services`, etc.) to keep domain expertise in the loop.
