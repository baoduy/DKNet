# ASP.NET Core Utilities

Utilities that enhance ASP.NET Core hosting scenarios, complementing DKNet's services and messaging layers. These
packages live in the **application layer** and focus on orchestrating background work, start-up routines, and other
cross-cutting concerns for web applications.

## Packages

| Package | Description |
|---------|-------------|
| [`DKNet.AspCore.Tasks`](./DKNet.AspCore.Tasks.md) | Background job orchestration for application start-up |
| [`DKNet.AspCore.Extensions`](./DKNet.AspCore.Extensions.md) | Minimal-API glue: claim-based request population, endpoint discovery/mapping, paged responses, Result/ProblemDetails conversion |
| [`DKNet.AspCore.Idempotency`](./DKNet.AspCore.Idempotency.md) | Endpoint filter that makes minimal-API operations safe to retry, backed by a pluggable key store |
| [`DKNet.AspCore.Idempotency.Relational`](./DKNet.AspCore.Idempotency.Relational.md) | Shared EF Core building blocks for implementing a relational idempotency store (not for app authors) |
| [`DKNet.AspCore.Idempotency.MsSqlStore`](./DKNet.AspCore.Idempotency.MsSqlStore.md) | SQL Server-backed idempotency key store |
| [`DKNet.AspCore.Idempotency.NpgsqlStore`](./DKNet.AspCore.Idempotency.NpgsqlStore.md) | PostgreSQL-backed idempotency key store |
| [`DKNet.AspCore.Idempotency.RedisStore`](./DKNet.AspCore.Idempotency.RedisStore.md) | Redis-backed idempotency key store, no schema/migrations required |
