# DKNet.AspCore.Idempotency.NpgsqlStore

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency.NpgsqlStore.svg)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency.NpgsqlStore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

PostgreSQL-backed persistent store for `DKNet.AspCore.Idempotency`, built on the shared EF Core relational
store (`DKNet.AspCore.Idempotency.Relational`).

## ✨ Why use it?

- **Persistent** – idempotency keys live in a Postgres `IdempotencyKeys` table, surviving app restarts.
- **Atomic under concurrency** – a unique index on the composite key serializes duplicate requests in the
  database itself; no application-level locking needed.
- **Migrations included** – ships with its own EF Core migration and applies it automatically on first use,
  independently per connection string.
- **Multi-database ready** – register the store against several Postgres databases (e.g. per tenant) from the
  same process; each one is prepared on its own.
- **Npgsql-tuned** – retry-on-failure and split-query behaviour are configured out of the box.

## 🚀 Quick Start

```bash
dotnet add package DKNet.AspCore.Idempotency.NpgsqlStore
```

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.NpgsqlStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyWithNpgsqlStore(
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

Clients send an `X-Idempotency-Key` header on `POST /orders`; a retry with the same key replays the first
response instead of creating a second order.

## Customisation reference

There is no PostgreSQL-specific options type. `AddIdempotencyWithNpgsqlStore` configures the shared `IdempotencyOptions` from
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
| `AddIdempotencyNpgsqlStore(connectionString)` | `IdempotencyDbContext` and `IDbContextFactory<IdempotencyDbContext>` only — **not** the key store, so on its own it leaves no `IIdempotencyKeyStore` registered. |
| `AddIdempotencyWithNpgsqlStore(connectionString, config)` | The above, then `AddIdempotentKey<IdempotencyPostgresStore>(config)`. This is the call an application makes. |

### Fixed by this package, not exposed as options

| Setting | Value |
|---|---|
| `EnableRetryOnFailure` | 3 retries, 5 seconds apart |
| `UseQuerySplittingBehavior` | `QuerySplittingBehavior.SplitQuery` |
| `MigrationsAssembly` | this package's assembly |
| `MigrationsHistoryTable` | ``migrate.IdempotencyDbContext`` |
| `optionsLifetime` | `ServiceLifetime.Singleton` (the `DbContext` itself stays scoped) |
| Table, indexes, constraint | `IdempotencyKeys` with `UX_CompositeKey` (unique), `IX_IdempotencyKeys_ExpiresAt`, and `CK_StatusCode_Valid` (100–599) |

You provide a reachable database and a login that can create tables in it; the package creates and migrates the
table on first use. Nothing sweeps expired rows — `ExpiresAt` is indexed so you can add your own cleanup job.

`IdempotencyPostgresStore` is `internal`, so `AddIdempotentKey<IdempotencyPostgresStore>()` does not compile in
application code — `AddIdempotencyWithNpgsqlStore(...)` is the supported way in. Both methods are first-wins: a
second call with a different connection string is silently a no-op.

## 📖 Documentation

Full guide — Postgres schema, concurrency behaviour, multi-database support, and how this store composes with
the core and relational packages:
[DKNet.AspCore.Idempotency.NpgsqlStore.md](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Idempotency.NpgsqlStore.md)

## License

MIT — see [LICENSE](https://github.com/baoduy/DKNet/blob/main/LICENSE).
