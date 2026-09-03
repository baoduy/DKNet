# DKNet.AspCore.Idempotency

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency.svg)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

An ASP.NET Core minimal-API endpoint filter that makes mutating endpoints safe to retry: requests carrying the same
idempotency key are processed once, with duplicates either rejected with `409 Conflict` or replayed from cache.

## Features

- Endpoint filter (`RequiredIdempotentKey()`) that enforces an idempotency key on any minimal API route
- Group-level declaration — the same `RequiredIdempotentKey()` on a `RouteGroupBuilder` covers every `POST`
  endpoint in the group (verb set overridable), nested groups and later-added endpoints included
- Composite key validation (presence, length, format) with automatic `400 Bad Request` responses
- Two duplicate-request strategies: `409 Conflict` (default) or transparent cached-response replay
- Caller scope isolation (authenticated user, HMAC'd `Authorization` header, or client IP) so the same key from
  different callers never collides — or supply your own resolver
- Pluggable storage via `IIdempotencyKeyStore`, plus a zero-infrastructure in-process default (`AddIdempotentKey()`)
  for local development and unit tests
- Configurable which HTTP status codes get cached, cache key prefix, and result expiration

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency
```

## Quick Start

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);

// No store named: idempotency runs on the package's in-process default store — no database,
// no cache, no Redis, no connection string. Keys are process-local, lost on restart, and not
// shared between instances, so this is for local development and unit tests only.
builder.Services.AddIdempotentKey();

var app = builder.Build();

app.MapPost("/orders", CreateOrder)
    .RequiredIdempotentKey();

await app.RunAsync();
```

Clients call `POST /orders` with an `X-Idempotency-Key` header; a retried request with the same key never
re-executes `CreateOrder`.

For multi-instance production traffic, name an atomic store from the ecosystem instead —
`DKNet.AspCore.Idempotency.MsSqlStore`, `DKNet.AspCore.Idempotency.NpgsqlStore`, or
`DKNet.AspCore.Idempotency.RedisStore` — each of which ships its own `AddIdempotencyWithXxxStore(...)`
registration:

```csharp
builder.Services.AddIdempotencyWithMsSqlStore(
    builder.Configuration.GetConnectionString("Idempotency")!);
```

An explicitly named store always wins over the in-process default, whatever the registration order — so a
shared `AddIdempotentKey()` in composition code does not have to be removed before layering a real store on
top in a test fixture or a deployed environment. Between two explicitly named stores, first registration
wins. `AddIdempotentKey<TStore>(...)` is for a public `IIdempotencyKeyStore` you write yourself; every
shipped store type is `internal`.

While the in-process default is the store actually serving requests, the app logs one startup warning that
idempotency keys are process-local, lost on restart, and not shared between instances. A named store emits no
such warning — that warning is how an accidental multi-instance deployment on the default store shows up.

## Group-level declaration

`RequiredIdempotentKey()` also extends `RouteGroupBuilder`, so a single declaration on a group protects every
matching endpoint in it — including endpoints added to that group later, and endpoints of groups nested under it:

```csharp
var orders = app.MapGroup("/api/orders").RequiredIdempotentKey();                 // POST endpoints only
var admin  = app.MapGroup("/api/admin").RequiredIdempotentKey("POST", "DELETE");  // explicit verb set

orders.MapPost("/", CreateOrder);            // protected — no per-endpoint call needed
orders.MapGet("/{id}", GetOrder);            // untouched: no header required, no duplicate lookup
orders.MapGroup("/{id}/lines")               // nested group inherits the same verb selection
      .MapPost("/", AddOrderLine);           // protected
```

The default covered set is `POST` only — `PUT` and `DELETE` are already idempotent by HTTP semantics, so they are
opt-in through the verb arguments. Verbs are matched case-insensitively against each endpoint's routed verb, and
coverage is decided once at application start: nothing the caller sends (an `X-HTTP-Method-Override` header
included) can move an endpoint into or out of coverage.

An endpoint whose verb falls outside the set is left completely alone — no header requirement, no rejection, no
duplicate lookup, nothing retained — so reads keep working for clients that never send a key. A covered endpoint
behaves exactly like an individually declared one: same header name, key validation, duplicate handling and
retention, all still driven by `IdempotencyOptions`. The per-endpoint overload is unchanged, and an endpoint
covered both ways is still protected exactly once.

Two limits worth knowing:

- An endpoint registered without a verb constraint (`app.Map(...)`, which also serves `GET`) has no routed verb to
  match and is **not** covered. Declare explicit verbs (`MapPost`, `MapPut`, ...) on endpoints you want protected.
- A verb set that omits a state-changing endpoint leaves that endpoint unprotected. That is an explicit choice —
  a group protects only the verbs you name.

## Customisation reference

All configuration lives on `IdempotencyOptions`, passed as the `Action<IdempotencyOptions>` on
`AddIdempotentKey()` and `AddIdempotentKey<TStore>()` (and on every store package's
`AddIdempotencyWithXxxStore(...)`). The in-process default store adds no options of its own — it reuses
`Expiration` and `InFlightReservationTimeout`. An expired key is never served, but eviction is write-driven:
the store sweeps expired entries out every 256 writes, so the memory it holds is one `Expiration` retention
window's worth of keys plus up to 255 already-expired entries awaiting the next sweep (a process that goes idle
right after a sweep keeps those until it writes again). Values are
validated eagerly at registration — an empty header key or cache prefix, a non-positive expiration, a
status-code window outside 100–599 or with min above max, a null `JsonSerializerOptions`, a
`MaxIdempotencyKeyLength` below 1, an empty key pattern, or a whitespace `ScopeHmacSecret` each throw
`ArgumentException` immediately rather than failing at request time.

| Knob | Type | Default | Effect |
|---|---|---|---|
| `IdempotencyHeaderKey` | `string` | `"X-Idempotency-Key"` | Request header the filter reads the key from. |
| `IdempotencyKeyPattern` | `string` | `^[a-zA-Z0-9\-_]+$` | Regex a key must match; a mismatch is `400 Bad Request`. |
| `MaxIdempotencyKeyLength` | `int` | `255` | Longer keys are rejected with `400`. |
| `ConflictHandling` | `IdempotentConflictHandling` | `ConflictResponse` | `ConflictResponse` answers a duplicate with `409`; `CachedResult` replays the original status, body and content type. |
| `Expiration` | `TimeSpan` | `4 hours` | Absolute lifetime of a cached result before the key is treated as new again. |
| `InFlightReservationTimeout` | `TimeSpan` | `30 seconds` | How long the in-flight reservation placeholder blocks a retry before it can be reclaimed. |
| `MinStatusCodeForCaching` | `int` | `200` | Inclusive lower bound of the cacheable status range. Must be ≥ 100. |
| `MaxStatusCodeForCaching` | `int` | `299` | Inclusive upper bound. Must be ≤ 599 and ≥ the minimum. |
| `AdditionalCacheableStatusCodes` | `HashSet<int>` (get-only, mutable) | empty | Extra status codes cached outside the min/max window. |
| `CachePrefix` | `string` | `"idem"` | Prepended, unchanged, to every storage key. |
| `JsonSerializerOptions` | `JsonSerializerOptions` | camelCase naming policy | Used to serialize and deserialize the cached response body. |
| `KeyScopeResolver` | `Func<HttpContext, string?>?` | `null` | Custom caller-scope resolver. When set it is used verbatim and the default chain is skipped entirely; returning `null` yields an empty scope. |
| `ScopeHmacSecret` | `string?` | `null` | Enables the `Authorization`-header HMAC-SHA256 fallback in the default scope chain. Only the digest is ever used or logged. |
| `IncludeClientIpInScope` | `bool` | `false` | Enables the client-IP fallback in the default scope chain. |

The default caller-scope chain, used when `KeyScopeResolver` is null: authenticated user's
`ClaimTypes.NameIdentifier` → `user:{id}`; else HMAC of the `Authorization` header when `ScopeHmacSecret` is
set → `auth:{hex}`; else the remote IP when `IncludeClientIpInScope` is `true` → `ip:{address}`; else the
empty string.

### Extension point — `IIdempotencyKeyStore`

```csharp
public interface IIdempotencyKeyStore
{
    ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo);
    ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse);
}
```

`IsKeyProcessedAsync` must check and reserve **atomically**: returning `(false, null)` has to have already
recorded the key as in flight, so no concurrent caller for the same key can also observe `(false, null)`. The
reservation must be distinguishable from a completed response (every shipped store uses HTTP `102` as the
sentinel) and must expire after `InFlightReservationTimeout` so a crashed handler cannot block the key
forever. Get that wrong and duplicate requests both run the handler, which is the one thing this package
exists to prevent.

## Migration — namespace changes in this release

Root types were grouped into concern folders; the namespace of each moved type now ends
with its folder name. This is an import-only source break: no type was renamed, removed,
resignatured, or had its behaviour changed — update the `using` line and you're done.

| Type | Old namespace | New namespace |
|---|---|---|
| `IdempotencyEndpointFilter` (incl. `RequiredIdempotentKey()`), `IdempotencyKeyScopeResolver`, `IdempotentKeyInfo` | `DKNet.AspCore.Idempotency` | `DKNet.AspCore.Idempotency.Filtering` |
| `CachedResponse` | `DKNet.AspCore.Idempotency` | `DKNet.AspCore.Idempotency.Store` (joins the existing `IIdempotencyKeyStore`) |

`IdempotencySetup` (registration point) and `IdempotencyOptions`/`IdempotentConflictHandling`
(the configuration surface) stay at `DKNet.AspCore.Idempotency`.

## Documentation

Full feature guide, configuration reference, and store comparison:
https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Idempotency.md

## License

MIT — see [LICENSE](https://github.com/baoduy/DKNet/blob/main/LICENSE).

## About

Developed by [Steven Hoang](https://drunkcoding.net).
