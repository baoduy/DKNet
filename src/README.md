# DKNet source tree

[![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../LICENSE)

This directory is the solution root for every DKNet package. If you are **using** DKNet, start from the
[documentation hub](../docs/README.md) instead — this page is for working on the code.

## Layout

| Path | Contents |
|---|---|
| `Core/` | `DKNet.Fw.Extensions`, `DKNet.RandomCreator` — no dependencies on the rest of the suite |
| `EfCore/` | the twelve `DKNet.EfCore.*` projects, including the two retired `Repos` ones |
| `SlimBus/` | `DKNet.SlimBus.Extensions` and the `DKNet.SlimBus.Generators` source generator |
| `AspNet/` | `DKNet.AspCore.Extensions`, `.Tasks`, and the idempotency family |
| `Services/` | the `DKNet.Svc.*` blob storage, encryption, PDF, and transformation packages |
| `Aspire/` | `Aspire.Hosting.ServiceBus` |
| `DKNet.FW.sln` | the solution containing all of the above plus every test project |

Every package has a sibling test project in the same area folder — `EfCore/EfCore.Specifications.Tests`,
`Services/Svc.Encryption.Tests`, and so on. They are the most current usage reference for any API.

## Build and test

```bash
cd src

dotnet restore DKNet.FW.sln
dotnet build   DKNet.FW.sln --configuration Release

# Tests with coverage collection, as CI runs them
dotnet test DKNet.FW.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

Integration tests use **TestContainers.MsSql**, so Docker must be running. `mcr.microsoft.com/mssql/server`
publishes no ARM64 image: on an ARM machine, run the MsSql-backed test projects on an x64 runner via the
`remote-tests.yml` workflow rather than substituting a different database engine. Every other test project runs
locally. See [Testing Strategy](../docs/Testing-Strategy.md).

## Solution-wide conventions

| File | What it fixes for every project |
|---|---|
| `Directory.Build.props` | `TreatWarningsAsErrors`, `Nullable=enable`, `LangVersion=latest`, `GenerateDocumentationFile`, and the Microsoft analyzers |
| `Directory.Packages.props` | central package version management — add or upgrade NuGet versions here, never in an individual `.csproj` |
| `global.json` | SDK `10.0.0` with `rollForward: latestMajor` |
| `coverage.runsettings` | coverage collection settings shared by local runs and CI |
| `stylecop.json` | StyleCop configuration, linked into every project |

Because `TreatWarningsAsErrors` and `GenerateDocumentationFile` are both on, a new warning, a missing XML doc
comment on a public member, or a nullable mismatch fails the build.

`verify_nuget_package.sh` packs the solution locally so a package's contents can be inspected before release.
Generated output — `nupkgs/`, `TestResults/`, `coverage-report*/` — is never committed.

## Documentation

| Page | What it covers |
|---|---|
| [Documentation hub](../docs/README.md) | all 28 published packages, and a problem-to-package table |
| [Architecture Guide](../docs/Architecture.md) | the onion rings, the package dependency graph, a request and a domain event end to end |
| [Getting Started](../docs/Getting-Started.md) | prerequisites and a first working setup |
| [Configuration & Setup](../docs/Configuration.md) | the registration conventions the packages share, and where each extension method lives |
| [Examples & Recipes](../docs/Examples/README.md) | runnable implementations |
| [API Reference](../docs/API-Reference.md) | per-package index |
| [FAQ](../docs/FAQ.md) | common questions and troubleshooting |
| [Testing Strategy](../docs/Testing-Strategy.md) | test stack and coverage targets |

Contributor notes for this tree live in [`AGENTS.md`](AGENTS.md); the repository-level guide is
[`CONTRIBUTING.md`](../CONTRIBUTING.md).

## License

[MIT](../LICENSE)
