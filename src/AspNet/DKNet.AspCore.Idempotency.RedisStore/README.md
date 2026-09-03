# DKNet.AspCore.Idempotency.RedisStore

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency.RedisStore.svg)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency.RedisStore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

A Redis-backed idempotency key store for [`DKNet.AspCore.Idempotency`](https://www.nuget.org/packages/DKNet.AspCore.Idempotency/).
Implements the store contract directly against `StackExchange.Redis` — no schema, no migrations, and expiry falls
out of Redis's own TTL.

## Features

- Atomic request reservation via `SET NX` — exactly one concurrent caller per idempotency key proceeds
- SHA-256 hashed, prefixed Redis keys so distinct composite keys never collide
- Native TTL-based expiry for both in-flight reservations and completed responses
- Reuses an existing `IConnectionMultiplexer`/`IDistributedCache`, or registers its own from a connection string

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency.RedisStore
```

## Quick Start

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.RedisStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyWithRedisStore(
    builder.Configuration.GetConnectionString("Redis")!,
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

There is no Redis-specific options type. `AddIdempotencyWithRedisStore` configures the shared
`IdempotencyOptions` from `DKNet.AspCore.Idempotency`:

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

Of those, this store reads `CachePrefix` (Redis key prefix), `Expiration` (TTL on a completed response),
`InFlightReservationTimeout` (TTL on a `SET NX` reservation) and `JsonSerializerOptions` (the stored value's
format). The rest are applied by the endpoint filter before the store is called.

### Fixed by this package, not exposed as options

| Setting | Value |
|---|---|
| Redis key | `CachePrefix` + lowercase hex SHA-256 of `Scope:Method:Endpoint:IdempotentKey` |
| Reservation sentinel | HTTP status `102`, written with `SET … NX` |
| Completion write | unconditional `SET` at the `Expiration` TTL |

You provide a reachable Redis instance, its memory sizing and its eviction policy. There is no schema, no
migration and no cleanup job — expiry is Redis's own TTL. An eviction under memory pressure silently makes a
key look new again.

### Registration entry points

| Method | Registers |
|---|---|
| `AddIdempotencyRedisStore(connectionString)` | `IDistributedCache` via `AddStackExchangeRedisCache` **and** a singleton `IConnectionMultiplexer`, each only when nothing is registered for it yet. Not the key store. |
| `AddIdempotencyRedisStore(connectionMultiplexer)` | The supplied multiplexer as a singleton, and nothing else. Returns early if an `IConnectionMultiplexer` is already registered. |
| `AddIdempotencyWithRedisStore(connectionString, config)` | The first method, then `AddIdempotentKey<IdempotencyRedisStore>(config)`. This is the call an application makes. |

`IdempotencyRedisStore` is `internal`, so `AddIdempotentKey<IdempotencyRedisStore>()` does not compile in
application code — `AddIdempotencyWithRedisStore(...)` is the supported way in. Pass an existing multiplexer to
the second overload first if you do not want this package connecting one of its own.

## Documentation

Full feature reference: https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Idempotency.RedisStore.md

## License

MIT — see [LICENSE](https://github.com/baoduy/DKNet/blob/main/LICENSE).

## About

Developed by [Steven Hoang](https://drunkcoding.net).
