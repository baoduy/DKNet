# DKNet.AspCore.Idempotency

| Field | Value |
|---|---|
| Area | AspNetCore |
| NuGet package | `DKNet.AspCore.Idempotency` — `dotnet add package DKNet.AspCore.Idempotency` |
| NuGet page | https://www.nuget.org/packages/DKNet.AspCore.Idempotency |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Idempotency.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/AspNet/DKNet.AspCore.Idempotency |
| Depends on (DKNet) | `DKNet.Fw.Extensions` (Core) |
| Depends on (3rd party) | `FluentResults`; `FrameworkReference: Microsoft.AspNetCore.App` (this is an ASP.NET Core-hosted library, not a plain library) |
| Target framework | `net10.0` |

## Purpose

An `IEndpointFilter` for ASP.NET Core **minimal APIs** that makes mutating endpoints safe to retry: it validates a client-supplied idempotency key header, atomically reserves it against a pluggable `IIdempotencyKeyStore`, runs the handler only for a genuinely new key, and either rejects (`409`) or replays a duplicate from cache. Wiring is two calls — `AddIdempotentKey()` in DI, `RequiredIdempotentKey()` on the route or route group — with no change to the handler itself.

**Not for**: MVC controller actions (it only extends `RouteHandlerBuilder`/`RouteGroupBuilder`), naturally-idempotent verbs you don't explicitly opt in (`PUT`/`DELETE` are excluded from the group default), or multi-instance production traffic on the shipped in-process store (that store is process-local only — use `DKNet.AspCore.Idempotency.MsSqlStore`/`NpgsqlStore`/`RedisStore` instead).

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `AddIdempotentKey()` | `public static IServiceCollection AddIdempotentKey(this IServiceCollection services, Action<IdempotencyOptions>? config = null)` | `IServiceCollection` | Registers the in-process default store (`IdempotencyInMemoryStore`, singleton) plus a startup-warning hosted service. No-op (including `config`) if any `IIdempotencyKeyStore` is already registered. |
| `AddIdempotentKey<TStore>()` | `public static IServiceCollection AddIdempotentKey<TStore>(this IServiceCollection services, Action<IdempotencyOptions>? config = null) where TStore : class, IIdempotencyKeyStore` | `IServiceCollection` | Registers a named store (singleton). Replaces the in-process default if that's what's registered; otherwise a no-op if a *different* named store already won. `TStore` must be accessible at your call site — every shipped store type is `internal`, so this generic overload is for your own store type. |
| `RequiredIdempotentKey()` (route) | `public static RouteHandlerBuilder RequiredIdempotentKey(this RouteHandlerBuilder builder)` | `RouteHandlerBuilder` (result of `MapPost`/`MapPut`/etc.) | Adds the endpoint filter via `AddEndpointFilter<T>()`. Call `AddIdempotentKey()`/`AddIdempotentKey<T>()` first — the filter resolves `IIdempotencyKeyStore`/`IOptions<IdempotencyOptions>` from DI at request time. |
| `RequiredIdempotentKey(params string[] httpMethods)` (group) | `public static RouteGroupBuilder RequiredIdempotentKey(this RouteGroupBuilder group, params string[] httpMethods)` | `RouteGroupBuilder` | Covers every endpoint in the group (nested groups too, and endpoints mapped later) whose routed HTTP verb matches, case-insensitively. Default (no args) = `POST` only. Coverage is decided once, at endpoint-build time, from routed metadata — never from the live request. An endpoint with no routed verb (`app.Map(...)`) is never covered. |
| `IdempotencyKeyScopeResolver.Resolve(...)` | `public static string Resolve(HttpContext context, IdempotencyOptions options)` | static method (`DKNet.AspCore.Idempotency.Filtering`) | Public, but normally invoked only by the endpoint filter; call it directly only if writing your own filter/store. |

## Public surface

### `DKNet.AspCore.Idempotency`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencySetup` | static class | DI + route-builder entry points | `AddIdempotentKey(...)`; `AddIdempotentKey<TStore>(...)`; `RequiredIdempotentKey(RouteHandlerBuilder)`; `RequiredIdempotentKey(RouteGroupBuilder, params string[])` |
| `IdempotencyOptions` | sealed class | Options-pattern configuration surface | See **Options & defaults** below |
| `IdempotentConflictHandling` | enum | Duplicate-request strategy | `CachedResult`, `ConflictResponse` (default) |

### `DKNet.AspCore.Idempotency.Filtering`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencyEndpointFilter` | `internal sealed class` (`IEndpointFilter`) | The filter itself; never constructed by consumer code — resolved via `ActivatorUtilities` | `ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext, EndpointFilterDelegate)` |
| `IdempotencyKeyScopeResolver` | public static class | Default caller-scope fallback chain | `static string Resolve(HttpContext, IdempotencyOptions)` |
| `IdempotentKeyInfo` | public sealed record | Extracted per-request key data, passed to the store | `string? IdempotentKey { get; init; }`; `required string Endpoint { get; init; }`; `required string Method { get; init; }`; `string Scope { get; init; } = string.Empty`; `string SafeKey { get; }` (lazy, control-char-stripped, log/display only); `string CompositeKey { get; }` (lazy, `$"{Scope}:{Method}:{Endpoint}:{IdempotentKey ?? string.Empty}"`); `IResultBase IsValid(IdempotencyOptions options)`; `override string ToString()` |

### `DKNet.AspCore.Idempotency.Store`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IIdempotencyKeyStore` | public interface | The one real extension point — a pluggable backing store | `ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)`; `ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)` |
| `CachedResponse` | public sealed record | What a store round-trips; every member `required` | `required int StatusCode`; `required string? Body`; `required string ContentType`; `required DateTimeOffset CreatedAt`; `required DateTimeOffset? ExpiresAt`; `bool IsExpired { get; }` (derived) |
| `IdempotencyInMemoryStore` | `internal sealed class` (`IIdempotencyKeyStore`) | Shipped default store; `ConcurrentDictionary`-backed, process-local | Not constructible by consumer code; registered by `AddIdempotentKey()`. Uses HTTP `102` as the in-flight sentinel; sweeps expired entries every 256 writes. |
| `IdempotencyInMemoryStoreWarning` | `internal sealed class` (`IHostedLifecycleService`) | Logs one startup warning while the in-process store is the resolved store | Not consumer-visible; registered by `AddIdempotentKey()`. |

No other public types exist in this package.

## Options & defaults

| Option | Type | Default | Effect |
|---|---|---|---|
| `IdempotencyHeaderKey` | `string` | `"X-Idempotency-Key"` | Header the filter reads the key from |
| `IdempotencyKeyPattern` | `string` | `^[a-zA-Z0-9\-_]+$` | Regex a key must match; setting it recompiles the internal `Regex` once, under a fixed 100ms `Regex.MatchTimeout` — a timing-out match is treated as a mismatch (`400`), never a hang |
| `MaxIdempotencyKeyLength` | `int` | `255` | Longer keys rejected with `400` |
| `ConflictHandling` | `IdempotentConflictHandling` | `ConflictResponse` | `ConflictResponse` → `409`; `CachedResult` → replay original status/body/content-type |
| `Expiration` | `TimeSpan` | `4 hours` | Absolute lifetime of a cached completed result |
| `InFlightReservationTimeout` | `TimeSpan` | `30 seconds` | How long the in-flight (`102`) reservation blocks a retry before it's reclaimable |
| `MinStatusCodeForCaching` / `MaxStatusCodeForCaching` | `int` / `int` | `200` / `299` | Inclusive cacheable status range |
| `AdditionalCacheableStatusCodes` | `HashSet<int>` (get-only, mutable) | empty | Extra cacheable codes outside the min/max window |
| `CachePrefix` | `string` | `"idem"` | Prepended to every storage key |
| `JsonSerializerOptions` | `JsonSerializerOptions` | camelCase naming policy | Serializes/deserializes the cached response body |
| `KeyScopeResolver` | `Func<HttpContext, string?>?` | `null` | Custom scope resolver; when set, used verbatim and the default chain is skipped entirely (a `null` return yields an empty scope) |
| `ScopeHmacSecret` | `string?` | `null` | Enables the `Authorization`-header HMAC-SHA256 fallback in the default scope chain |
| `IncludeClientIpInScope` | `bool` | `false` | Enables the client-IP fallback in the default scope chain |

All fourteen properties are configured via the `Action<IdempotencyOptions>` passed to `AddIdempotentKey()`/`AddIdempotentKey<TStore>()`, registered through the options pattern with ten `.Validate(...)` rules plus `.ValidateOnStart()` (registered once per `IServiceCollection`):
`IdempotencyHeaderKey` non-blank; `CachePrefix` non-blank; `ScopeHmacSecret` null-or-non-blank; `Expiration > TimeSpan.Zero`; `JsonSerializerOptions` non-null; `MaxIdempotencyKeyLength >= 1`; `IdempotencyKeyPattern` non-blank; `MinStatusCodeForCaching >= 100`; `MaxStatusCodeForCaching <= 599`; `MinStatusCodeForCaching <= MaxStatusCodeForCaching`. A failure throws `OptionsValidationException` when the host **starts** (`ValidateOnStart()`), not at the `AddIdempotentKey(...)` call itself.

## Usage patterns

### Zero-infrastructure local/dev setup

**When**: local development or unit tests, no external store needed.

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey();

var app = builder.Build();

app.MapPost("/orders", () => TypedResults.Created("/orders/1"))
    .RequiredIdempotentKey();

await app.RunAsync();
```

**Notes**: keys live in process memory only, are lost on restart, and are not shared across instances — one startup warning is logged while this store serves requests. Never use for multi-instance production traffic.

### Protecting a whole route group

**When**: many endpoints under one prefix need the same protection without per-route calls.

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey();

var app = builder.Build();

var orders = app.MapGroup("/api/orders").RequiredIdempotentKey();              // POST only (default)
var admin = app.MapGroup("/api/admin").RequiredIdempotentKey("POST", "DELETE"); // explicit verbs

orders.MapPost("/", () => TypedResults.Created("/api/orders/1"));  // covered
orders.MapGet("/{id}", (string id) => TypedResults.Ok(id));        // untouched — GET is outside the set
admin.MapDelete("/tenants/{id}", (string id) => TypedResults.Ok(id)); // covered — DELETE named explicitly

await app.RunAsync();
```

**Notes**: coverage is decided once at app start from the endpoint's routed verb; an `X-HTTP-Method-Override` header cannot move an endpoint into or out of coverage. `app.Map(...)` with no verb constraint is never covered.

### Choosing replay instead of 409

**When**: a duplicate request should transparently get the exact original response back instead of an explicit conflict.

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey(options =>
{
    options.ConflictHandling = IdempotentConflictHandling.CachedResult;
});
```

**Notes**: the replayed response's status code, body and content type come straight from the store's `CachedResponse`, not from re-running the handler.

### Caller-scope isolation for anonymous callers

**When**: two different anonymous callers must not collide on the same key/endpoint.

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey(options =>
{
    options.ScopeHmacSecret = builder.Configuration["Idempotency:HmacSecret"];
    options.IncludeClientIpInScope = true; // last-resort fallback
});
```

**Notes**: the default chain is user claim (`ClaimTypes.NameIdentifier`) → HMAC'd `Authorization` header (only if `ScopeHmacSecret` set) → client IP (only if `IncludeClientIpInScope`) → empty scope. The raw header and secret are never logged; only the digest is used.

### Custom scope resolver (e.g. multi-tenant)

**When**: your app already has its own principal/tenant concept and the default chain doesn't fit.

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey(options =>
{
    options.KeyScopeResolver = ctx => ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();
});
```

**Notes**: when `KeyScopeResolver` is non-null it is used verbatim; the default chain (user claim, HMAC, IP) is skipped entirely, even if `ScopeHmacSecret`/`IncludeClientIpInScope` are also set.

### Writing your own `IIdempotencyKeyStore`

**When**: none of the shipped stores fit (e.g. DynamoDB, a message broker's own dedup table).

```csharp
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;

public sealed class MyIdempotencyKeyStore : IIdempotencyKeyStore
{
    public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
    {
        // Atomic check-and-reserve: absent -> write a reservation, return (false, null).
        throw new NotImplementedException();
    }

    public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
    {
        // Overwrite this caller's reservation with cachedResponse.
        throw new NotImplementedException();
    }
}
```

That store type lives in its own file, like any other application class. Wire it up from `Program.cs`:

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey<MyIdempotencyKeyStore>();
```

**Notes**: `IsKeyProcessedAsync` must be atomic (unique index / `SET NX` / CAS — never Get-then-Set), must use a distinguishable in-flight sentinel (every shipped store uses HTTP `102`), must expire that reservation after `InFlightReservationTimeout`, and must hash `IdempotentKeyInfo.CompositeKey` (never `SafeKey`, never truncate/escape it) — see **Gotchas** for the failure modes of getting each wrong.

## Runtime behaviour

Per protected request, the endpoint filter:

1. Guards against double invocation via a request-scoped flag (relevant when an endpoint is covered by both a group declaration and its own `RequiredIdempotentKey()`).
2. Builds an `IdempotentKeyInfo`: reads the header named by `IdempotencyHeaderKey`; resolves `Endpoint` from the route pattern (falling back to the raw request path), upper-invariant; resolves `Method` upper-invariant; resolves `Scope` from `KeyScopeResolver` if set, else `IdempotencyKeyScopeResolver.Resolve`.
3. Calls `IdempotentKeyInfo.IsValid(options)` (presence → length → regex match under the fixed 100ms timeout). Any failure short-circuits with a `400` — the handler never runs.
4. Calls `store.IsKeyProcessedAsync(keyInfo)` — **not** wrapped in try/catch. If `processed == true`: `ConflictResponse` strategy always returns `409`; `CachedResult` strategy replays the stored `CachedResponse` (status/body/content-type), or the same `409` if no cached response exists yet (in-flight).
5. Otherwise runs the actual handler.
6. Serializes the response and calls `store.MarkKeyAsProcessedAsync` — only if the status code is cacheable (`Min`/`MaxStatusCodeForCaching` or `AdditionalCacheableStatusCodes`), and only inside a try/catch: a `JsonException` logs a warning, any other exception logs an error — a caching failure never fails the original response.
7. Returns the handler's result to the client.

## Diagnostics & exceptions

| Type | Severity | When | Fix |
|---|---|---|---|
| `400` (`TypedResults.Problem`) | Client response | `IdempotentKeyInfo.IsValid` fails: missing header, key too long, or pattern mismatch/regex timeout | Send a well-formed header value under `MaxIdempotencyKeyLength`, matching `IdempotencyKeyPattern` |
| `409` (`TypedResults.Problem`) | Client response | `ConflictHandling == ConflictResponse` on a duplicate key, or `CachedResult` with no cached response yet (in-flight/missing) | Client should treat as "already processed"; switch to `CachedResult` for transparent replay once the cache is populated |
| `OptionsValidationException` | Startup fault | Any of the ten `.Validate(...)` rules on `IdempotencyOptions` fails, surfaced by `.ValidateOnStart()` when the host starts | Fix the offending option before deploying; check `exception.Failures` for the specific rule |
| Unhandled exception (any type) | Request fault (`500`) | `store.IsKeyProcessedAsync` throws (e.g. store outage) — not caught by the filter | There is no fail-open/fail-closed toggle; harden the store's own resilience if this matters |
| `JsonException` (caught internally) | Logged warning, no client impact | Serializing the response body for caching fails | Original response still reaches the client uncached; check `JsonSerializerOptions` against the response type |
| Any other `Exception` (caught internally) | Logged error, no client impact | `MarkKeyAsProcessedAsync` throws for any other reason | Original response still reaches the client uncached; investigate the store implementation |

## Gotchas

- **`IsKeyProcessedAsync` has no surrounding try/catch.** A store outage on the duplicate check propagates as an unhandled exception and fails the request — asymmetric with the caching path, which *is* caught. Workaround: harden the store itself; there is no app-level toggle.
- **`AddIdempotentKey()` (no type argument) is a complete no-op once *any* `IIdempotencyKeyStore` is already registered** — including its `config` delegate. A `config` passed to a second `AddIdempotentKey()` call silently never runs. Workaround: configure options on whichever call actually wins, or configure once, early.
- **Between two *named* stores, first registration wins and only the *first* call's `config` ever runs.** A second library/app layering its own `AddIdempotentKey<TOther>(cfg)` on top silently loses both the store and the config. Workaround: register the desired named store first, or route all configuration through the call that will win.
- **A named store registered *after* the in-process default replaces it — and the *named* call's `config` decides shared options** — the reverse of the "first wins" rule above. Registration order matters differently depending on whether the earlier registration was the default or a named store.
- **A group declaration only reaches endpoints with routed `HttpMethodMetadata`.** `app.Map(...)`/`group.Map(...)` (verb-less, also serves GET) is never covered even though it accepts POST. Workaround: always declare explicit verbs (`MapPost`, `MapPut`, ...) on endpoints you want protected.
- **The group verb set is silent about omissions** — no warning when a mutating verb falls outside the named set. A `PUT` in a POST-only-default group is simply unprotected, with nothing flagging it. Workaround: audit the group's mutating routes against the verb arguments you pass to `RequiredIdempotentKey(...)`.
- **`SafeKey` (control-char-stripped) must never be used for storage/lookup** — only `CompositeKey` (raw `IdempotentKey`) is hashed and stored. Using `SafeKey` as a cache key would silently collapse distinct-but-similar keys that differ only in stripped control characters. It exists purely for logging/display.
- **The in-process store's memory is bounded only by a write-driven sweep every 256 writes, not a timer.** A process that goes idle right after a sweep keeps up to 255 already-expired entries until the next write — not a leak, a known trade-off.
- **`IdempotencyKeyPattern`'s 100ms match timeout is a hang backstop, not a fix for a pathological regex.** A nested-quantifier pattern (`^([a-zA-Z0-9]+)+$`) still burns CPU on a long key before timing out, even though the eventual outcome is a safe `400` rather than a hang. Workaround: write custom patterns to be linear (no nested quantifiers).

## Anti-patterns & hallucination traps

- `services.AddIdempotency(...)` / `services.AddDKNetIdempotency(...)` — do not exist; the real call is `AddIdempotentKey()` / `AddIdempotentKey<TStore>()`.
- `[Idempotent]` attribute on a controller action — does not exist. This package only extends `RouteHandlerBuilder`/`RouteGroupBuilder` for **minimal APIs**; there is no MVC-controller attribute form.
- `new IdempotencyInMemoryStore(...)`, `new IdempotencySqlServerStore(...)`, `new IdempotencyPostgresStore(...)`, `new IdempotencyRedisStore(...)` — every shipped store type is `internal`. Select one via `AddIdempotentKey()` (default) or a provider package's `AddIdempotencyWithXxxStore(...)`.
- `IdempotencyEndpointFilter` — `internal sealed class`; you cannot `new` it or reference it by type from consumer code. Access it only indirectly through `RequiredIdempotentKey()`.
- `DKNet.AspCore.Idempotency.CachedResponse` / `DKNet.AspCore.Idempotency.IdempotencyEndpointFilter` / `...IdempotentKeyInfo` at the package root — wrong namespace. These live in `DKNet.AspCore.Idempotency.Store` and `DKNet.AspCore.Idempotency.Filtering` respectively. `IdempotencySetup`, `IdempotencyOptions`, `IdempotentConflictHandling` stay at the root `DKNet.AspCore.Idempotency` namespace.
- Assuming a group's `RequiredIdempotentKey()` with no verb argument covers `PUT`/`DELETE` too — it does not; the default is `POST` only, and coverage is opt-in per verb.
- Assuming `ConflictHandling` defaults to replaying the cached response — the default is `ConflictResponse` (`409`), not `CachedResult`.
- Calling `MarkKeyAsProcessedAsync` yourself from application code to "pre-warm" a key — it's meant to be called exactly once by the filter, from the caller that won the reservation; hand-calling it bypasses the atomic reservation the filter relies on.
- Treating `IdempotentKeyInfo.IsValid` as validating only header *presence* — it also enforces length (`MaxIdempotencyKeyLength`) and format (`IdempotencyKeyPattern` under a timeout) in the same call.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.AspCore.Idempotency.Relational` | Shared EF Core base for a relational store; every type is `internal`. Never referenced by an application — read only if adding a brand-new relational provider in-repo. |
| `DKNet.AspCore.Idempotency.MsSqlStore` | Reach for it instead of the in-process default when deploying to production on SQL Server; register via its own `AddIdempotencyWithMsSqlStore(connectionString, options)`. |
| `DKNet.AspCore.Idempotency.NpgsqlStore` | Same trade-offs as MsSqlStore, for PostgreSQL; `AddIdempotencyWithNpgsqlStore(connectionString, options)`. |
| `DKNet.AspCore.Idempotency.RedisStore` | Reach for it for high-throughput/multi-instance deployments that want to avoid a migrated table; `AddIdempotencyWithRedisStore(connectionString, options)`. |
| `FluentResults` | `IdempotentKeyInfo.IsValid` returns `IResultBase` — check `.IsFailed`/`.Errors`, don't expect an exception. |
| `DKNet.AspCore.Tasks` | Unrelated concern (start-up background jobs) in the same area; not a functional dependency, just a neighbour worth pairing with when hardening an API. |

## Testing notes

- Test an idempotent endpoint with `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory<TEntryPoint>` and real `HttpRequestMessage`s carrying an `X-Idempotency-Key` (or your configured header) — no TestContainers/Docker needed for the in-process store.
- Assert on `(int)response.StatusCode` against real `HttpStatusCode` values, and for `CachedResult` scenarios compare full response bodies between the first and duplicate request rather than only status codes.
- A `sealed class ApiFixtureXxx : WebApplicationFactory<TEntryPoint>, IAsyncLifetime` that overrides `ConfigureWebHost` to call `AddIdempotentKey<TStore>(...)` (or a provider's `AddIdempotencyWithXxxStore`) with the scenario's options is the pattern to copy for testing a specific conflict-handling/scope configuration in isolation.
- To pin "does my endpoint actually require the header on every verb I expect," send an unkeyed request against every mutating verb in a group and assert `200`/`201` (uncovered) vs `400` (covered, key missing) — the group's own silent-omission behaviour (see Gotchas) means this is the only reliable way to catch a missed verb.
