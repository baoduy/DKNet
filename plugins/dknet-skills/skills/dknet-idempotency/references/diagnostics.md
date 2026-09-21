# Idempotency diagnostics — one table

Every outcome the idempotency family can produce, across all five packages, in one place. Package-specific detail lives in that package's own reference file; this file is for "I got X, why, and what do I fix."

## Client-visible HTTP outcomes

| Outcome | Cause | Which layer | Fix |
|---|---|---|---|
| `400 Bad Request` | `Idempotency-Key`-style header missing, or over `MaxIdempotencyKeyLength`, or fails `IdempotencyKeyPattern` (including a regex-timeout treated as a mismatch) | Core (`IdempotentKeyInfo.IsValid`) — same for every store | Send a well-formed header value: present, under the length limit, matching the pattern |
| `409 Conflict` | `ConflictHandling == ConflictResponse` (the default) on a duplicate key, **or** `ConflictHandling == CachedResult` but nothing is cached yet (still in-flight) | Core (`IdempotencyEndpointFilter`) — same for every store | Retry after `InFlightReservationTimeout` elapses, or switch to `CachedResult` once you expect the response to already be cached |
| Original response replayed verbatim | `ConflictHandling == CachedResult` and a completed `CachedResponse` already exists for this key | Core + whichever store is registered | Working as intended — status/body/content-type come from the store, not from re-running the handler |
| `500` with no idempotency-specific detail | `store.IsKeyProcessedAsync` threw (store outage) — the endpoint filter does **not** catch this call | Core, but the actual fault is in the store (DB/Redis unreachable) | There is no fail-open/fail-closed toggle. Harden the store's own resilience (connection retries, circuit breaker) |
| Handler's real response, uncached | `MarkKeyAsProcessedAsync` failed after the handler ran (caught, logged, never surfaced) | Core catches; store threw | Check logs for a `JsonException` (serialization) or other exception from the store; the client is unaffected, only future replay is missing |

## Startup faults

| Exception | Cause | Fix |
|---|---|---|
| `OptionsValidationException` | One of ten `IdempotencyOptions` validation rules failed, surfaced by `ValidateOnStart()` the moment the host starts (not at the `Add...` call) | Inspect `exception.Failures`; common causes: empty `IdempotencyHeaderKey`/`CachePrefix`/`IdempotencyKeyPattern`, non-positive `Expiration`, `MaxIdempotencyKeyLength < 1`, `MinStatusCodeForCaching`/`MaxStatusCodeForCaching` outside `100..599` or inverted, whitespace `ScopeHmacSecret`, null `JsonSerializerOptions` |
| `ArgumentNullException` | `services` (or, for the Redis multiplexer overload, `connectionMultiplexer`) is null on any `Add...` call; also thrown — not `ArgumentException` — when a store's `connectionString` parameter is literally `null` (`ArgumentException.ThrowIfNullOrWhiteSpace` throws this subtype specifically for `null`) | Pass real, non-null arguments |
| `ArgumentException` | A store's `connectionString` is empty or whitespace-only (not `null` — see row above) | Supply a real connection string |
| Nothing throws, nothing logs | Calling any `Add...`/`AddIdempotencyWith...Store` a second time with a different store or connection string — every registration in this family is first-wins | Register idempotency exactly once per `IServiceCollection`; check what actually won with a DI-descriptor assertion in a test if unsure |

## Store-specific runtime faults

| Type | Store | When | Fix |
|---|---|---|---|
| `InvalidOperationException` | MsSqlStore, NpgsqlStore | `dotnet ef` design-time factory runs with `IDEMPOTENCY_MSSQL_CONNECTION` / `IDEMPOTENCY_NPGSQL_CONNECTION` unset | Export the store-specific env var — these factories never read `IConfiguration`/`appsettings.json` |
| `DbUpdateException` (`CK_StatusCode_Valid` violation) | MsSqlStore, NpgsqlStore | A cached response's `StatusCode` falls outside `100..599` | Should not occur through normal use; indicates something bypassed the filter's own validation |
| `DbUpdateException` matched by `IsProviderUniqueViolation` (SQL Server 2601/2627; Postgres SQL state `23505`) | MsSqlStore, NpgsqlStore | Two callers race the same `CompositeKey` on insert | Expected control flow, handled internally — never add your own catch around the store expecting to need this |
| `RedisConnectionException` / other `StackExchange.Redis` exceptions | RedisStore | Redis is unreachable when the store runs | No in-memory/degraded fallback exists; ensure Redis availability |
| Silent replay of a "new" request | RedisStore | Redis evicted a completed idempotency key under memory pressure before its TTL — indistinguishable from "key never existed" | Size Redis memory/eviction policy so idempotency keys are never evicted before `Expiration` |
| Unbounded table growth | MsSqlStore, NpgsqlStore | Nothing purges expired rows; `ExpiresAt` is indexed but only reclaimed opportunistically on the next collision for that exact key | Add your own scheduled cleanup job filtering on `ExpiresAt` |

## The `102` sentinel

Every shipped store — in-process, MsSqlStore, NpgsqlStore, RedisStore — uses HTTP status `102` (Processing) as the in-flight reservation marker while a handler runs for a key nobody has completed yet. It is legal under the relational stores' `100..599` check constraint but is never a real completed response:

- Seeing `StatusCode == 102` on a row/entry means "reserved, not yet complete" — branch on the status value, not on row/key existence.
- A query or migration that narrows the valid status range below 102 breaks the reservation/completed distinction.
- The sentinel expires after `IdempotencyOptions.InFlightReservationTimeout` (default 30 seconds); a handler that runs longer than that leaves its reservation reclaimable by the next caller for the same key — the original caller's eventual `MarkKeyAsProcessedAsync` call can then race a second winner.

## Which layer catches what

The endpoint filter's own two store calls are **not symmetric**:

| Call | Wrapped in try/catch? | Consequence of a store outage here |
|---|---|---|
| `store.IsKeyProcessedAsync(keyInfo)` | No | Propagates as an unhandled exception → the request fails with `500` |
| `store.MarkKeyAsProcessedAsync(keyInfo, cachedResponse)` | Yes — `JsonException` logs a warning, any other exception logs an error | The handler's real response still reaches the client; only future replay/dedup for that key is missing |

This is intentional, not a bug to "fix" by adding your own try/catch around the filter — see the core package's Gotchas for why.
