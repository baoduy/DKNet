# Performance findings

Every item: where it is, what it costs, what to do instead. Impact is described in units that can be counted (round trips, allocations, reflection calls) rather than guessed percentages.

Effort key: **S** = under an hour, **M** = half a day to a day, **L** = multi-day / needs design agreement.

> **Not yet actioned — reviewing separately.** Three items here were nonetheless already applied, because they are the same edit as a simplification-report twin that was in scope: **P12** (`blob.Data.ToMemory()`, = S7), **P24** (`IValueHttpResult`, = S4), and **P29** partially (`Convert.ToHexStringLower` and the no-op `ToUpperInvariant`, = S6 — the `AesGcmEncryption` double-base64 and `RsaEncryption` key caching parts remain open). Everything else in this file is untouched.

---

## Data access — DKNet.EfCore.Specifications

### P1 — `ToPageEnumerable` offset-paginates instead of streaming {#p1}

`EfCore/DKNet.EfCore.Specifications/Paging/PageAsyncEnumerator.cs:81`

```csharp
var page = await _query
    .Skip(_currentPage * _pageSize)
    .Take(_pageSize)
    .ToListAsync(cancellationToken)
```

**Cost.** Enumerating a 100,000-row result at the default page size of 100 issues **1,000 separate queries**. Each one re-runs the full `WHERE` and the full `ORDER BY` server-side and then discards a growing prefix, so total server work is O(n²/pageSize) rather than O(n log n). This is the entry point behind `SpecRepoExtensions.ToPageEnumerable` and `ModelSpecRepoExtensions.ToPageEnumerable`, i.e. the API the framework offers for "process a large result set".

**Fix.** EF Core already streams an `IQueryable` over a single open reader:

```csharp
public static IAsyncEnumerable<TEntity> ToPageEnumerable<TEntity>(this IQueryable<TEntity> query, ...)
    => query.AsAsyncEnumerable();
```

That deletes `EfCorePageAsyncEnumerator` (56 lines) and the `pageSize` parameter, and turns 1,000 round trips into 1. It also removes a latent correctness problem: offset pagination over a mutating table can skip or duplicate rows between pages.

If the intent behind chunking was to bound the change tracker rather than the reader, `AsNoTracking().AsAsyncEnumerable()` achieves that directly — and `Query<TEntity, TModel>` already applies `AsNoTracking`.

The `EnsureSpecHasOrdering()` guard in front of it (`SpecificationExtensions.cs:118`) exists because offset paging needs a stable sort. With streaming it is no longer load-bearing, though keeping it is harmless.

**Effort:** S. **Risk:** the public signature keeps its shape; only the `pageSize` overload disappears.

---

### P4 — every dynamic filter condition is rendered to a string and re-parsed {#p4}

`EfCore/DKNet.EfCore.Specifications/Dynamics/DynamicPredicateExtensions.cs:52,95-96`

The typed builder path — `DynamicAnd("Price", Ops.GreaterThan, 100m)` — does this per condition:

1. `IsValidPropertyName` → LINQ scan over the characters, then `ToPascalCase` (allocates), then a compiled `Regex.IsMatch`.
2. `ToPascalCase` **again** in `BuildDynamicExpression` (`:19`) — the same allocation twice.
3. `ResolvePropertyType` → uncached `Type.GetProperty` per path segment.
4. `BuildClause` → builds an interpolated clause string such as `"Price > @0"`.
5. `DynamicExpressionParser.ParseLambda` → tokenises and parses that string into an expression tree.

**Cost.** For a list endpoint with the documented maximum of 20 filter conditions (`ListQuery.MaxFilterCount`), that is 20 parser invocations, ~40 `ToPascalCase` allocations, one uncached reflection lookup per path segment, and 20 throwaway clause strings — on every request, for a set of conditions whose *shape* is drawn from a fixed grammar of 14 operators.

**Fix.** Build the expression tree directly. The package already does exactly this in two other places — `Specification.AddOrderBy(string, ListSortDirection)` (`Definitions/Specification.cs:145`) composes `Expression.PropertyOrField` + `Expression.Lambda` by hand, and `KeysetQueryExtensions.BuildSingleKeyPredicate` composes comparisons by hand. A `switch` over `Ops` mapping to `Expression.GreaterThan`, `Expression.Call(prop, StringContainsMethod, value)`, etc. is roughly the same amount of code as `BuildClause` and removes the parser entirely.

Two things fall out of it:

- **`System.Linq.Dynamic.Core` is no longer needed for the typed path.** It stays only for the `DynamicAnd(string expression, params object?[])` overloads, which take a caller-supplied expression string by design.
- **The blacklist in `DynamicPredicateBuilderExtensions.cs:36-90` becomes dead for the typed path.** `DangerousExpressionPatterns` — 24 substrings such as `"System."`, `"GetType("`, `"Task.Run"` — is scanned per call. Note it is *already* not reachable from the typed path (only `ValidateExpression`, used by the raw-string overloads, consults it), so the cost lands on the raw overloads instead. For those, `ParsingConfig` with a restricted `CustomTypeProvider` is the supported mechanism and is stronger than substring matching.

**Interim cheap version (S, do it regardless):** memoise `ToPascalCase` and `ResolvePropertyType` in `ConcurrentDictionary` caches, and stop calling `ToPascalCase` twice. That alone removes the reflection and most of the allocation without touching the parser.

**Effort:** M for the full expression-tree builder, S for the interim. **Risk:** medium — needs the existing `EfCore.Specifications.Tests` SQL assertions (`ToQueryString()`) to confirm byte-identical SQL. That test pattern is exactly what makes this safe to attempt.

---

### P5 — free-text search parses N clause strings per request {#p5}

`AspNet/DKNet.AspCore.Extensions/Endpoints/ListQuery.cs:221`

```csharp
foreach (var clause in clauses) predicate = predicate.DynamicOr(clause, search);
```

`ModelSearch.Clauses<TModel, TEntity>()` correctly caches the *clause strings* per `(model, entity)` pair. But `DynamicOr(string, params object?[])` then, for each clause, runs `ValidateExpression` (the 24-substring blacklist scan) and `DynamicExpressionParser.ParseLambda`. A model with 8 searchable text fields costs 8 blacklist scans plus 8 parses per request.

The blacklist scan is pure waste here: these clauses are generated by `ModelSearch`, not supplied by the caller. Only the `search` *value* is user input, and it arrives as a bound parameter.

**Fix.** Two options, both good:

- Cache the built `Expression<Func<TEntity,bool>>` template per `(TModel, TEntity)` and swap in the search value via a boxed holder — `Expression.Property(Expression.Constant(box), nameof(box.Value))` — which also gets EF Core to parameterise the `LIKE` argument instead of inlining it as a literal.
- Or build the `Contains` call tree directly in `ModelSearch` instead of emitting strings, which folds this into P4.

**Effort:** S–M. **Risk:** low.

---

### P17 — `TypeExtractor` filters an in-memory array through `IQueryable` {#p17}

`Core/DKNet.Fw.Extensions/TypeExtractors/TypeExtractor.cs:93`

```csharp
var query = _assemblies.SelectMany(a => a.GetTypes()).AsQueryable();
foreach (var predicate in _predicates) query = query.Where(predicate);
```

The predicates are `Expression<Func<Type, bool>>`. Over an `EnumerableQuery`, every `Where` goes through `Queryable`'s expression-rewriting path and the lambdas are **interpreted or compiled at enumeration time** rather than invoked as delegates. For a chain such as `.Classes().NotAbstract().IsInstanceOf<IEndpointConfig>()` over an assembly with 2,000 types, that is expression-tree machinery on 6,000 predicate evaluations for no benefit — nothing here is ever translated to a remote provider.

Two further costs in the same method: `a.GetTypes()` is called fresh on every enumeration (no per-assembly cache), and `ITypeExtractor` extends `IEnumerable<Type>`, so any caller that enumerates twice pays twice.

**Fix.** Change the predicate list to `List<Func<Type, bool>>` and use plain LINQ. The public `Where(Expression<Func<Type,bool>>)` member can keep its signature and call `.Compile()` once at registration. Optionally cache `GetTypes()` per `Assembly` in a static `ConcurrentDictionary`.

Used by `EndpointConfigExtensions.UseEndpointConfigs`, `EfCoreDataSeedingExtensions`, `EntityAutoConfigExtensions`, `SequenceExtensions` — all startup paths, so this is startup latency rather than per-request. Still measurable on a large solution.

**Effort:** S. **Risk:** low. Note the builder is *mutable* and returns `this`, so `FilterBy` appends to shared state; that stays as-is (see [S13](simplification.md#s13)).

---

### P21 — `IgnorableFilterKeys` rebuilds a collection on every query {#p21}

`EfCore/DKNet.EfCore.Extensions/Configurations/GlobalQueryFilter.cs:60`

```csharp
public static IReadOnlyCollection<string> IgnorableFilterKeys =>
    KnownFilterKeys.Where(kv => kv.Value).Select(kv => kv.Key).ToArray();
```

`SpecificationExtensions.ApplySpecs:24` reads this for every specification with `IsIgnoreQueryFilters` set — so a LINQ chain over a `ConcurrentDictionary` plus a fresh `string[]` per query.

**Fix.** The set only changes during model building. Recompute into a cached `string[]` inside `Apply` (where `KnownFilterKeys[FilterKey]` is written) and have the property return the cached array.

**Effort:** S. **Risk:** low.

---

### P22 — `AddNewEntitiesFromNavigations` walks every tracked entity on every save {#p22}

`EfCore/DKNet.EfCore.Extensions/Extensions/NavigationExtensions.cs:169-215`

`GetPossibleUpdatingEntities()` calls `ChangeTracker.DetectChanges()` and then selects entries in state `Detached`, `Modified`, **or `Unchanged`**. For each one, `GetNewEntitiesFromNavigations` enumerates every collection navigation, reads it by reflection (`navigation.PropertyInfo.GetValue`, `NavigationExtensions.cs:23`), calls `context.Entry(child)` for every child, and `IsNewEntity()` materialises `GetOriginalKeyValues().ToList()` per child.

**Cost.** With 200 tracked entities averaging 3 collection navigations of 10 children, one save does 600 reflection `GetValue` calls, 6,000 `context.Entry` lookups, and 6,000 short-lived `List<object?>` allocations — including for entities in `Unchanged` state that are not being saved at all. This runs from two places on every write: `RepositorySpec.SaveChangesAsync` (`Repositories/IRepositorySpec.cs:229`) and `EfAutoSavePostInterceptor.OnHandle` — so a SlimBus write command pays it **twice**.

**Fix, in order of preference:**

1. **Establish whether it is needed at all.** EF Core discovers reachable untracked entities through relationship fixup during `DetectChanges`/`SaveChanges`; `DbContext.Add` on a graph roots the whole graph. If that covers the intended cases, the entire mechanism deletes. This needs a spike with a failing-first test to confirm — it is the highest-value item in this file if it holds.
2. If it is needed: drop `Unchanged` from the state filter, replace the reflection read with `entry.Collection(nav.Name).CurrentValue` (EF's compiled accessor, also correct for backing-field-only navigations), and short-circuit `IsNewEntity` on `entry.State == EntityState.Detached` before touching key values.
3. Either way, call it once per request, not once per layer.

**Effort:** M (spike) then S–M. **Risk:** high without the spike — this sits on the write path of every consumer.

---

### P23 — `RepositorySpec.UpdateAsync` marks the whole entity modified {#p23}

`EfCore/DKNet.EfCore.Specifications/Repositories/IRepositorySpec.cs:236`

```csharp
_dbContext.Entry(entity).State = EntityState.Modified;
var newEntities = _dbContext.GetNewEntitiesFromNavigations(_dbContext.Entry(entity)).ToList();
```

Setting `State = Modified` marks **every** property dirty, so the generated `UPDATE` writes all columns regardless of what changed — wider row locks, more log volume, and every non-clustered index on an untouched column gets maintained. It also calls `Entry(entity)` twice.

For an already-tracked entity, attaching is unnecessary: mutate and let change tracking compute the delta. For a detached entity, `Attach` + selective `IsModified` is the targeted form.

**Effort:** S for hoisting the duplicate `Entry` call; M for the semantic change (needs a decision about detached-entity support, and the return value — currently the count of newly discovered navigation entities, which is a surprising contract for `UpdateAsync`).

---

## Idempotency — DKNet.AspCore.Idempotency*

### P15 — `SafeKey` and `CompositeKey` are recomputed on every read {#p15}

`AspNet/DKNet.AspCore.Idempotency/Filtering/IdempotentKeyInfo.cs:42,130,143`

`SafeKey` is a computed property that on **every access** does three `string.Replace` passes and then a `StringBuilder` character scan. `IdempotencyEndpointFilter` reads it in essentially every log statement and in both 409 bodies — six or more times per request. `CompositeKey` is an interpolated string, also recomputed per read, and read by both store methods.

`IsValid` adds `Regex.IsMatch(IdempotentKey, options.IdempotencyKeyPattern)` — the **static** overload with a pattern *string*, so each request hashes the pattern and hits the 15-entry `Regex` cache.

**Fix.**

- Compute `SafeKey` and `CompositeKey` once (lazy backing field, or in the `init` path). The record is created once per request in `GetIdempotentKeyInfo`, so this is a pure win.
- Replace the regex with a `SearchValues<char>` scan — the default pattern `^[a-zA-Z0-9\-_]+$` is exactly `IndexOfAnyExcept`, allocation-free and no cache lookup. If the pattern must stay configurable, hold a constructed `Regex` instance on `IdempotencyOptions` instead of passing the string per call.
- The `SafeKey` sanitisation itself can be a single `string.Create` pass, or skipped entirely when the key contains no control characters (the common case) via `key.AsSpan().IndexOfAnyInRange('\0', '')`.

**Effort:** S. **Risk:** none — behaviour identical.

---

### P2 — relational store spends two round trips to reserve a key {#p2}

`AspNet/DKNet.AspCore.Idempotency.Relational/Store/IdempotencyRelationalStore.cs:113,180,219`

The common path (a genuinely new request) is: `SELECT` by composite key → miss → `INSERT` reservation. Two round trips before the endpoint body runs. `MarkKeyAsProcessedAsync` then does a tracked `SELECT` + `SaveChanges` — two more.

Also on every call: `EnsureDatabaseCreatedAsync` reads `Database.GetConnectionString()` (allocates) to probe a `ConcurrentDictionary`.

**Fix.**

- **Reserve by insert-first.** Attempt the `INSERT` immediately and only `SELECT` when the unique index rejects it. The unique constraint already provides the single-winner guarantee that the current `SELECT`-then-`INSERT` cannot (and the code already handles the violation path correctly at `:171`). New requests drop to **one** round trip; duplicates cost the same as today.
- **Complete by `ExecuteUpdateAsync`.** `MarkKeyAsProcessedAsync` reads the row only to mutate it. A single `ExecuteUpdateAsync` on the composite key, with the `Add` fallback kept for the zero-rows-affected case, halves that path too.
- Cache the connection-string key per store instance rather than re-reading it per call.

**Effort:** M. **Risk:** medium — this is the concurrency-critical path. The existing `AspCore.Idempotency.MsSqlStore.Tests` concurrency tests are the gate.

---

### P3 — Redis store spends two round trips where `SET NX GET` does one {#p3}

`AspNet/DKNet.AspCore.Idempotency.RedisStore/Store/IdempotencyRedisStore.cs:105`

`IsKeyProcessedAsync` does `StringGetAsync` and then, on a miss, `StringSetAsync(..., When.NotExists)`. Two network round trips per new request.

**Fix.** `StringSetAndGetAsync(key, reservation, expiry, When.NotExists)` sets-if-absent **and** returns the pre-existing value in one command: a `null` return means "we won the reservation", a non-null return is the concurrent value that the code then branches on. One RTT, and the collision re-read at `:117` disappears.

Two smaller items in the same file:

- `SanitizeKey` (`:183`) runs SHA-256 and then `Convert.ToHexString(...).ToLowerInvariant()` — two string allocations where `Convert.ToHexStringLower` (.NET 9) does one. It is also called once per store method, so twice per request for the same input; compute it once in the filter and pass it down.
- `JsonSerializerOptions` is reflection-based. A `JsonSerializerContext` for `CachedResponse` removes the per-request reflection metadata lookup and makes the package trim/AOT-friendly.

**Effort:** S. **Risk:** low — `StringSetAndGetAsync` requires Redis 6.2+ (`SET ... GET`); worth confirming the deployment floor.

---

### P24 — response value extracted by reflection {#p24}

`AspNet/DKNet.AspCore.Idempotency/Filtering/IdempotencyEndpointFilter.cs:119`

```csharp
var resultValue = result is null ? null : result.GetPropertyValue("Value") ?? result;
```

`GetPropertyValue` (`Core/DKNet.Fw.Extensions/Reflection/PropertyExtensions.cs:70`) splits the name on `'.'` into a fresh `string[]`, then does an uncached `Type.GetProperty` with `IgnoreCase | Public | NonPublic | Instance`, then `PropertyInfo.GetValue` — per cached response, per request.

**Fix.** ASP.NET Core declares the contract: `result is IValueHttpResult v ? v.Value : result`. No reflection, no allocation, and it is the interface `TypedResults.Ok<T>`/`Json<T>` actually implement. See also [S4](simplification.md#s4).

Adjacent, same method: `routeTemplate.ToUpperInvariant()` and `Request.Method.ToUpperInvariant()` (`:144-145`) allocate two strings per request. The route template is constant per endpoint (cacheable in endpoint metadata) and HTTP methods arrive already uppercase for all standard verbs — `HttpMethods.IsPost(...)` and friends compare without allocating.

**Effort:** S. **Risk:** none.

---

### P25 — HMAC scope re-encodes the secret per request {#p25}

`AspNet/DKNet.AspCore.Idempotency/Filtering/IdempotencyKeyScopeResolver.cs:33`

`Encoding.UTF8.GetBytes(options.ScopeHmacSecret)` runs on every request that takes the Authorization-header branch, and `Convert.ToHexString(hash).ToLowerInvariant()` allocates twice.

**Fix.** Cache the secret bytes on the options object (it is a singleton), and use `Convert.ToHexStringLower`.

**Effort:** S.

---

## Interceptor stack — Hooks, AuditLogs, DataAuthorization, Events

### P6 — audit log re-resolves attributes per property per save {#p6}

`EfCore/DKNet.EfCore.AuditLogs/AuditLogExtensions.cs:30,37,51` and `Internals/SensitiveDataPatterns.cs:62`

For every audited entity, for every property, `BuildAuditLog` calls:

- `HasAttribute<IgnoreAuditLogAttribute>()`,
- `HasAttribute<AuditLogAttribute>()`,
- `HasAttribute<SensitiveDataAttribute>()`,
- and `SensitiveDataPatterns.IsSensitive`, which LINQ-scans **15 substrings** with `Contains(OrdinalIgnoreCase)` against the property name.

**Cost.** An entity with 20 properties costs 60 attribute lookups plus up to 300 substring scans — per entity, per save. None of it can change at runtime.

**Fix.** Precompute per entity type into a `ConcurrentDictionary<Type, AuditPlan>` holding, per property, the three booleans already resolved. Build it once on first sight of the type; the per-save loop then reads flags. `IsSensitive` also becomes a dictionary hit rather than 15 scans.

While there: `entry.Properties` is enumerated in full for `Modified` entities, reading `OriginalValue` and `CurrentValue` for every property. The `prop.IsModified || !Equals(oldVal, newVal)` test is deliberate (it catches value-comparer cases `IsModified` misses), so this is not free to change — but for the `OnlyAttributedProperties` policy the loop can be driven from the precomputed attributed-property list instead of all properties.

**Effort:** S. **Risk:** low — output is unchanged by construction.

---

### P7 — `DataOwnerHook` stamps properties by reflection {#p7}

`EfCore/DKNet.EfCore.DataAuthorization/Internals/DataOwnerHook.cs:150,171`

`SetOwnedProperty` calls `FindWritableProperty`, which walks the type hierarchy calling `Type.GetProperty` with `DeclaredOnly` at each level until it finds one with a non-public setter — uncached, per property, per entity, per save. It then routes through `PropertyExtensions.SetPropertyValue`, which runs `Convert.ChangeType` and `PropertyInfo.SetValue`.

Four properties are stamped (`CreatedBy`, `CreatedOn`, `UpdatedBy`, `UpdatedOn`) plus `OwnedBy`, so five hierarchy walks per entity per save.

**Fix.** EF Core already has a compiled accessor for exactly this:

```csharp
entry.Entry.Property(nameof(IAuditedProperties.UpdatedBy)).CurrentValue = ownerKey;
```

No reflection, no `Convert.ChangeType`, and it works for private setters, init-only properties, and shadow properties — which `FindWritableProperty` cannot reach at all. The `FindProperty(...) is null` guards already in the method (`:160`, `:196`) mean the metadata is being consulted anyway; this just finishes the job. `FindWritableProperty` (13 lines) deletes.

Same substitution applies to `GuardOwnedByReassignment`'s revert at `:203`.

**Effort:** S. **Risk:** low, and it *widens* what the hook can stamp.

---

### P8 — `HookFactory.LoadHooks` allocates per save {#p8}

`EfCore/DKNet.EfCore.Hooks/Internals/HookFactory.cs:43`

```csharp
var keys = GetProviderKeyNames(dbContext);
var hooks = keys.SelectMany(provider.GetKeyedServices<IHookBaseAsync>).ToImmutableList();
```

Per `SaveChanges`: a `HashSet<string>` plus an array for the type-hierarchy key names, a keyed-service resolution per key, an `ImmutableList` build (node-per-element allocation), and then two `OfType` enumerations that `HookContext` immediately materialises into two more arrays.

**Fix.** Cache `GetProviderKeyNames` per `Type` in a static `ConcurrentDictionary<Type, string[]>` — the hierarchy is fixed. Replace `ToImmutableList` with a plain array or `List<T>`; nothing here needs structural sharing. `HookContext`'s two collection-expression copies (`Internals/HookContext.cs:15-16`) can then be the arrays themselves.

**Effort:** S. **Risk:** none.

---

### P19 — `LogDebug`/`LogInformation` calls without a level guard on hot paths {#p19}

Notably `SlimBus/DKNet.SlimBus.Extensions/Interceptors/EfAutoSavePostInterceptor.cs` (eight calls per request), `AspNet/DKNet.AspCore.Idempotency/Filtering/IdempotencyEndpointFilter.cs`, and `EfCore/DKNet.EfCore.Hooks/Internals/HookRunnerInterceptor.cs`.

Calling `logger.LogDebug("... {A} {B}", a, b)` allocates a `params object?[]` and boxes value-type arguments **before** the logger checks whether the level is enabled. Some call sites already guard with `IsEnabled` (correctly), most do not.

**Fix.** `[LoggerMessage]` source-generated methods. They compile to a level check first and pass arguments through a strongly typed struct — zero allocation when the level is off — and remove the need for hand-written `IsEnabled` guards. Mechanical, and it removes the inconsistency between guarded and unguarded sites.

**Effort:** S–M (mechanical, but touches many call sites). **Risk:** none; message templates are preserved.

---

### P26 — `EventContext` allocates a `HashSet` per entity {#p26}

`EfCore/DKNet.EfCore.Events/Internals/EventContext.cs:34`

`GetEvents()` creates a fresh `HashSet<object> finalEvents` inside the loop over entities, plus a `mapper.Map` call per declared event type. For a save touching many event-raising aggregates this is one hash set per aggregate.

Minor, but in the same file: `_cachedEntities` is declared as `ICollection<IEventEntity>` initialised with `(List<IEventEntity>)[]` and then filled via DKNet's own `ICollection.AddRange` extension. Declaring it `List<IEventEntity>` uses `List<T>.AddRange` (which pre-sizes from the source's count) and drops the cast. The `if (_cachedEntities.Count > 0) return _cachedEntities;` memoisation also re-scans on every call when the result is legitimately empty.

**Effort:** S. **Risk:** none.

---

## HTTP layer — DKNet.AspCore.Extensions

### P16 — the contextual-population filter runs on every request of every endpoint {#p16}

`AspNet/DKNet.AspCore.Extensions/Endpoints/EndpointConfigExtensions.cs:153`

The factory correctly inspects `factoryContext.MethodInfo.GetParameters()` at **build** time to fail fast. But the returned delegate is installed unconditionally, so at **request** time every endpoint does:

```csharp
var population = invocationContext.HttpContext.RequestServices
    .GetService<IContextualRequestPopulationService>();
if (population is not null)
    foreach (var argument in invocationContext.Arguments) ...
```

— a scoped service resolution plus an argument loop, even for endpoints where no parameter type declares a single `IContextualSource` member.

**Fix.** The factory already computed the answer. If no parameter type has declared members, `return next;` — the filter is never installed and costs nothing thereafter.

**Effort:** S. **Risk:** none. This is the single cheapest per-request win in the HTTP layer, and it applies to *every* endpoint mapped through `UseEndpointConfigs`.

---

### P20 — `ContextualRequestPopulationService.Populate` uses `TypeDescriptor` and `SetValue` {#p20}

`AspNet/DKNet.AspCore.Extensions/ModelBinding/ContextualRequestPopulation.cs:81`

Per declared member, per request:

- `resolvers.FirstOrDefault(r => r.CanResolve(...))` — enumerator allocation over the injected `IEnumerable<IContextualValueResolver>`;
- `TypeDescriptor.GetConverter(underlyingType)` — the reflection-and-locking path, one of the slower conversion APIs in the BCL;
- `Activator.CreateInstance(targetType)` to produce a value-type default;
- `member.Property.SetValue(request, ...)` — reflection set.

The overwhelmingly common case is a `string` property populated from a claim, where all four are avoidable.

**Fix.**

- Fast path first: `if (targetType == typeof(string)) return raw;` — skips `TypeDescriptor` entirely for the dominant case.
- Cache the `TypeConverter` per type; better, prefer `IParsable<T>` where available.
- Replace `Activator.CreateInstance` for defaults with a cached boxed default per type (or `RuntimeHelpers.GetUninitializedObject` is not needed — a cached `object` boxed default suffices, since the value is immediately re-boxed into a reflection set).
- Cache a compiled setter delegate per property in `ContextualMember` — the scanner already caches the member list per type (`:63`), so this is the natural place for it.

**Effort:** S for the string fast path and converter cache; M for compiled setters. **Risk:** low.

---

## Blob storage — DKNet.Svc.BlobStorage.*

### P10 — S3 client is per-scope and does `ListBuckets` on first use {#p10}

`Services/DKNet.Svc.BlobStorage.AwsS3/S3BlobService.cs:258` (bucket-ensure), `:246` (client build)

`GetS3ClientAsync` lazily creates `AmazonS3Client` and then unconditionally calls `ListBucketsAsync`, followed by `PutBucketAsync` when the configured bucket is absent. The service is registered `AddScoped` (`S3Setup.cs`) and implements `IDisposable`, disposing `_client` per scope.

**Cost.** Every request that touches blob storage pays: a new `AmazonS3Client` (new HTTP connection pool, no socket reuse across requests) **plus a `ListBuckets` API call** before the actual operation. That is an extra full network round trip on the request path, forever, to answer a question whose answer does not change.

The `if (_client != null) return _client;` guard is also not thread-safe — two concurrent calls in the same scope can each build a client and each call `ListBuckets`.

**Fix.** Register `IAmazonS3` as a **singleton** (the AWS SDK clients are thread-safe and designed to be long-lived) and move the bucket-ensure into a one-shot `Lazy<Task>` or a hosted startup task. Drop `IDisposable` from the scoped service.

`ListItemsAsync` in the same file has a separate problem: no `ContinuationToken` handling, so it silently returns at most the first page (1,000 keys). See [correctness-notes.md](correctness-notes.md#c8).

**Effort:** S. **Risk:** low; the change is in DI wiring plus deleting the eager check.

---

### P11 — Azure container client is per-scope, and `GetAsync` costs three round trips {#p11}

`Services/DKNet.Svc.BlobStorage.AzureStorage/AzureStorageBlobService.cs:159,124`

Same shape as P10: `GetClient()` builds a `BlobServiceClient` per scope and calls `CreateIfNotExistsAsync()` on it — one control-plane call per request that touches blob storage.

`GetAsync` then does three round trips for one download:

```csharp
var props = await b.GetPropertiesAsync(...);   // 1
var es = await b.ExistsAsync(...);             // 2  (unreachable-if-missing: line 1 already threw)
var data = await b.DownloadContentAsync(...);  // 3
```

`GetPropertiesAsync` throws `RequestFailedException(404)` when the blob is absent, so the `ExistsAsync` check below it can never return `false`. And `DownloadContentAsync` returns the properties in its own response.

**Fix.** Register the `BlobServiceClient` as a singleton (`AddAzureClients` from `Microsoft.Extensions.Azure` is the supported way), ensure the container once at startup, and collapse `GetAsync` to a single `DownloadContentAsync` inside a `try/catch (RequestFailedException { Status: 404 })` returning `null`. Three round trips become one.

**Effort:** S. **Risk:** low.

---

### P12 — local save copies the whole payload an extra time {#p12}

`Services/DKNet.Svc.BlobStorage.Local/LocalBlobService.cs:260`

```csharp
await File.WriteAllBytesAsync(finalFile, blob.Data.ToArray(), cancellationToken);
```

`BinaryData.ToArray()` allocates and copies the entire payload before writing it. For a 50 MB upload that is 50 MB of avoidable LOH allocation.

**Fix.** .NET 9 added `File.WriteAllBytesAsync(string, ReadOnlyMemory<byte>, CancellationToken)` — pass `blob.Data.ToMemory()`, which is a view, not a copy. One-word change.

Also in the same file, `GetAsync` (`:130`) never disposes the `FileStream`:

```csharp
var data = await BinaryData.FromStreamAsync(file.OpenRead(), cancellationToken);
```

The stream leaks until finalisation — a held file handle, which on Windows blocks subsequent writes to the same file. `await using var stream = file.OpenRead();` fixes it.

**Effort:** S. **Risk:** none.

---

### P27 — `ListItemsAsync` walks the local tree twice {#p27}

`Services/DKNet.Svc.BlobStorage.Local/LocalBlobService.cs:212-241`

`EnumerateFiles("*", AllDirectories)` followed by `EnumerateDirectories("*", AllDirectories)` — two full recursive traversals of the same tree, two rounds of syscalls.

**Fix.** One `EnumerateFileSystemInfos("*", SearchOption.AllDirectories)` pass, branching on `info is DirectoryInfo`. Note this changes result *ordering* (interleaved rather than files-then-directories); check whether any consumer or test depends on that.

Relatedly, `LocalDirectorySetup.IsDirectory` uses `File.GetAttributes` inside a `try/catch` of two exception types as control flow. `Directory.Exists(path)` returns the same answer with no exception and no attribute read.

**Effort:** S.

---

## Cryptography and hashing — DKNet.Svc.Encryption, DKNet.EfCore.Encryption

### P28 — `AesGcm` instance constructed per operation {#p28}

`EfCore/DKNet.EfCore.Encryption/Encryption/AesGcmColumnEncryptionProvider.cs:73,102`

```csharp
using var aesGcm = new AesGcm(_key, TagSize);
```

Constructing `AesGcm` performs key expansion. This runs **per encrypted column value, per row** — so reading 1,000 rows with two encrypted columns does 2,000 key expansions. The method also allocates six arrays per call (`iv`, `tag`, `actualCipherText`, `plaintextBytes`, `result`, plus the base64 string) and moves data with `Buffer.BlockCopy` where spans would slice in place.

**Fix.** Hold one `AesGcm` per provider instance (the provider is created once per encrypted property at model-build time) and slice the ciphertext with `ReadOnlySpan<byte>` instead of three `BlockCopy` calls. `stackalloc` covers the nonce and tag.

Caveat: `AesGcm.Encrypt`/`Decrypt` thread-safety is not documented as guaranteed. `DKNet.Svc.Encryption.AesGcmEncryption` already takes the conservative route with `lock (_aesGcm)` — which serialises throughput instead. The clean answer is a small pooled/`ThreadLocal` set of instances: no per-call key expansion, no lock contention. Worth a benchmark before choosing.

**Effort:** M. **Risk:** medium — it is the crypto path; the existing round-trip tests are the gate, and the ciphertext format must not change.

---

### P29 — hex and base64 helpers allocate twice {#p29}

- `Services/DKNet.Svc.Encryption/Hashing/ShaHashing.cs:86-87` — `Convert.ToHexString(hash)` then `.ToLowerInvariant()`: two strings. `Convert.ToHexStringLower` (.NET 9) does one.
- `Services/DKNet.Svc.Encryption/Hashing/HmacHashing.cs:96` — `Convert.ToHexString(hash).ToUpperInvariant()`. `ToHexString` **already returns uppercase**; the `ToUpperInvariant` call is a pure extra allocation over an already-correct string. Delete it.
- `Services/DKNet.Svc.Encryption/Base65Extensions.cs:75` — `IsBase64String` heap-allocates `new byte[base64String.Length]` just to run a validity check. `Base64.IsValid(base64String)` (.NET 8) answers it with no allocation.
- `Services/DKNet.Svc.Encryption/Ciphers/AesGcmEncryption.cs:202` — the cipher package is base64 of a colon-joined string of three base64 segments. **Double base64** inflates the payload to ~1.78× the raw bytes and costs five string allocations per encrypt. `AesGcmColumnEncryptionProvider` in the same repo already does the right thing — one concatenated byte array, one base64. Consolidating on that format is both faster and smaller, but it is a **stored-format change**, so it needs a version marker or a migration path.
- `Services/DKNet.Svc.Encryption/Ciphers/RsaEncryption.cs:124` — `PublicKey`/`PrivateKey` re-run `ExportRSAPublicKey()` (ASN.1 encode) plus base64 on **every property read**. Cache both.
- `Services/DKNet.Svc.Encryption/Hashing/HmacHashing.cs:110-118` — `Verify` computes the hash, then base64- or hex-**decodes its own output** back to bytes in order to compare. Compare the raw hash bytes against the decoded expected value and skip the round trip.

**Effort:** S each. **Risk:** none, except the `AesGcmEncryption` format change.

---

## Templating — DKNet.Svc.Transformation

### P30 — extractors rebuilt on every `Transform` call {#p30}

`Services/DKNet.Svc.Transformation/ITransformerService.cs:60,107,118`

```csharp
private ITokenExtractor[] GetExtractors() =>
    [.. Options.DefaultDefinitions.Select(ITokenExtractor (d) => new TokenExtractor(d))];
```

Called from both `Transform` and `TransformAsync`, so a fresh array plus a fresh `TokenExtractor` per definition on every invocation. The definitions never change after options binding.

**Fix.** Build once in the constructor.

Two more in the same package:

- `TokenExtractors/ITokenExtractor.cs:41` — `ExtractAsync` is `Task.Run(() => ExtractCore(...))`. Offloading synchronous CPU work to the thread pool from inside a library burns a thread and adds a context switch for no gain. `TokenResolver.ResolveAsync` in the same package already documents this and returns `Task.FromResult` — the extractor should match.
- `TokenExtractors/TokenResolver.cs:118-121` — `TryGetValueFromObject` does up to **two** uncached `Type.GetProperty` calls per token per candidate object. Cache `(Type, name) → PropertyInfo` in a `ConcurrentDictionary`. `TryGetValueFromDictionary` (`:108`) does `Keys.FirstOrDefault(k => k.Equals(name, OrdinalIgnoreCase))` — an O(n) scan per token where a `TryGetValue` against an `OrdinalIgnoreCase`-comparer dictionary is O(1).

`TransformerService._cacheService` also has a correctness problem — see [correctness-notes.md](correctness-notes.md#c4).

**Effort:** S. **Risk:** low.

---

## PDF generation — DKNet.Svc.PdfGenerators

### P18 — a Chromium process is launched per document {#p18}

`Services/DKNet.Svc.PdfGenerators/PdfGenerator.cs:203`

```csharp
await using var browser = await Puppeteer.LaunchAsync(launchOptions);
await using var page = await browser.NewPageAsync();
```

Every `ConvertMarkdownFileAsync`/`ConvertHtmlAsync` call starts a browser, renders one page, and tears the browser down. Process startup dominates: typically 300–800 ms and 100 MB+ of RSS per document, versus a few tens of milliseconds for a new page/tab in an existing browser.

`IPdfGenerator` is registered **singleton** (`PdfGeneratorSetup.cs`), so the instance already has the right lifetime to hold a browser.

**Fix.** Hold one `IBrowser` behind a `Lazy<Task<IBrowser>>` (or an `AsyncLazy`), create and dispose a `Page` per document, and implement `IAsyncDisposable` on the generator to close the browser at shutdown. Concurrency needs a decision: Puppeteer supports many pages per browser, so a `SemaphoreSlim` bounding concurrent pages is the usual shape.

Two more in the same file:

- `:240` — `EnsureChromeAsync` calls `new BrowserFetcher().DownloadAsync()` on **every** generation when `ChromePath` is null. The fetcher short-circuits when the revision is already present, but it still constructs, resolves paths, and takes the semaphore per document. Make it a one-shot `Lazy<Task>`.
- `:164,179` — `PipelineBuilder.Build()` is called per conversion. Markdig pipelines are explicitly designed to be built once and reused; building one walks and instantiates every registered extension.

Also: `Services/DKNet.Svc.PdfGenerators/Services/EmbeddedResourceService.cs` calls `GetManifestResourceNames()` and `Single(EndsWith)` per resource load — cache the resolved name (or the content) per key. And the five `Regex` instances in `TableOfContentsCreator` (`:723-730`) are `RegexOptions.Compiled`, which pays IL emission at first use; `[GeneratedRegex]` moves that to build time.

**Effort:** M. **Risk:** medium — browser lifetime and concurrency need care. The `Svc.PdfGenerators.Tests` suite covers the output.

---

## SlimBus

### P31 — `IsWriteRequest()` does reflection per request {#p31}

`SlimBus/DKNet.SlimBus.Extensions/Interceptors/EfAutoSavePostInterceptor.cs:79`

```csharp
private static bool IsWriteRequest() =>
    typeof(Fluents.Requests.INoResponse).IsAssignableFrom(typeof(TRequest)) ||
    typeof(TRequest).GetInterfaces().Any(i => i.IsGenericType && ...);
```

`GetInterfaces()` allocates an array and the LINQ chain allocates an enumerator — every request. The answer is fixed per closed generic type.

**Fix.** `private static readonly bool IsWrite = ...;` — computed once per `EfAutoSavePostInterceptor<TRequest, TResponse>` instantiation by the runtime's static initialiser.

**Effort:** S. **Risk:** none.

---

### P32 — `LazyResult` re-runs LINQ on every property read {#p32}

`SlimBus/DKNet.SlimBus.Extensions/LazyMapper/LazyResult.cs:17,21-22`

```csharp
public bool IsFailed => Reasons.OfType<IError>().Any();
public IReadOnlyList<IError> Errors => [.. Reasons.OfType<IError>()];
public IReadOnlyList<ISuccess> Successes => [.. Reasons.OfType<ISuccess>()];
```

`Errors` builds a **new list** per access. `ProblemDetailsExtensions.ToProblemDetails` reads `result.Errors` twice (`:41` and `:45`) and `ResultResponseExtensions.Response` checks `IsSuccess` first — so a single failing response walks `Reasons` three times and allocates two lists.

**Fix.** `IsFailed => Reasons.Any(r => r is IError)` avoids the `OfType` iterator; materialise `Errors`/`Successes` lazily into backing fields (the `Reasons` list is not mutated after construction in practice — confirm before caching).

**Effort:** S. **Risk:** low; verify nothing mutates `Reasons` post-construction.

---

## Core primitives — DKNet.Fw.Extensions

### P33 — string helpers allocate more than they need to {#p33}

- `Primitives/StringExtensions.cs:49` — `ExtractDigits` builds a `char[]` via LINQ `Where` and a collection expression, then a `string` from it: two allocations plus an iterator. `string.Create` with a single pass, or `SearchValues<char>` + `IndexOfAny`, does one.
- `Primitives/StringExtensions.cs:56` — `IsNumber` makes **four** passes over the string (`Count`, `Contains`, `LastIndexOf`, `All`) plus a LINQ enumerator. `decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out _)` is one pass, handles the edge cases the manual checks approximate, and is the stdlib answer.
- `EfCore/DKNet.EfCore.Specifications/Extensions/PropertyNameExtensions.cs:49,76` — `ToPascalCase` does `Split` (array), `StringBuilder`, `ToString`, and for a dotted path a second `Split` plus `string.Join`. It is called at least twice per dynamic filter condition (see P4). Memoise in a `ConcurrentDictionary<string, string>`; property names come from a bounded set.
- `Enums/EnumExtensions.cs:28` — `GetEumInfos<T>` calls `type.GetFields()` with no caching and no `BindingFlags`, then filters by `FieldType == typeof(int)` to skip the backing field. `Enum.GetValues<T>()` (.NET 7+) is the typed, allocation-lighter form — and the `typeof(int)` test is a latent bug for enums with a non-`int` backing type (see [correctness-notes.md](correctness-notes.md#c6)). `GetAttribute` at `:41` calls `@this.ToString()` (allocates, and yields a comma-joined string for flag combinations) purely to feed `GetField`. Cache per `(Type, value)`.
- `Reflection/PropertyExtensions.cs:70` — `GetPropertyValue` splits the path per call and does an uncached `GetProperty` per segment. Cache per `(Type, path)`.

**Effort:** S each. **Risk:** low, except `IsNumber` where `TryParse` semantics differ slightly from the hand-rolled rules (grouping separators, leading `+`). Check the existing tests.

---

### P34 — `StringCreator` allocates three buffers and draws from the RNG three times {#p34}

`Core/DKNet.RandomCreator/StringCreator.cs:68-70`

```csharp
var array = result.ToArray();
var span = array.AsSpan();
RandomNumberGenerator.Shuffle(span);
return span.ToArray();
```

A `List<char>` grown by three `AddRange` calls, then `ToArray()`, then `AsSpan().ToArray()` — the array is copied a second time for no reason, since `Shuffle` mutates in place. `RandomNumberGenerator.GetItems` is also called up to three times (numbers, symbols, letters), i.e. three separate entropy draws.

**Fix.** Allocate one `char[bufferLength]`, fill the three segments into slices of it, `Shuffle`, return. One allocation instead of three, and `ToChars`/`ToString` stop copying.

The class is also `IDisposable` with an empty `Dispose` (`:15`), and `RandomCreators` wraps it in `using` for no effect — see [S12](simplification.md#s12).

**Effort:** S. **Risk:** none — output distribution is unchanged.

---

## Build time — the source generators

### P13 — `CrudGenerator` drives off `CompilationProvider` {#p13}

`SlimBus/DKNet.SlimBus.Generators/CrudGenerator.cs:33`

```csharp
var results = context.CompilationProvider.Select(static (compilation, ct) => CrudModelBuilder.Build(compilation, ct));
context.RegisterSourceOutput(results, static (spc, result) => Emitter.Emit(spc, result));
```

`CompilationProvider` changes on **every edit to any file in the project**. So `CrudModelBuilder.Build` — a full walk of the compilation looking for `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`[GenerateDto]` — re-runs on every keystroke in the IDE and on every incremental build.

The one thing done right: `CrudGenerationResult` implements value equality over its `ImmutableArray`s (`:98-105`), so *emission* is skipped when the model is unchanged. The expensive model **build** is not.

**Fix.** `context.SyntaxProvider.ForAttributeWithMetadataName(...)` for each driving attribute. Roslyn maintains an index of attribute usages, so the provider only fires for files whose attribute usage actually changed. Combine the per-attribute providers, project each to a small value-equatable record of strings/enums (never symbols), and emit from that.

**Effort:** M. **Risk:** medium — a pipeline rewrite, but `SlimBus.Generators.Tests` gives a strong regression net, and the emitted output is asserted.

---

### P14 — `DtoGenerator` puts symbols and hash sets in its pipeline model {#p14}

`EfCore/DKNet.EfCore.DtoGenerator/DtoGenerator.cs:50-51,62,1578-1584`

```csharp
private sealed record Target
{
    public INamedTypeSymbol DtoSymbol { get; set; } = null!;
    public INamedTypeSymbol EntitySymbol { get; set; } = null!;
    public HashSet<string> ExcludedProperties { get; set; } = new();
    public HashSet<string> IncludedProperties { get; set; } = new();
    public bool? IgnoreComplexType { get; set; }
}
```

Three problems compound:

1. **`INamedTypeSymbol` in the model.** Symbols are tied to a specific `Compilation` and are not value-equatable across compilations, so the incremental cache **never hits**. Worse, holding symbols in the pipeline roots the whole `Compilation` in the generator's cache — a known memory-growth pattern in long IDE sessions.
2. **`HashSet<string>` in a record.** Record equality uses the members' `Equals`; `HashSet<T>` does not override it, so equality is **reference** equality. Two structurally identical targets compare unequal, defeating caching even within one compilation.
3. **`.Combine(context.CompilationProvider)`** (`:51`) reintroduces the per-compilation invalidation that `CreateSyntaxProvider` was avoiding, so every source output re-runs regardless.

`RaisesEventValidator.cs:46-54` has the same `CreateSyntaxProvider` + `Combine(CompilationProvider)` shape.

**Fix.**

- Use `ForAttributeWithMetadataName("DKNet.EfCore.DtoGenerator.GenerateDtoAttribute", ...)` instead of `CreateSyntaxProvider`.
- Extract everything needed into a value-equatable model in the transform: `ImmutableArray<string>` (or `EquatableArray<T>`) instead of `HashSet<string>`, and plain `string`s for names/namespaces instead of symbols. If per-property type information is needed, flatten it to strings in the transform too.
- Drop the `CompilationProvider` combine. The analyzer-config combine (`:78`) is fine — it changes rarely.
- Make `Target` immutable (positional record); the settable properties are what allowed the mutable collections in.

Roslyn's own analyzer package ships rules for exactly this class of mistake (`RS1036`/`RS1038` family). Both generator projects already set `EnforceExtendedAnalyzerRules=true` and reference `Microsoft.CodeAnalysis.Analyzers` 5.9.0, so enabling the incremental-generator rules should surface these automatically — worth checking whether they are currently suppressed.

**Effort:** M–L (the file is 1,717 lines, and the model extraction touches most of it). **Risk:** medium; `EfCore.DtoGenerator.Tests` plus the `EfCore.DtoGenerator.TestEntities` project are the regression net.

**Payoff.** These two items are the difference between "the generators re-run when a relevant attribute changes" and "the generators re-run on every keystroke in a 55-project solution". On this codebase's size that is the most user-visible performance item in the repo, even though it never shows up at runtime.
