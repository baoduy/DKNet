# DKNet.AspCore.Idempotency.Relational

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency.Relational.svg)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency.Relational/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

Shared Entity Framework Core base for building relational **DKNet.AspCore.Idempotency** stores — the entity, the
entity mapping, the shared `DbContext`, and the concurrency-safe insert-or-query pattern that
`DKNet.AspCore.Idempotency.MsSqlStore` and `DKNet.AspCore.Idempotency.NpgsqlStore` both derive from.

> This is an internal base package, not something app authors register directly. If you are adding idempotency to
> an app, install `DKNet.AspCore.Idempotency` plus a concrete provider package (`.MsSqlStore` / `.NpgsqlStore`)
> instead. Use this package only when implementing support for a **new** relational database.

## Features

- Shared `IdempotencyKeyEntity` and `IEntityTypeConfiguration<IdempotencyKeyEntity>` mapping every relational
  provider reuses as-is
- Shared `IdempotencyDbContext` base with automatic discovery of the derived provider's own entity configuration
- Race-safe insert-or-query reservation pattern (`IdempotencyRelationalStore<TContext>`) backed by a unique-index
  guarantee, including atomic reclaim of expired reservations
- Per-connection-string migration guard so a process serving multiple databases migrates each one exactly once
- Only two seams left to each provider: the response-body column type/check-constraint SQL, and how to recognize
  that provider's own unique-key-violation error

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency.Relational
```

## Quick Start — implementing a new provider store

A new relational provider derives four types from this package:

```csharp
// 1. Closed DbContext
internal sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
    : DKNet.AspCore.Idempotency.Relational.Data.IdempotencyDbContext(options);

// 2. Provider-specific mapping details
internal sealed class IdempotencyKeyConfiguration
    : DKNet.AspCore.Idempotency.Relational.Data.Configurations.IdempotencyKeyConfiguration
{
    protected override string BodyColumnType => "text";
    protected override string StatusCodeCheckConstraintSql => "\"StatusCode\" BETWEEN 100 AND 599";
}

// 3. Provider-specific unique-violation detection
internal sealed class IdempotencyPostgresStore(
    IServiceProvider serviceProvider, IOptions<IdempotencyOptions> options, ILogger<IdempotencyPostgresStore> logger)
    : IdempotencyRelationalStore<IdempotencyDbContext>(serviceProvider, options, logger)
{
    protected override bool IsProviderUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

// 4. DI wiring (own project's setup extension)
services.AddDbContext<IdempotencyDbContext>(o => o.UseNpgsql(connectionString))
    .AddDbContextFactory<IdempotencyDbContext>();
services.AddIdempotentKey<IdempotencyPostgresStore>();
```

Because the base types here are `internal`, the new provider project must be added to this package's
`InternalsVisibleTo` list.

## Customisation reference

**This package exposes no public API, so there is nothing to configure and nothing to register.** The four types it
declares — `IdempotencyRelationalStore<TContext>`, `IdempotencyDbContext`, `IdempotencyKeyEntity` and
`IdempotencyKeyConfiguration` — are all `internal`, and its `InternalsVisibleTo` list names only
`DKNet.AspCore.Idempotency.MsSqlStore`, `DKNet.AspCore.Idempotency.NpgsqlStore` and their two test projects. Adding
this package to an application changes nothing about that application.

The runtime knobs that affect a relational store are the core package's `IdempotencyOptions` — see
[DKNet.AspCore.Idempotency](https://github.com/baoduy/DKNet/blob/main/src/AspNet/DKNet.AspCore.Idempotency/README.md).
Of those, this base reads only `InFlightReservationTimeout` (30 seconds by default), the lifetime of the `StatusCode
= 102` reservation row on both the fresh-insert and the reclaim path.

For in-repo provider authors, the internal contract is two `protected abstract` members and one override:

| Seam | Declared on | Example values |
|---|---|---|
| `BodyColumnType` | `IdempotencyKeyConfiguration` | `nvarchar(max)` on SQL Server, `text` on PostgreSQL |
| `StatusCodeCheckConstraintSql` | `IdempotencyKeyConfiguration` | `[StatusCode] BETWEEN 100 AND 599` vs `"StatusCode" BETWEEN 100 AND 599` |
| `IsProviderUniqueViolation(DbUpdateException)` | `IdempotencyRelationalStore<TContext>` | `SqlException { Number: 2601 or 2627 }` vs `PostgresException { SqlState: 23505 }` — never a message substring, which the server localises |

Everything else — the table name, column lengths, the `UX_CompositeKey` unique index, the `IX_IdempotencyKeys_ExpiresAt`
index and the reserve/complete/reclaim flow — is fixed by the shared mapping and cannot be overridden.

## Requirements

- .NET 10.0+
- `Microsoft.EntityFrameworkCore` / `Microsoft.EntityFrameworkCore.Relational`
- DKNet.AspCore.Idempotency (automatically included)

## Full documentation

See [DKNet.AspCore.Idempotency.Relational](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Idempotency.Relational.md)
for the full feature breakdown, mapping defaults, and gotchas.

## License

MIT — see [LICENSE](https://github.com/baoduy/DKNet/blob/main/LICENSE).

## About

Developed by [Steven Hoang](https://drunkcoding.net).
