# DKNet Framework

[![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![GitHub release](https://img.shields.io/github/release/baoduy/DKNet.svg)](https://github.com/baoduy/DKNet/releases)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![CodeQL Advanced](https://github.com/baoduy/DKNet/actions/workflows/codeql.yml/badge.svg)](https://github.com/baoduy/DKNet/actions/workflows/codeql.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=baoduy_DKNet&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=baoduy_DKNet)
[![GitHub issues](https://img.shields.io/github/issues/baoduy/DKNet)](https://github.com/baoduy/DKNet/issues)
[![GitHub pull requests](https://img.shields.io/github/issues-pr/baoduy/DKNet)](https://github.com/baoduy/DKNet/pulls)
[![StyleCop](https://img.shields.io/badge/code%20style-StyleCop-brightgreen.svg)](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)

![codeCove](https://codecov.io/gh/baoduy/DKNet/graphs/sunburst.svg?token=xtNN7AtB1O)

[![GitHub contributors](https://img.shields.io/github/contributors/baoduy/DKNet)](https://github.com/baoduy/DKNet/graphs/contributors)

## DKNet Project Overview

Here’s a summary of all DKNET-prefixed projects in this repository, with links to their source code and documentation:

| Project Name                       | Description                                  | Source Code                                                                                        | Documentation                                                                                              |
|------------------------------------|----------------------------------------------|----------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------|
| DKNet.Fw.Extensions                | Framework-level extensions and utilities     | [src/Core/DKNet.Fw.Extensions](src/Core/DKNet.Fw.Extensions)                                       | [docs/Core/DKNet.Fw.Extensions.md](docs/Core/DKNet.Fw.Extensions.md)                                       |
| DKNet.EfCore.Abstractions          | Core abstractions and interfaces for EF Core | [src/EfCore/DKNet.EfCore.Abstractions](src/EfCore/DKNet.EfCore.Abstractions)                       | [docs/EfCore/DKNet.EfCore.Abstractions.md](docs/EfCore/DKNet.EfCore.Abstractions.md)                       |
| DKNet.EfCore.DataAuthorization     | Data authorization and access control        | [src/EfCore/DKNet.EfCore.DataAuthorization](src/EfCore/DKNet.EfCore.DataAuthorization)             | [docs/EfCore/DKNet.EfCore.DataAuthorization.md](docs/EfCore/DKNet.EfCore.DataAuthorization.md)             |
| DKNet.EfCore.Events                | Domain event handling and dispatching        | [src/EfCore/DKNet.EfCore.Events](src/EfCore/DKNet.EfCore.Events)                                   | [docs/EfCore/DKNet.EfCore.Events.md](docs/EfCore/DKNet.EfCore.Events.md)                                   |
| DKNet.EfCore.Extensions            | EF Core functionality enhancements           | [src/EfCore/DKNet.EfCore.Extensions](src/EfCore/DKNet.EfCore.Extensions)                           | [docs/EfCore/DKNet.EfCore.Extensions.md](docs/EfCore/DKNet.EfCore.Extensions.md)                           |
| DKNet.EfCore.Hooks                 | Lifecycle hooks for EF Core operations       | [src/EfCore/DKNet.EfCore.Hooks](src/EfCore/DKNet.EfCore.Hooks)                                     | [docs/EfCore/DKNet.EfCore.Hooks.md](docs/EfCore/DKNet.EfCore.Hooks.md)                                     |
| DKNet.EfCore.Relational.Helpers    | Relational database utilities                | [src/EfCore/DKNet.EfCore.Relational.Helpers](src/EfCore/DKNet.EfCore.Relational.Helpers)           | [docs/EfCore/DKNet.EfCore.Relational.Helpers.md](docs/EfCore/DKNet.EfCore.Relational.Helpers.md)           |
| DKNet.EfCore.Repos                 | Repository pattern implementations           | [src/EfCore/DKNet.EfCore.Repos](src/EfCore/DKNet.EfCore.Repos)                                     | [docs/EfCore/DKNet.EfCore.Repos.md](docs/EfCore/DKNet.EfCore.Repos.md)                                     |
| DKNet.EfCore.Repos.Abstractions    | Repository abstractions                      | [src/EfCore/DKNet.EfCore.Repos.Abstractions](src/EfCore/DKNet.EfCore.Repos.Abstractions)           | [docs/EfCore/DKNet.EfCore.Repos.Abstractions.md](docs/EfCore/DKNet.EfCore.Repos.Abstractions.md)           |
| DKNet.SlimBus.Extensions           | SlimMessageBus extensions for EF Core        | [src/SlimBus/DKNet.SlimBus.Extensions](src/SlimBus/DKNet.SlimBus.Extensions)                       | [docs/Messaging/DKNet.SlimBus.Extensions.md](docs/Messaging/DKNet.SlimBus.Extensions.md)                   |
| DKNet.Svc.BlobStorage.Abstractions | File storage service abstractions            | [src/Services/DKNet.Svc.BlobStorage.Abstractions](src/Services/DKNet.Svc.BlobStorage.Abstractions) | [docs/Services/DKNet.Svc.BlobStorage.Abstractions.md](docs/Services/DKNet.Svc.BlobStorage.Abstractions.md) |
| DKNet.Svc.BlobStorage.AwsS3        | AWS S3 storage adapter                       | [src/Services/DKNet.Svc.BlobStorage.AwsS3](src/Services/DKNet.Svc.BlobStorage.AwsS3)               | [docs/Services/DKNet.Svc.BlobStorage.AwsS3.md](docs/Services/DKNet.Svc.BlobStorage.AwsS3.md)               |
| DKNet.Svc.BlobStorage.AzureStorage | Azure Blob storage adapter                   | [src/Services/DKNet.Svc.BlobStorage.AzureStorage](src/Services/DKNet.Svc.BlobStorage.AzureStorage) | [docs/Services/DKNet.Svc.BlobStorage.AzureStorage.md](docs/Services/DKNet.Svc.BlobStorage.AzureStorage.md) |
| DKNet.Svc.BlobStorage.Local        | Local file system storage                    | [src/Services/DKNet.Svc.BlobStorage.Local](src/Services/DKNet.Svc.BlobStorage.Local)               | [docs/Services/DKNet.Svc.BlobStorage.Local.md](docs/Services/DKNet.Svc.BlobStorage.Local.md)               |
| DKNet.Svc.Transformation           | Data transformation services                 | [src/Services/DKNet.Svc.Transformation](src/Services/DKNet.Svc.Transformation)                     | [docs/Services/DKNet.Svc.Transformation.md](docs/Services/DKNet.Svc.Transformation.md)                     |
| Aspire.Hosting.ServiceBus          | .NET Aspire Service Bus hosting extensions   | [src/Aspire/Aspire.Hosting.ServiceBus](src/Aspire/Aspire.Hosting.ServiceBus)                       | - (No dedicated docs yet)                                                                                  |
| SlimBus.ApiEndpoints (Template)    | Complete API template using SlimMessageBus   | [src/Templates/SlimBus.ApiEndpoints](src/Templates/SlimBus.ApiEndpoints)                           | [src/Templates/SlimBus.ApiEndpoints/README.md](src/Templates/SlimBus.ApiEndpoints/README.md)               |

---

### Documentation

📖 **[Complete Framework Documentation](docs/README.md)** - Comprehensive documentation organized by functional areas

For detailed information about architecture, implementation patterns, and usage examples, visit
our [complete documentation](docs/README.md) or refer to our [GitHub Pages](https://baoduy.github.io/DKNet/)

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).