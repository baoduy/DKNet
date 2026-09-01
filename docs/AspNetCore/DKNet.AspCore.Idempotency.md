# DKNet.AspCore.Idempotency

Idempotency support for ASP.NET Core minimal API endpoints. The package wraps an `IEndpointFilter` that recognizes a
client-supplied idempotency key, blocks the same operation from running twice, and replays (or rejects) the retry —
protecting `POST`/`PUT`/`PATCH` handlers from network retries, double-clicks, and at-least-once message redelivery.

## ✨ Why use it?

- **Safe retries** – A client (or a retrying HTTP client, or a message consumer with at-least-once delivery) that
  resends the same request with the same idempotency key never re-executes the side effect.
- **Composite key isolation** – Keys are scoped by HTTP method, route template, and (optionally) caller, so the same
  key value can be reused safely across different endpoints, verbs, or principals without colliding.
- **Two conflict strategies** – Choose whether a duplicate request gets the original response replayed back
  (`CachedResult`) or an explicit `409 Conflict` (`ConflictResponse`, the default).
- **Pluggable storage** – The filter talks to an `IIdempotencyKeyStore` abstraction. Ship with the built-in
  distributed-cache store, or swap in an atomic store from the ecosystem (see
  [Choosing a store](#-choosing-a-store)).
- **Minimal API-native** – A single `.RequiredIdempotentKey()` call on a `RouteHandlerBuilder` is all that's needed
  to protect an endpoint.

Reach for this package whenever a mutating endpoint must be safe to retry — order/payment creation, resource
provisioning, or any handler triggered by a message consumer that might redeliver.

## 🚀 Quick Start

```bash
dotnet add package DKNet.AspCore.Idempotency
```

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);

// The built-in store needs a distributed cache backend (in-memory here; swap for Redis/SQL Server in production).
builder.Services.AddDistributedMemoryCache();

// IdempotencyDistributedCacheStore is internal, so app code cannot name it: the obsolete non-generic
// overload is the only way to select it. It is not atomic — see "Choosing a store" below.
#pragma warning disable CS0618
builder.Services.AddIdempotentKey();
#pragma warning restore CS0618

var app = builder.Build();

app.MapPost("/orders", CreateOrder)
    .RequiredIdempotentKey();

await app.RunAsync();
```

Callers now must send an `X-Idempotency-Key` header on `POST /orders`. A retry with the same header value gets a
`409 Conflict` (default) instead of creating a second order.

> **Every shipped store type is `internal`.** `IdempotencyDistributedCacheStore` here, and
> `IdempotencySqlServerStore`/`IdempotencyPostgresStore`/`IdempotencyRedisStore` in the sibling packages, are all
> internal implementation details. `AddIdempotentKey<TStore>()` can only name a store *you* declare; to use a shipped
> one, call its package's own `AddIdempotencyWithXxxStore(...)` extension, or — for the built-in cache store only —
> the obsolete non-generic overload above. Every `AddIdempotentKey<TStore>()` sample on this page therefore uses
> `MyIdempotencyKeyStore`, a public `IIdempotencyKeyStore` of your own (see
> [Pluggable store abstraction](#pluggable-store-abstraction)).

## 🧩 Features

### Idempotency endpoint filter

`RequiredIdempotentKey()` adds the filter to a single route:

```csharp
app.MapPost("/orders", CreateOrder)
    .RequiredIdempotentKey();
```

It intercepts every request to that route, extracts and validates the idempotency key, checks the store for a prior
result, runs the handler only for genuinely new requests, and caches the response afterward when applicable — all
without touching the handler code itself.

![Workflow diagram of the idempotency filter: an invalid key exits to 400 Bad Request and a duplicate key to 409 Conflict or the cached replay, so only a genuinely new key reaches the endpoint handler. The response is recorded afterwards only when its status is cacheable.](../diagrams/idempotency-request-flow.svg)

The three exits matter: a malformed key and a duplicate key both stop before the handler, and only a genuinely new key
reaches it. Note the asymmetry in failure handling — `IsKeyProcessedAsync` is not wrapped in a `try`/`catch`, so a store
outage on the duplicate check fails the request, while a failure while caching the response afterwards is caught and
logged and the client still gets its result.

### Composite key validation (400 Bad Request)

Before touching the store, the filter validates the incoming key against `IdempotencyOptions`:

- **Presence** – the header (`X-Idempotency-Key` by default) must be present and non-blank.
- **Length** – must not exceed `MaxIdempotencyKeyLength` (default 255).
- **Format** – must match `IdempotencyKeyPattern` (default `^[a-zA-Z0-9\-_]+$`, i.e. UUID-v4 compatible).

Any failure short-circuits the pipeline with a `400 Bad Request` problem response — the handler never runs.

```csharp
app.MapPost("/orders", CreateOrder)
    .RequiredIdempotentKey(); // client omits the header, or sends 300 chars, or "bad key!" -> 400
```

### Duplicate-request handling — two strategies

`IdempotencyOptions.ConflictHandling` controls what a client sees when the same composite key is reused:

```csharp
builder.Services.AddIdempotentKey<MyIdempotencyKeyStore>(options =>
{
    // Default: tell the client explicitly that this was already processed.
    options.ConflictHandling = IdempotentConflictHandling.ConflictResponse; // 409 Conflict

    // Or: silently replay the original response as if it just happened again.
    // options.ConflictHandling = IdempotentConflictHandling.CachedResult;
});
```

With `CachedResult`, the second request gets the exact status code, body, and content type of the first — read back
from the store, not recomputed.

### Response caching (which results get remembered)

Only responses whose status code falls in `[MinStatusCodeForCaching, MaxStatusCodeForCaching]` (default `200`–`299`)
or in `AdditionalCacheableStatusCodes` are cached; everything else (validation errors, `404`s, etc.) is left
unrecorded so a genuinely failed attempt can be retried as a new request:

```csharp
builder.Services.AddIdempotentKey<MyIdempotencyKeyStore>(options =>
{
    // Also remember 201-with-redirect-style responses outside the 2xx window, e.g. 226.
    options.AdditionalCacheableStatusCodes.Add(226);
});
```

### Caller scope isolation

Two different callers sending the identical idempotency key to the identical endpoint must not collide. By default,
`IdempotencyKeyScopeResolver` resolves a scope using a fallback chain:

1. The authenticated user's `ClaimTypes.NameIdentifier` → `user:{id}`.
2. An HMAC-SHA256 digest of the `Authorization` header, only when `ScopeHmacSecret` is configured → `auth:{hash}`.
3. The caller's remote IP address, only when `IncludeClientIpInScope` is `true` → `ip:{address}`.
4. Otherwise, an empty scope (all anonymous callers share one scope for that key/endpoint/method).

```csharp
builder.Services.AddIdempotentKey<MyIdempotencyKeyStore>(options =>
{
    options.ScopeHmacSecret = builder.Configuration["Idempotency:HmacSecret"];
    options.IncludeClientIpInScope = true; // last-resort fallback for fully anonymous callers
});
```

The raw `Authorization` header and the HMAC secret are never logged or persisted — only the resulting digest is used.

### Custom scope resolver

Supply your own resolver to bypass the default chain entirely — useful for multi-tenant scoping by tenant ID, API
key, or any other principal your app already tracks:

```csharp
builder.Services.AddIdempotentKey<MyIdempotencyKeyStore>(options =>
{
    options.KeyScopeResolver = ctx => ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();
});
```

When `KeyScopeResolver` is set, it is used verbatim and the default chain (user claim, HMAC, IP) is skipped.

### Pluggable store abstraction

The filter depends only on `IIdempotencyKeyStore` (namespace `DKNet.AspCore.Idempotency.Store`):

```csharp
public interface IIdempotencyKeyStore
{
    ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo);
    ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse);
}
```

This is the package's one real extension point, and it carries a contract the compiler cannot enforce. A custom
store must guarantee all four of the following:

1. **Atomic check-and-reserve.** `IsKeyProcessedAsync` returning `(false, null)` must have *already durably recorded*
   that this composite key is in flight, in the same indivisible operation that observed it absent. A unique index
   insert, a Redis `SET NX`, or a compare-and-swap all qualify; a `Get` followed by a separate `Set` does not.
   **If you get this wrong:** two concurrent requests both observe `(false, null)`, both run the handler, and the
   side effect the filter exists to protect happens twice.
2. **An in-flight reservation placeholder.** The reservation written in step 1 must be distinguishable from a
   completed response, so that a concurrent duplicate is answered `(true, null)` — "already in flight, no cached
   response yet" — rather than `(true, someResponse)`. Every shipped store uses HTTP `102 Processing` as that
   sentinel. **If you get this wrong:** the filter's `CachedResult` strategy replays an empty or half-written body
   as though it were the original response.
3. **A bounded reservation lifetime.** The placeholder must expire after `IdempotencyOptions.InFlightReservationTimeout`
   (default 30 seconds), and an expired one must be reclaimable — again atomically. **If you get this wrong:** a
   handler that crashes mid-flight blocks that key permanently, and the caller can never retry.
4. **Distinct keys stay distinct.** `IdempotentKeyInfo.CompositeKey` is `Scope:Method:Endpoint:Key` and is free-form
   caller input. Hash it (every shipped store uses SHA-256) rather than escaping or truncating it, so two
   structurally different composite keys can never collapse onto one storage key.

`MarkKeyAsProcessedAsync` has no atomicity requirement — the filter calls it once, from the caller that won the
reservation, and it should overwrite that caller's placeholder with the completed response.

```csharp
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;

public sealed class MyIdempotencyKeyStore : IIdempotencyKeyStore
{
    public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
    {
        // Atomically: if no live entry exists for keyInfo.CompositeKey, write a reservation and return
        // (false, null); otherwise return (true, null) while it is a reservation, or (true, response)
        // once it holds a completed response.
        throw new NotImplementedException();
    }

    public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
    {
        // Overwrite this caller's reservation with cachedResponse, expiring after IdempotencyOptions.Expiration.
        throw new NotImplementedException();
    }
}

// Register it in place of the built-in store:
builder.Services.AddIdempotentKey<MyIdempotencyKeyStore>();
```

`CachedResponse` is what a store round-trips. Every member is `required` on construction:

| Member | Type | Meaning |
|---|---|---|
| `StatusCode` | `int` | The original response's status code, replayed verbatim. `102` is reserved for the in-flight sentinel. |
| `Body` | `string?` | The serialized response body, or `null` for a body-less response. |
| `ContentType` | `string` | The original content type; the filter falls back to `"application/json"` when the response did not set one. |
| `CreatedAt` | `DateTimeOffset` | When the entry was written (UTC). |
| `ExpiresAt` | `DateTimeOffset?` | When it stops being valid, or `null` for no expiry. |
| `IsExpired` | `bool` (derived) | `true` once a non-null `ExpiresAt` has passed. Stores are expected to honour it on read. |

`IdempotentKeyInfo` is what a store receives. `Endpoint` and `Method` are `required`; both are already
upper-invariant when the filter builds one:

| Member | Type | Meaning |
|---|---|---|
| `IdempotentKey` | `string?` | The raw header value, or `null` when the header was absent. |
| `Endpoint` | `string` | Route template (`RoutePattern.RawText`, then `IRouteDiagnosticsMetadata.Route`, then the request path), upper-invariant. |
| `Method` | `string` | HTTP method, upper-invariant. |
| `Scope` | `string` | Caller scope; `string.Empty` for an unscoped anonymous caller. |
| `CompositeKey` | `string` (derived) | `$"{Scope}:{Method}:{Endpoint}:{IdempotentKey}"` — the value to hash and store under. |
| `SafeKey` | `string` (derived) | `IdempotentKey` with CR/LF/U+2028/U+2029 and every other control character stripped. **Logging and display only** — never use it as a storage key. |
| `IsValid(IdempotencyOptions)` | `IResultBase` | The presence/length/pattern check the filter runs before touching the store. |

### In-flight reservation window

While a handler is still running for a brand-new key, the built-in store writes a short-lived reservation record
(HTTP `102 Processing` sentinel) so a concurrent duplicate sees "already in flight" instead of also slipping through
as new. `InFlightReservationTimeout` (default 30 seconds) bounds how long that reservation is honored before a crashed
or hung handler stops blocking retries of the same key:

```csharp
builder.Services.AddIdempotentKey<MyIdempotencyKeyStore>(options =>
{
    options.InFlightReservationTimeout = TimeSpan.FromSeconds(10); // fail fast for quick handlers
});
```

### Cache namespacing and expiration

`CachePrefix` (default `"idem"`) namespaces cache keys to avoid collisions with unrelated cached data, and
`Expiration` (default 4 hours) is the absolute lifetime of a cached idempotency result before the same key is treated
as brand-new again:

```csharp
builder.Services.AddIdempotentKey<MyIdempotencyKeyStore>(options =>
{
    options.CachePrefix = "checkout-idem";
    options.Expiration = TimeSpan.FromHours(24);
});
```

## ⚙️ Configuration reference

All options live on `IdempotencyOptions`, configured via the `Action<IdempotencyOptions>` passed to
`AddIdempotentKey<TStore>()`.

| Option | Default | Purpose |
|---|---|---|
| `IdempotencyHeaderKey` | `"X-Idempotency-Key"` | Header the filter reads the key from. |
| `IdempotencyKeyPattern` | `^[a-zA-Z0-9\-_]+$` | Regex a key must match to be accepted. |
| `MaxIdempotencyKeyLength` | `255` | Maximum accepted key length. |
| `ConflictHandling` | `ConflictResponse` | `ConflictResponse` (409) or `CachedResult` (replay). |
| `Expiration` | `4 hours` | Absolute lifetime of a cached result. |
| `InFlightReservationTimeout` | `30 seconds` | How long an in-flight reservation blocks a retry before expiring. |
| `MinStatusCodeForCaching` / `MaxStatusCodeForCaching` | `200` / `299` | Inclusive status-code range eligible for caching. |
| `AdditionalCacheableStatusCodes` | *(empty)* | Extra status codes to cache outside the min/max range. |
| `CachePrefix` | `"idem"` | Prefix applied to every cache key. |
| `JsonSerializerOptions` | camelCase naming policy | Used to serialize/deserialize cached response bodies. |
| `KeyScopeResolver` | `null` | Custom caller-scope resolver; bypasses the default chain when set. |
| `ScopeHmacSecret` | `null` | Enables the `Authorization`-header HMAC fallback in the default scope chain. |
| `IncludeClientIpInScope` | `false` | Enables the client-IP fallback in the default scope chain. |

`AddIdempotentKey<TStore>()` validates these eagerly at registration time (empty header key, empty cache prefix,
non-positive expiration, an out-of-range status window, etc. all throw `ArgumentException` immediately rather than
failing later at request time).

## 🧱 Where it fits

- **`DKNet.Fw.Extensions`** – the endpoint filter uses its reflection helper to unwrap `TypedResults`-style results
  before serializing them for caching.
- **`FluentResults`** – `IdempotentKeyInfo.IsValid` returns an `IResultBase`, following the same result pattern used
  across DKNet.
- Pairs naturally with **`DKNet.AspCore.Tasks`** (start-up jobs) and other ASP.NET Core hardening utilities in the
  same `AspNet/` area to build resilient web APIs on top of DKNet's DDD/Onion architecture.
- Defines the storage abstraction (`IIdempotencyKeyStore`) that the store ecosystem below implements.

## 🗄️ Choosing a store

This package owns the endpoint filter, options, and the store *contract* (`IIdempotencyKeyStore`). It ships exactly
one store implementation — `IdempotencyDistributedCacheStore`, built on `IDistributedCache` — and four sibling
packages provide alternatives with stronger concurrency guarantees:

| Store | Package | How you select it | Atomicity | Infra cost | Best for |
|---|---|---|---|---|---|
| Distributed cache (built-in) | `DKNet.AspCore.Idempotency` | `AddIdempotentKey()` — the obsolete non-generic overload | Narrows, does not eliminate, the check-then-act race | None beyond an `IDistributedCache` you may already have | Single-instance apps, local dev, low-traffic endpoints |
| Relational (base) | [`DKNet.AspCore.Idempotency.Relational`](DKNet.AspCore.Idempotency.Relational.md) | Not selectable — every type in it is `internal` and its `InternalsVisibleTo` list is closed | Atomic via a unique index / insert-or-query pattern | Shared EF Core building blocks for the two SQL stores below | Nothing to register; read it only to add a *new* relational provider inside this repo |
| SQL Server | [`DKNet.AspCore.Idempotency.MsSqlStore`](DKNet.AspCore.Idempotency.MsSqlStore.md) | `AddIdempotencyWithMsSqlStore(connectionString, options)` | Atomic (unique index) | A migrated table in an existing SQL Server database | Apps already running SQL Server that want an auditable, queryable idempotency table |
| PostgreSQL | [`DKNet.AspCore.Idempotency.NpgsqlStore`](DKNet.AspCore.Idempotency.NpgsqlStore.md) | `AddIdempotencyWithNpgsqlStore(connectionString, options)` | Atomic (unique index) | A migrated table in an existing PostgreSQL database | Apps already running PostgreSQL, same trade-offs as the SQL Server store |
| Redis | [`DKNet.AspCore.Idempotency.RedisStore`](DKNet.AspCore.Idempotency.RedisStore.md) | `AddIdempotencyWithRedisStore(connectionString, options)` | Atomic (native Redis primitives, e.g. `SET NX`) | A Redis instance/cluster | High-throughput APIs, multi-instance deployments, when you don't want schema migrations |
| Your own | — | `AddIdempotentKey<TStore>(options)` | Whatever you implement — see [Pluggable store abstraction](#pluggable-store-abstraction) | Yours | A backing store none of the above covers |

Guidance:

- **Prefer the built-in distributed-cache store only for single-instance or development scenarios.** It logs a
  startup warning to that effect (`IdempotencyDistributedCacheStore is not atomic under concurrent load.`), and
  `IDistributedCache` has no atomic compare-and-set primitive — this store narrows the check-then-act race window
  with a short-lived in-flight reservation but cannot close it the way a database unique index can. It is also the
  only store you select through the obsolete overload, which is a deliberate nudge away from it.
- **Do not plan on subclassing `DKNet.AspCore.Idempotency.Relational`.** Every type in it is `internal`, and its
  `InternalsVisibleTo` list names only the two in-repo provider packages and their test projects. A store of your own
  implements `IIdempotencyKeyStore` directly, the way the Redis store does.
- **Pick a relational store (MsSql/Npgsql)** when the app already runs that relational database, you want the
  idempotency ledger to live alongside your business data (same backups, same transactional boundary tooling), or
  you want to query/audit processed keys with SQL. Concurrency correctness comes from a unique index plus an
  insert-or-query pattern — see the store's own page for the exact schema.
- **Pick Redis** when you need the lowest latency and highest throughput (no database round-trip / query planner),
  you're scaling horizontally across many instances, or you'd rather avoid owning a migrated table at all. Redis's
  native atomic commands give the same all-or-nothing guarantee as a unique index without a schema.
- **TTL/expiry**: the built-in store relies on the backing `IDistributedCache`'s own absolute-expiration support
  (`Expiration` is passed straight through) — if that cache backend evicts entries early under memory pressure, the
  eviction is silent and the key is simply treated as new again. Redis TTLs expire keys natively with the same
  "silently becomes new" behavior once the TTL elapses. Relational stores need an explicit expiry column and a
  cleanup strategy (a scheduled sweep or query filter) because SQL Server/PostgreSQL rows don't expire themselves —
  see each store's page for its approach.
- **Concurrency**: only the relational and Redis stores give a true atomic guarantee against two simultaneous
  requests for the same key both slipping through as "new". The distributed-cache store's reservation placeholder
  narrows but does not close that window.

The four store pages link back to this section instead of repeating the comparison — update it here first.

## ⚠️ Gotchas & limits

- **Default conflict handling is `409`, not replay.** If you expect a duplicate request to transparently get the
  original response back, you must opt into `ConflictHandling = IdempotentConflictHandling.CachedResult` — the
  default explicitly tells the caller the request was already processed.
- **The built-in store is not safe for multi-instance production traffic.** It only narrows, not eliminates, the
  race between checking and reserving a key. Use a relational or Redis store (above) once you run more than one
  instance or expect real concurrent duplicate traffic.
- **A store failure during the duplicate check is not caught.** `IsKeyProcessedAsync` runs with no surrounding
  try/catch in the filter, so a store outage (e.g. cache/DB unavailable) propagates as an unhandled exception and
  the request fails — there is currently no configurable fail-open/fail-closed toggle for this path. By contrast,
  a failure while *caching* the response after a successful handler run (`MarkKeyAsProcessedAsync`/serialization) is
  caught and logged — the original response still reaches the client even if it couldn't be cached for future
  replay.
- **Minimal APIs only.** `RequiredIdempotentKey()` extends `RouteHandlerBuilder`, so it wires up through
  `app.MapPost(...)`/`MapPut(...)` etc. It is not something you attach to an MVC controller action via attributes.
- **`AddIdempotentKey<TStore>()` is call-once-wins.** If it's called more than once (e.g. by two library extension
  methods), the first call's options and store registration stick; later calls are no-ops.
- **The non-generic `AddIdempotentKey(...)` overload is obsolete — and is still the only public way to select the
  built-in store.** `IdempotencyDistributedCacheStore` is `internal`, so `AddIdempotentKey<IdempotencyDistributedCacheStore>()`
  does not compile outside this repo (`CS0122`). Suppress `CS0618` around the non-generic call for local development,
  and move to an atomic store package before production. The obsolete message's suggested replacements
  (`AddIdempotentKey<IdempotencySqlServerStore>()` and friends) name internal types too — use each package's own
  `AddIdempotencyWithXxxStore(...)` instead.
- **Cache keys are hashed, not human-readable.** The built-in store's cache key is `CachePrefix` + a SHA-256 hex
  digest of the composite key — you cannot reconstruct the original key/scope/endpoint from the cache key alone;
  rely on structured logs (which log the raw composite key) if you need to trace a specific request.

## 🔗 Related packages

- [DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md) – shared EF Core base for building
  a relational store.
- [DKNet.AspCore.Idempotency.MsSqlStore](DKNet.AspCore.Idempotency.MsSqlStore.md) – atomic SQL Server-backed store.
- [DKNet.AspCore.Idempotency.NpgsqlStore](DKNet.AspCore.Idempotency.NpgsqlStore.md) – atomic PostgreSQL-backed store.
- [DKNet.AspCore.Idempotency.RedisStore](DKNet.AspCore.Idempotency.RedisStore.md) – atomic Redis-backed store.
- [DKNet.AspCore.Tasks](DKNet.AspCore.Tasks.md) – start-up background job orchestration for the same `AspNet/` area.
