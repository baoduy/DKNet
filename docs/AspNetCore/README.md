# ASP.NET Core Utilities

Utilities that enhance ASP.NET Core hosting scenarios, complementing DKNet's services and messaging layers. These
packages live in the **application layer** and focus on orchestrating background work, start-up routines, and other
cross-cutting concerns for web applications.

## Packages

| Package | Description |
|---------|-------------|
| [`DKNet.AspCore.Tasks`](../DKNet.AspCore.Tasks/index.md) | Background job orchestration: every registered `IBackgroundTask` runs once, concurrently, per host start-up |
| [`DKNet.AspCore.Extensions`](../DKNet.AspCore.Extensions/index.md) | Minimal-API glue: claim-based request population, endpoint discovery/mapping, generic list/read/delete endpoints with filtering and paging, Result/ProblemDetails conversion |
| [`DKNet.AspCore.Idempotency`](../DKNet.AspCore.Idempotency/index.md) | Endpoint filter that makes minimal-API operations safe to retry, backed by a pluggable key store |
| [`DKNet.AspCore.Idempotency.Relational`](../DKNet.AspCore.Idempotency.Relational/index.md) | Shared EF Core building blocks behind the two SQL stores — entirely `internal`, so nothing here is registerable by an application |
| [`DKNet.AspCore.Idempotency.MsSqlStore`](../DKNet.AspCore.Idempotency.MsSqlStore/index.md) | SQL Server-backed idempotency key store |
| [`DKNet.AspCore.Idempotency.NpgsqlStore`](../DKNet.AspCore.Idempotency.NpgsqlStore/index.md) | PostgreSQL-backed idempotency key store |
| [`DKNet.AspCore.Idempotency.RedisStore`](../DKNet.AspCore.Idempotency.RedisStore/index.md) | Redis-backed idempotency key store, no schema/migrations required |
