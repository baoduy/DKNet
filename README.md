# DKNet Framework

[![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![CodeQL Advanced](https://github.com/baoduy/DKNet/actions/workflows/codeql.yml/badge.svg)](https://github.com/baoduy/DKNet/actions/workflows/codeql.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=baoduy_DKNet&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=baoduy_DKNet)

![codeCove](https://codecov.io/gh/baoduy/DKNet/graphs/sunburst.svg?token=xtNN7AtB1O)

## DKNet Project Overview

Here’s a summary of all DKNET-prefixed projects in this repository, with links to their source code and documentation:

| Project Name                        | Description                                             | Source Code                                            | Documentation                                      |
|-------------------------------------|---------------------------------------------------------|-------------------------------------------------------|----------------------------------------------------|
| DKNet.Fw.Extensions                 | Framework-level extensions and utilities                | [Core/DKNet.Fw.Extensions](Core/DKNet.Fw.Extensions)  | [docs/Core/DKNet.Fw.Extensions.md](docs/Core/DKNet.Fw.Extensions.md)     |
| DKNet.EfCore.Abstractions           | Core abstractions and interfaces for EF Core            | [EfCore/DKNet.EfCore.Abstractions](EfCore/DKNet.EfCore.Abstractions) | [docs/EfCore/DKNet.EfCore.Abstractions.md](docs/EfCore/DKNet.EfCore.Abstractions.md) |
| DKNet.EfCore.DataAuthorization      | Data authorization and access control                   | [EfCore/DKNet.EfCore.DataAuthorization](EfCore/DKNet.EfCore.DataAuthorization) | [docs/EfCore/DKNet.EfCore.DataAuthorization.md](docs/EfCore/DKNet.EfCore.DataAuthorization.md) |
| DKNet.EfCore.Events                 | Domain event handling and dispatching                   | [EfCore/DKNet.EfCore.Events](EfCore/DKNet.EfCore.Events) | [docs/EfCore/DKNet.EfCore.Events.md](docs/EfCore/DKNet.EfCore.Events.md) |
| DKNet.EfCore.Extensions             | EF Core functionality enhancements                      | [EfCore/DKNet.EfCore.Extensions](EfCore/DKNet.EfCore.Extensions) | [docs/EfCore/DKNet.EfCore.Extensions.md](docs/EfCore/DKNet.EfCore.Extensions.md) |
| DKNet.EfCore.Hooks                  | Lifecycle hooks for EF Core operations                  | [EfCore/DKNet.EfCore.Hooks](EfCore/DKNet.EfCore.Hooks) | [docs/EfCore/DKNet.EfCore.Hooks.md](docs/EfCore/DKNet.EfCore.Hooks.md) |
| DKNet.EfCore.Relational.Helpers     | Relational database utilities                           | [EfCore/DKNet.EfCore.Relational.Helpers](EfCore/DKNet.EfCore.Relational.Helpers) | [docs/EfCore/DKNet.EfCore.Relational.Helpers.md](docs/EfCore/DKNet.EfCore.Relational.Helpers.md) |
| DKNet.EfCore.Repos                  | Repository pattern implementations                      | [EfCore/DKNet.EfCore.Repos](EfCore/DKNet.EfCore.Repos) | [docs/EfCore/DKNet.EfCore.Repos.md](docs/EfCore/DKNet.EfCore.Repos.md) |
| DKNet.EfCore.Repos.Abstractions     | Repository abstractions                                 | [EfCore/DKNet.EfCore.Repos.Abstractions](EfCore/DKNet.EfCore.Repos.Abstractions) | [docs/EfCore/DKNet.EfCore.Repos.Abstractions.md](docs/EfCore/DKNet.EfCore.Repos.Abstractions.md) |
| DKNet.SlimBus.Extensions            | SlimMessageBus extensions for EF Core                   | [SlimBus/DKNet.SlimBus.Extensions](SlimBus/DKNet.SlimBus.Extensions) | [docs/SlimBus/DKNet.SlimBus.Extensions.md](docs/SlimBus/DKNet.SlimBus.Extensions.md) |
| DKNet.Svc.BlobStorage.Abstractions  | File storage service abstractions                       | [Services/DKNet.Svc.BlobStorage.Abstractions](Services/DKNet.Svc.BlobStorage.Abstractions) | [docs/Services/DKNet.Svc.BlobStorage.Abstractions.md](docs/Services/DKNet.Svc.BlobStorage.Abstractions.md) |
| DKNet.Svc.BlobStorage.AwsS3         | AWS S3 storage adapter                                 | [Services/DKNet.Svc.BlobStorage.AwsS3](Services/DKNet.Svc.BlobStorage.AwsS3) | [docs/Services/DKNet.Svc.BlobStorage.AwsS3.md](docs/Services/DKNet.Svc.BlobStorage.AwsS3.md) |
| DKNet.Svc.BlobStorage.AzureStorage  | Azure Blob storage adapter                              | [Services/DKNet.Svc.BlobStorage.AzureStorage](Services/DKNet.Svc.BlobStorage.AzureStorage) | [docs/Services/DKNet.Svc.BlobStorage.AzureStorage.md](docs/Services/DKNet.Svc.BlobStorage.AzureStorage.md) |
| DKNet.Svc.BlobStorage.Local         | Local file system storage                               | [Services/DKNet.Svc.BlobStorage.Local](Services/DKNet.Svc.BlobStorage.Local) | [docs/Services/DKNet.Svc.BlobStorage.Local.md](docs/Services/DKNet.Svc.BlobStorage.Local.md) |
| DKNet.Svc.Transformation            | Data transformation services                            | [Services/DKNet.Svc.Transformation](Services/DKNet.Svc.Transformation) | [docs/Services/DKNet.Svc.Transformation.md](docs/Services/DKNet.Svc.Transformation.md) |
| Aspire.Hosting.ServiceBus           | .NET Aspire Service Bus hosting extensions              | [Aspire/Aspire.Hosting.ServiceBus](Aspire/Aspire.Hosting.ServiceBus) | [docs/Aspire/Aspire.Hosting.ServiceBus.md](docs/Aspire/Aspire.Hosting.ServiceBus.md) |
| SlimBus.ApiEndpoints (Template)     | Complete API template using SlimMessageBus              | [z_Templates/SlimBus.ApiEndpoints](z_Templates/SlimBus.ApiEndpoints) | [docs/z_Templates/SlimBus.ApiEndpoints.md](docs/z_Templates/SlimBus.ApiEndpoints.md) |

> For a complete and up-to-date list, see the [docs folder](docs/README.md) or browse the source tree.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

## 🙏 Acknowledgments

- **SlimMessageBus Team**: For providing an excellent messaging framework
- **Entity Framework Team**: For the robust ORM foundation
- **Domain-Driven Design Community**: For architectural guidance and patterns
- **Contributors**: All developers who have contributed to this framework
