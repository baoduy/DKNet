# DKNet Framework

[![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

DKNet Framework is a suite of independent .NET 10 NuGet packages for building enterprise applications around
**Domain-Driven Design (DDD)** and **Onion Architecture** — EF Core building blocks (specifications, domain events,
hooks, audit logs, data authorization, encryption, DTO generation), ASP.NET Core utilities (idempotency, background
tasks), CQRS/messaging via SlimMessageBus, blob storage adapters, PDF generation, and .NET Aspire integrations. Each
package is opt-in: pull in only what your `DbContext` or application needs.

## Getting started

```bash
dotnet add package DKNet.EfCore.Specifications
dotnet add package DKNet.EfCore.Abstractions
```

See the [Getting Started Guide](docs/Getting-Started.md) for a full walkthrough, or the
[SlimBus.ApiEndpoints template](https://github.com/baoduy/DKNet.Templates) for a complete reference implementation.

## Documentation

Full documentation — architecture, every package's API reference, configuration, migration, and FAQ — lives under
[`docs/`](docs/README.md):

- **[Documentation Index](docs/README.md)** — all 30 packages, grouped by area
- **[Architecture Guide](docs/Architecture.md)** — DDD and Onion Architecture as implemented in DKNet
- **[Getting Started](docs/Getting-Started.md)** · **[Configuration](docs/Configuration.md)** · **[FAQ](docs/FAQ.md)**
- **[Migration Guide](docs/Migration-Guide.md)** · **[Changelog](docs/CHANGELOG.md)**

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, coding standards, and the pull request process, and
[SECURITY.md](SECURITY.md) for reporting a vulnerability.

## License

[MIT](LICENSE)
