# DKNet Framework

[![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

DKNet Framework is a suite of 28 independent .NET 10 NuGet packages for building enterprise applications around
**Domain-Driven Design (DDD)** and **Onion Architecture** — EF Core building blocks (specifications, domain events,
hooks, audit logs, data authorization, encryption, DTO generation), ASP.NET Core utilities (idempotency, background
tasks, minimal-API glue), CQRS/messaging via SlimMessageBus, blob storage adapters, PDF generation, and .NET Aspire
integrations. Each package is opt-in: there is no `AddDKNet()` and no package that only aggregates others, so you
pull in only what your `DbContext` or application needs.

## Getting started

```bash
dotnet add package DKNet.EfCore.Abstractions
dotnet add package DKNet.EfCore.Specifications
```

See the [Getting Started Guide](docs/Getting-Started.md) for a walkthrough, or the
[SlimBus.ApiEndpoints template](https://github.com/baoduy/DKNet.Templates) for a complete reference implementation.

Not sure which packages you need? The
[**Which package do I need?**](docs/README.md#which-package-do-i-need) table maps problems to packages.

## Documentation

Full documentation — architecture, every package's API reference, configuration, migration, and FAQ — lives under
[`docs/`](docs/README.md):

- **[Documentation Index](docs/README.md)** — all 28 published packages, grouped by area
- **[Architecture Guide](docs/Architecture.md)** — the onion rings, the package dependency graph, and a request and a domain event traced end to end
- **[Getting Started](docs/Getting-Started.md)** · **[Configuration](docs/Configuration.md)** · **[Examples](docs/Examples/README.md)** · **[FAQ](docs/FAQ.md)**
- **[Migration Guide](docs/Migration-Guide.md)** · **[Testing Strategy](docs/Testing-Strategy.md)** · **[Security](docs/Security.md)** · **[Changelog](docs/CHANGELOG.md)**

![The DKNet onion: presentation packages on top, the application ring below, the EF Core infrastructure ring in the middle, and DKNet.EfCore.Abstractions plus the dependency-free foundation packages at the centre. Every arrow is a project reference pointing inward.](docs/diagrams/dknet-layers.svg)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, coding standards, and the pull request process, and
[SECURITY.md](SECURITY.md) for reporting a vulnerability.

## License

[MIT](LICENSE)
