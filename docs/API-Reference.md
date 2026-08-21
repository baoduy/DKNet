# API Reference

Index into the per-package API documentation. Each package below has its own canonical reference page —
`docs/<Area>/<Package>.md` — kept in sync with `src/` on every change; this page does not restate their contents; it
just points to them, sorted by area.

## 🔧 Core Framework

- [DKNet.Fw.Extensions](Core/DKNet.Fw.Extensions.md) - Type/property/enum/async-enumerable extensions, reflection helpers
- [DKNet.RandomCreator](Core/DKNet.RandomCreator.md) - Cryptographically secure random string/char generation

## 🗄️ Entity Framework Core

- [DKNet.EfCore.Abstractions](EfCore/DKNet.EfCore.Abstractions.md) - Entity base classes, domain-event contracts
- [DKNet.EfCore.Extensions](EfCore/DKNet.EfCore.Extensions.md) - Entity configuration discovery, query filters, data seeding
- [DKNet.EfCore.Specifications](EfCore/DKNet.EfCore.Specifications.md) - Specification pattern, `IRepositorySpec`, Dynamic Predicate Builder — the current, supported way to query and persist
- [DKNet.EfCore.Repos](EfCore/DKNet.EfCore.Repos.md) - **Retired**, superseded by Specifications
- [DKNet.EfCore.Repos.Abstractions](EfCore/DKNet.EfCore.Repos.Abstractions.md) - **Retired**, superseded by Specifications
- [DKNet.EfCore.Hooks](EfCore/DKNet.EfCore.Hooks.md) - Before/after-SaveChanges interceptor pipeline
- [DKNet.EfCore.Events](EfCore/DKNet.EfCore.Events.md) - Domain event dispatching
- [DKNet.EfCore.AuditLogs](EfCore/DKNet.EfCore.AuditLogs.md) - Audit trail of entity changes
- [DKNet.EfCore.DataAuthorization](EfCore/DKNet.EfCore.DataAuthorization.md) - Row-level, ownership-based data authorization
- [DKNet.EfCore.Encryption](EfCore/DKNet.EfCore.Encryption.md) - Transparent column-level encryption
- [DKNet.EfCore.DtoGenerator](EfCore/DKNet.EfCore.DtoGenerator.md) - Compile-time DTO generation
- [DKNet.EfCore.Relational.Helpers](EfCore/DKNet.EfCore.Relational.Helpers.md) - Provider-aware `DbContext` helpers

## 📨 Messaging & CQRS

- [DKNet.SlimBus.Extensions](Messaging/DKNet.SlimBus.Extensions.md) - `Fluents.Requests`/`Fluents.Queries`/`Fluents.EventsConsumers` command/query/event contracts, auto-save behavior, Result pattern

## 🗃️ Blob Storage Services

- [DKNet.Svc.BlobStorage.Abstractions](Services/DKNet.Svc.BlobStorage.Abstractions.md) - Provider-agnostic `IBlobService` contract
- [DKNet.Svc.BlobStorage.AwsS3](Services/DKNet.Svc.BlobStorage.AwsS3.md) - AWS S3 adapter
- [DKNet.Svc.BlobStorage.AzureStorage](Services/DKNet.Svc.BlobStorage.AzureStorage.md) - Azure Blob Storage adapter
- [DKNet.Svc.BlobStorage.Local](Services/DKNet.Svc.BlobStorage.Local.md) - Local filesystem adapter

## 🔐 Security & Encryption

- [DKNet.Svc.Encryption](Services/DKNet.Svc.Encryption.md) - AES-GCM, password-based, RSA, and HMAC/SHA helpers

## 📝 PDF Generation

- [DKNet.Svc.PdfGenerators](Services/DKNet.Svc.PdfGenerators.md) - Markdown/HTML/template to PDF generation toolkit

## 🌐 ASP.NET Core Utilities

- [DKNet.AspCore.Tasks](AspNetCore/DKNet.AspCore.Tasks.md) - Application start-up background job orchestration
- [DKNet.AspCore.Extensions](AspNetCore/DKNet.AspCore.Extensions.md) - Minimal-API glue, endpoint discovery, Result/ProblemDetails conversion
- [DKNet.AspCore.Idempotency](AspNetCore/DKNet.AspCore.Idempotency.md) - Idempotent minimal-API endpoints
- [DKNet.AspCore.Idempotency.Relational](AspNetCore/DKNet.AspCore.Idempotency.Relational.md) - Shared EF Core idempotency store building blocks
- [DKNet.AspCore.Idempotency.MsSqlStore](AspNetCore/DKNet.AspCore.Idempotency.MsSqlStore.md) - SQL Server idempotency store
- [DKNet.AspCore.Idempotency.NpgsqlStore](AspNetCore/DKNet.AspCore.Idempotency.NpgsqlStore.md) - PostgreSQL idempotency store
- [DKNet.AspCore.Idempotency.RedisStore](AspNetCore/DKNet.AspCore.Idempotency.RedisStore.md) - Redis idempotency store

## ☁️ Aspire Integrations

- [Aspire.Hosting.ServiceBus](Aspire/Aspire.Hosting.ServiceBus.md) - Azure Service Bus resource builder extensions for .NET Aspire AppHost projects

## ⚙️ Configuration

There is no `DKNetOptions` and no single aggregator to configure DKNet through — each package registers itself
independently via its own `IServiceCollection` extension method and exposes its own strongly-typed options where
configuration is needed. See [Configuration & Setup](Configuration.md) for the full picture.

## 📖 Usage Examples

- **[Examples & Recipes](Examples/README.md)** - Practical usage examples
- **[SlimBus.ApiEndpoints template](https://github.com/baoduy/DKNet.Templates)** - Complete reference implementation, in the DKNet.Templates repository
- **Unit Tests** - API usage in the sibling `*.Tests` projects next to each package under `src/`
