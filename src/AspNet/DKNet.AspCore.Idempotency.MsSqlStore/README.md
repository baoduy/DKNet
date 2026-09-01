# DKNet.AspCore.Idempotency.MsSqlStore

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency.MsSqlStore.svg)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency.MsSqlStore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

A SQL Server-backed persistent store for `DKNet.AspCore.Idempotency` — idempotency keys and cached
responses survive application restarts, with a race-free reserve-then-check flow under concurrent
duplicate requests.

## Features

- Persists idempotency keys and cached responses in SQL Server via EF Core
- Race-free duplicate handling backed by a database unique index, not application-level locking
- Migrations ship with the package and run automatically on first use
- Configured through the same `IdempotencyOptions` the core package already exposes — no extra options type to learn

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency.MsSqlStore
```

## Quick Start

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyWithMsSqlStore(
    builder.Configuration.GetConnectionString("IdempotencyDb")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
        options.ConflictHandling = IdempotentConflictHandling.CachedResult;
    });

var app = builder.Build();

app.MapPost("/orders", () => Results.Ok())
    .RequiredIdempotentKey();

app.Run();
```

## Customisation reference

There is no SQL Server-specific options type. `AddIdempotencyWithMsSqlStore` configures the shared `IdempotencyOptions` from
`DKNet.AspCore.Idempotency`:

| Knob | Type | Default | Effect |
|---|---|---|---|
| `IdempotencyHeaderKey` | `string` | `"X-Idempotency-Key"` | Request header the filter reads the key from. |
| `IdempotencyKeyPattern` | `string` | `^[a-zA-Z0-9\-_]+$` | Regex a key must match; a mismatch is `400 Bad Request`. |
| `MaxIdempotencyKeyLength` | `int` | `255` | Longer keys are rejected with `400`. |
| `ConflictHandling` | `IdempotentConflictHandling` | `ConflictResponse` | `ConflictResponse` answers a duplicate with `409`; `CachedResult` replays the original status, body and content type. |
| `Expiration` | `TimeSpan` | `4 hours` | Absolute lifetime of a cached result before the key is treated as new again. |
| `InFlightReservationTimeout` | `TimeSpan` | `30 seconds` | Lifetime of the in-flight reservation placeholder before it can be reclaimed. |
| `MinStatusCodeForCaching` | `int` | `200` | Inclusive lower bound of the cacheable status range (must be ≥ 100). |
| `MaxStatusCodeForCaching` | `int` | `299` | Inclusive upper bound (must be ≤ 599 and ≥ the minimum). |
| `AdditionalCacheableStatusCodes` | `HashSet<int>` (get-only, mutable) | empty | Extra status codes cached outside the min/max window. |
| `CachePrefix` | `string` | `"idem"` | Prepended, unchanged, to every storage key. |
| `JsonSerializerOptions` | `JsonSerializerOptions` | camelCase naming policy | Used to serialize and deserialize the cached response body. |
| `KeyScopeResolver` | `Func<HttpContext, string?>?` | `null` | Custom caller-scope resolver. When set it is used verbatim and the default chain is skipped. |
| `ScopeHmacSecret` | `string?` | `null` | Enables the `Authorization`-header HMAC-SHA256 fallback in the default scope chain. |
| `IncludeClientIpInScope` | `bool` | `false` | Enables the client-IP fallback in the default scope chain. |

Values are validated eagerly at registration: an empty header key or cache prefix, a non-positive expiration, a
status-code window outside 100–599 or with min above max, a null `JsonSerializerOptions`, a
`MaxIdempotencyKeyLength` below 1, an empty key pattern, or a whitespace `ScopeHmacSecret` each throw
`ArgumentException` immediately rather than failing at request time.

### Registration entry points

| Method | Registers |
|---|---|
| `AddIdempotencyMsSqlStore(connectionString)` | `IdempotencyDbContext` and `IDbContextFactory<IdempotencyDbContext>` only — **not** the key store, so on its own it leaves no `IIdempotencyKeyStore` registered. |
| `AddIdempotencyWithMsSqlStore(connectionString, config)` | The above, then `AddIdempotentKey<IdempotencySqlServerStore>(config)`. This is the call an application makes. |

### Fixed by this package, not exposed as options

| Setting | Value |
|---|---|
| `EnableRetryOnFailure` | 3 retries, 5 seconds apart |
| `UseQuerySplittingBehavior` | `QuerySplittingBehavior.SplitQuery` |
| `MigrationsAssembly` | this package's assembly |
| `MigrationsHistoryTable` | ``[migrate].[IdempotencyDbContext]`` |
| `optionsLifetime` | `ServiceLifetime.Singleton` (the `DbContext` itself stays scoped) |
| Table, indexes, constraint | `IdempotencyKeys` with `UX_CompositeKey` (unique), `IX_IdempotencyKeys_ExpiresAt`, and `CK_StatusCode_Valid` (100–599) |

You provide a reachable database and a login that can create tables in it; the package creates and migrates the
table on first use. Nothing sweeps expired rows — `ExpiresAt` is indexed so you can add your own cleanup job.

`IdempotencySqlServerStore` is `internal`, so `AddIdempotentKey<IdempotencySqlServerStore>()` does not compile in
application code — `AddIdempotencyWithMsSqlStore(...)` is the supported way in. Both methods are first-wins: a
second call with a different connection string is silently a no-op.

## Learn more

Full registration details, the SQL Server schema, and gotchas around migrations and concurrency:
[DKNet.AspCore.Idempotency.MsSqlStore docs](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Idempotency.MsSqlStore.md).

## Requirements

- .NET 10.0+
- SQL Server 2019+ (or Azure SQL Database)

## License

MIT — see [LICENSE](https://github.com/baoduy/DKNet/blob/main/LICENSE).

## About

Developed by [Steven Hoang](https://drunkcoding.net).
