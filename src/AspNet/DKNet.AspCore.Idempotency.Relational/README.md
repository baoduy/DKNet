# DKNet.AspCore.Idempotency.Relational

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency.Relational.svg)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency.Relational/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

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

## Requirements

- .NET 10.0+
- `Microsoft.EntityFrameworkCore` / `Microsoft.EntityFrameworkCore.Relational`
- DKNet.AspCore.Idempotency (automatically included)

## Full documentation

See [DKNet.AspCore.Idempotency.Relational](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Idempotency.Relational.md)
for the full feature breakdown, mapping defaults, and gotchas.

## License

This project is licensed under the MIT License - see the [LICENSE](https://opensource.org/licenses/MIT) file for
details.

## About

Developed by [Steven Hoang](https://drunkcoding.net).
