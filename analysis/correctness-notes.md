# Correctness notes

Not part of the requested scope — these surfaced while reading for performance and simplification. Listed separately so they do not muddy the other two reports. Ordered by how likely they are to bite.

Severity is my read, not a triage decision.

**Status key:** ✅ fixed and verified in the working tree · ✖ cancelled (recommendation withdrawn — the reason is recorded in the item) · ❓ awaiting your decision.

---

## ✅ C1 — data seeding compares entities by reference, so it re-inserts on every run {#c1}

`EfCore/DKNet.EfCore.Extensions/Configurations/IDataSeedingConfiguration.cs:65-67`  **High**

**Applied.** Key-based comparison via `EF.Property<object>` projection (shadow keys supported); only key columns read from the DB. Verified by `DataSeedingTests.SeedAsync_RunTwice_DoesNotInsertDuplicatesForReferenceEqualityEntity` against real Postgres.

```csharp
var existing = await dbSet.AsNoTracking().ToListAsync(cancellation);
var existingSet = new HashSet<TEntity>(existing, EqualityComparer<TEntity>.Default);
var toAdd = data.Where(item => !existingSet.Contains(item)).ToList();
```

`TEntity` is constrained to `class`. Unless the entity overrides `Equals`/`GetHashCode`, `EqualityComparer<TEntity>.Default` is **reference** equality — and the freshly materialised `existing` instances can never be reference-equal to the instances `GetDataAsync` just constructed. So `existingSet.Contains(item)` is always `false` and **every seed row is inserted on every startup**, accumulating duplicates until a unique constraint stops it.

Two costs on top of the defect: the entire table is loaded into memory to answer the question, and the comment above it ("Load all existing entities once to avoid N+1 queries") asserts an optimisation that does not work.

**Fix.** Compare by primary key. `context.Model.FindEntityType(typeof(TEntity)).FindPrimaryKey()` gives the key properties; project the existing keys with `Select` so the query returns keys rather than whole entities, and match against them. For records or entities with value equality this bug is invisible, which is likely why it has survived.

---

## ✅ C2 — `HookDisablingContext` disables hooks process-wide {#c2}

`EfCore/DKNet.EfCore.Hooks/Internals/HookDisablingContext.cs:30,46,59`  **High**

**Applied.** Ref-count moved to `AsyncLocal<ImmutableDictionary<string,int>>`; `Dispose` made idempotent. 4 tests added; the old implementation fails 2 of them (double-dispose underflow, cross-flow leak).

```csharp
private static readonly ConcurrentDictionary<string, int> DisabledHooks = new();
// ctor
DisabledHooks.AddOrUpdate(context.GetType().FullName!, 1, (_, oldValue) => oldValue + 1);
```

The ref-count is keyed by **`DbContext` type name in a static dictionary**, so `using (db.DisableHooks())` in one request disables hooks for **every instance of that `DbContext` type in the whole process** for the duration — including concurrent requests on other threads, which then silently skip audit logging, event dispatch, and owner stamping.

For a background job that disables hooks for a bulk import, every concurrent HTTP request loses its audit trail.

**Fix.** `AsyncLocal<int>` (or an `AsyncLocal` holding a per-context set) scopes the suppression to the logical call context, which is what the API's `using` shape implies. Also note `Dispose` is not idempotent: a double dispose decrements twice.

---

## ✖ C3 — ~~hooks are resolved from the root provider~~ — RESOLVED BEFORE HEAD, NOT A LIVE DEFECT {#c3}

**This finding was wrong. It describes a real bug that was already fixed on `dev` before the current HEAD.** Left in place rather than deleted so the reasoning is on record.

What I originally claimed: `HookRunnerInterceptor` is a keyed singleton that resolves `HookFactory` and the keyed-scoped hooks from `CoreOptionsExtension.ApplicationServiceProvider` — the root provider — making every hook and its dependencies captive singletons, so `DataOwnerHook`'s `IDataOwnerProvider` would resolve once and every request would stamp rows with the first request's owner.

Why it does not hold at HEAD:

- `ApplicationServiceProvider` here is **not** the root provider. EF Core populates it from whichever provider built that `DbContext`'s options, and `DbContextOptionsFactory<TContext>` is registered with `optionsLifetime`. Both `AddDbContextWithHook` overloads default `optionLifetime` to `ServiceLifetime.Scoped` (`EfCore/DKNet.EfCore.Hooks/SetupEfCoreHook.cs:50` and `:76`), so it holds the request-scoped provider.
- `HookRunnerInterceptor.GetApplicationServiceProvider` carries an XML doc stating this is deliberate: *"Resolves the DbContext's own application service provider, so hooks are loaded from the same DI scope as the DbContext itself instead of a detached scope off the root provider."*
- `HookContext`'s constructor resolves directly from that provider — there is no `CreateScope()` anywhere in the path.
- Two commits on `dev` made this fix explicitly, both predating HEAD (`334f314`): `61ef4d4` *fix(hooks): resolve hooks from DbContext's own DI scope* removed the detached root scope, and `5696361` *fix(hooks): default DbContextOptions lifetime to Scoped for hook DI resolution* changed `optionLifetime` from `Singleton` back to `Scoped`. The second names the issue `DKNET-HOOK-001` — the singleton-options mechanism is precisely what I re-derived.
- The decisive test already exists and passes: `EfCore/EfCore.HookTests/Hooks/HookScopeResolutionTests.cs:73` (`Hook_AcrossMultipleRequests_ObservesEachRequestsOwnScopedState`) runs two sequential `CreateScope()` blocks with distinct scoped marker values (`"tenant-A"` / `"tenant-B"` — exactly the `IDataOwnerProvider` scenario), saves in each, and asserts the hook observed each request's own value and its own scoped instance. Verified passing at HEAD in an isolated worktree: 7/7 in that class, 31/31 for the project.

Also note the interceptor staying `AddKeyedSingleton` (`SetupEfCoreHook.cs:133`, not `:78` as originally cited) is **correct** and should not be changed: it captures no provider in a field, re-derives one per call from `eventData.Context`, and keys its `HookContext` cache by `ContextId.InstanceId`, so concurrent saves on different contexts in different scopes do not collide. The "register the interceptor as scoped" suggestion in the original write-up was unnecessary — disregard it.

**Root cause of the error:** the citation `SetupEfCoreHook.cs:78` came from a comment-stripped working copy rather than the file on disk, and I reasoned from the registration shape without tracing how EF Core populates `ApplicationServiceProvider` or checking whether a test already covered it. The general lesson for the rest of this report: claims about DI lifetimes need a test or a trace, not an inference from registration calls.

---

## ✅ C4 — the transformer caches token values across calls {#c4}

`Services/DKNet.Svc.Transformation/ITransformerService.cs:47,130-133`  **High**

**Applied.** `_cacheService` field deleted; cache is now a local `Dictionary` scoped to one `Transform`/`TransformAsync` call. 3 tests added; the old implementation fails 2 of them.

```csharp
private readonly ConcurrentDictionary<string, object> _cacheService = new(StringComparer.Ordinal);

private object? TryGetAndCacheValue(IToken token, object[] additionalData)
{
    if (_cacheService.TryGetValue(token.Token, out var value)) return value;
    var val = TryGetValue(token, additionalData);
    if (val is not null) _cacheService.TryAdd(token.Token, val);
    return val;
}
```

The cache key is the token text alone — the `additionalData` that produced the value is not part of it. So:

```csharp
svc.Transform("Hello [Name]", new { Name = "Alice" });   // "Hello Alice"
svc.Transform("Hello [Name]", new { Name = "Bob" });     // "Hello Alice"
```

`ITransformerService` is registered `AddTransient` (`TransformSetup.cs`), which limits the blast radius to a single injected instance's lifetime — but a consumer that holds the service in a scoped or singleton class, or calls `Transform` twice on one instance, gets the wrong output. The dictionary is also unbounded, so a long-lived instance leaks one entry per distinct token seen.

**Fix.** Make the cache per-`Transform`-call (a local `Dictionary` passed down), which is where the deduplication actually pays — the same token appearing twice in one template. Delete the field.

---

## ✅ C5 — `LastDayOfMonth` silently changes `DateTimeKind` {#c5}

`Core/DKNet.Fw.Extensions/Primitives/DateTimeExtensions.cs:36-46`  **Medium**

**Applied.** Now `date.AddDays(DateTime.DaysInMonth(...) - date.Day)` — preserves `Kind` and sub-millisecond precision.

The method reconstructs the `DateTime` from seven components and hardcodes `DateTimeKind.Local`. Pass a UTC timestamp, get a Local one back with the same wall-clock digits — a silent offset error of up to 14 hours in any subsequent conversion or comparison.

Also drops sub-millisecond precision, since the constructor overload used takes only milliseconds.

**Fix.** `date.AddDays(DateTime.DaysInMonth(date.Year, date.Month) - date.Day)` preserves both `Kind` and full precision. See [S9](simplification.md#s9).

---

## ✅ C6 — `GetEumInfos` filters the enum backing field by assuming `int` {#c6}

`Core/DKNet.Fw.Extensions/Enums/EnumExtensions.cs:28-31`  **Medium**

**Applied.** `GetFields(BindingFlags.Public | BindingFlags.Static)`; redundant `FieldType == typeof(int)` check removed. Regression test uses a `byte`-backed enum. Misspelled name retained (breaking to rename — see bucket 1).

```csharp
var members = type.GetFields();
foreach (var info in members)
{
    if (info.FieldType == typeof(int)) continue;
```

`GetFields()` with no `BindingFlags` returns the public static members **plus** the instance field holding the value, named `value__`. The `FieldType == typeof(int)` test is there to skip that field — but it only works for enums with an `int` backing type. For `enum Foo : byte` or `: long`, `value__` is not `int`, so it is **not skipped** and appears in the results as an `EnumInfo` with `Key = "value__"`.

**Fix.** `Enum.GetValues<T>()` / `Enum.GetNames<T>()` (.NET 7+), or `GetFields(BindingFlags.Public | BindingFlags.Static)` which excludes the instance field by construction.

Adjacent, same file (`:41`): `GetAttribute` does `type.GetField(@this.ToString())`. For a `[Flags]` value with multiple bits set, `ToString()` returns `"A, B"`, `GetField` returns `null`, and the method silently reports no attribute rather than indicating it cannot answer for combinations.

Also, the method name is misspelled — `GetEumInfos` / `GetEumInfo` (missing `n`). Renaming is breaking, so it belongs on the major-version list with [S20](simplification.md#s20).

---

## ✅ C7 — Azure `ListItemsAsync` returns the request name for every item {#c7}

`Services/DKNet.Svc.BlobStorage.AzureStorage/AzureStorageBlobService.cs:210-216`  **Medium**

**Applied.** Now constructs with `b.Name`; redundant `Name =` initialiser removed. Test asserts distinct per-item names.

```csharp
await foreach (var b in resultSegment)
    yield return new BlobDetails.BlobResult(blob.Name)   // <- request's name
    {
        Name = blob.Name,                                // <- again
        Details = b.IsDirectory() ? null : new BlobDetails { ... b.Properties ... }
    };
```

`b.Name` — the actual blob name from the listing — is never read. Every result carries the **prefix that was searched for**, so listing a folder returns N items all named after the folder. The `Details` are correct per item, which makes the results internally inconsistent rather than obviously broken.

The local and S3 implementations both use the item's own name (`GetRelativePath(file.FullName)` and `obj.Key`), so this is Azure-specific.

**Fix.** `new BlobDetails.BlobResult(b.Name)` and drop the redundant `Name = ` initialiser.

This also breaks `BlobService.GetItemAsync`, which is implemented as "take the first result of `ListItemsAsync`".

---

## ✅ C8 — S3 `ListItemsAsync` silently truncates at one page {#c8}

`Services/DKNet.Svc.BlobStorage.AwsS3/S3BlobService.cs:281-303`  **Medium**

**Applied.** Paginates on `ContinuationToken`; directory heuristic is now `Key.EndsWith('/')` with `Size.GetValueOrDefault()`. Multi-page coverage added: `ListItemsAsync_MoreThanOnePage_ReturnsAllObjectsAcrossPages` uploads 1001 objects to force a genuine `NextContinuationToken`, since `S3BlobService` sets no `MaxKeys` and the page size cannot be shrunk from the test side.

`ListObjectsV2Async` is called once and its `IsTruncated`/`NextContinuationToken` are never read. Any prefix holding more than 1,000 objects returns the first 1,000 with no error and no indication that results were dropped — the failure mode is a quiet wrong answer.

`DeleteFolderAsync` in the same file (`:115-140`) *does* loop, so the pagination idiom is understood; the listing path just misses it.

**Fix.** Loop on `IsTruncated`, passing `ContinuationToken`. Since the method is already `IAsyncEnumerable`, this is natural — yield each page's objects and fetch the next.

Also in that method (`:296`): `Type = obj.Size > 1 ? BlobTypes.File : BlobTypes.Directory` classifies a zero-byte or one-byte **file** as a directory.

---

## ✅ C9 — local `GetRelativePath` strips the root folder name from anywhere in the path {#c9}

`Services/DKNet.Svc.BlobStorage.Local/LocalBlobService.cs:190-194`  **Medium**

**Applied.** `Path.GetRelativePath(_rootFolder, fullPath)`. Test covers a root whose name repeats as a subfolder, and verifies `GetAsync` round-trips.

`fullPath.Replace(_rootFolder, string.Empty, ...)` removes **every** occurrence. With `RootFolder = "/var/store"`, a file at `/var/store/tenants/store/a.txt` — where `store` legitimately reappears as a subfolder — is reported as `/tenants//a.txt`. Round-tripping that name back through `GetFinalPath` resolves to the wrong file, or throws the path-traversal guard.

**Fix.** `Path.GetRelativePath(_rootFolder, fullPath)`. See [S8](simplification.md#s8).

---

## ✅ C10 — `GetEntityKeyValues` dereferences a possibly-null `PropertyInfo` {#c10}

`EfCore/DKNet.EfCore.Extensions/Extensions/EfCoreExtensions.cs:47`  **Medium**

**Applied.** `entityEntry.CurrentValues[p]` replaces the `PropertyInfo!` dereference; `GetPrimaryKeyValues` likewise. New `EfCoreExtensionsShadowKeyTests` covers a shadow primary key.

```csharp
p => p.PropertyInfo!.GetValue(entityEntry.Entity),
```

`IProperty.PropertyInfo` is `null` for shadow properties and for properties mapped only to a backing field. The `!` converts that into a `NullReferenceException` at runtime. Reached from `AuditLogExtensions.BuildAuditLog` (`Keys = entry.GetEntityKeyValues()`), so an entity with a shadow key crashes audit logging rather than degrading.

**Fix.** `entityEntry.CurrentValues[p]` — no reflection, no null case. `NavigationExtensions.GetCurrentKeyValues` in the same project already does exactly this. See [S15](simplification.md#s15).

---

## ✖ C11 — `EventHook` holds per-save state on a shared instance {#c11}

`EfCore/DKNet.EfCore.Events/Internals/EventHook.cs:32`  **Low** (downgraded — its premise was [C3](#c3), which does not hold)

```csharp
private readonly HashSet<(object Entity, Type EventType)> _declaredEvents = [];
```

`BeforeSaveAsync` clears and fills it; `AfterSaveAsync` reads and clears it. That is safe only if one hook instance serves exactly one `SaveChanges` at a time.

The original concern rested on [C3](#c3) — that hooks were effectively root-provider singletons, so two concurrent saves on different `DbContext` instances could interleave, publishing one save's declared events against the other's. **C3 does not hold: hooks are genuinely request-scoped.** That narrows this to two concurrent `SaveChanges` on the *same* `DbContext` instance, which EF Core does not support regardless. No action needed — the mutable field is safe at the actual lifetime.

`EfCoreAuditHook` (`AuditLogs/Internals/EfCoreAuditHook.cs:24`) has the same shape but keys its state by `ContextId.InstanceId`, which handles the multi-context case — though the container is a plain `Dictionary`, which is not safe for concurrent writes.

---

## ✅ C12 — publisher failures are logged and swallowed {#c12}

`EfCore/DKNet.EfCore.Events/Internals/EventHook.cs:96-103` and `AuditLogs/Internals/EfCoreAuditHook.cs:63-88`  **Medium — by design, but worth confirming**

**Applied.** **Documented as accepted, no logic change.** `<remarks>` added to `EventHook.AfterSaveAsync` and `EfCoreAuditHook.PublishLogsAsync` stating that publishing happens after commit so a failure cannot roll the write back, that the failure is therefore logged and swallowed and the event or entry is lost, and that consumers needing at-least-once delivery must use a transactional outbox rather than these hooks.

Both hooks catch every exception from a publisher, log it, and continue. So a broker outage means domain events are **silently dropped after the transaction has already committed** — the write succeeds, the event never lands, and nothing downstream knows.

That may well be the intended trade-off (an event publish failure should not roll back a committed write, and it cannot). But the current shape offers no recovery path. If at-least-once delivery matters for any consumer, the transactional-outbox pattern is the standard answer: `BeforeSaveAsync` writes the events to an outbox table inside the same transaction, and a separate dispatcher drains it.

Flagging it as a design decision to make explicitly and document, rather than a bug to fix. The audit-log handler additionally serialises the entire log batch to JSON inside its `catch` for the error message (`:76`), which on a large save is a substantial allocation on a failure path — and it is guarded by `IsEnabled(LogLevel.Error)`, so it only fires when error logging is on, which is normally always.

---

## ✅ C13 — `IdempotencyDbContext` migrations run on the request path {#c13}

`AspNet/DKNet.AspCore.Idempotency.Relational/Store/IdempotencyRelationalStore.cs:76-95`  **Low–Medium**

**Applied.** New `IdempotencyMigrationHostedService` registered by both the MsSql and Npgsql setup extensions migrates once at startup. The per-request guard is kept as an explicitly-documented defensive fallback so a host that skips or reorders hosted services still self-heals rather than failing outright, and the connection string is now cached per store instance instead of re-read per call.

`EnsureDatabaseCreatedAsync` is called from both store methods on **every request**. The `ConcurrentDictionary` guard makes the steady state cheap, but the first request after startup runs `GetPendingMigrationsAsync` and possibly `MigrateAsync` **while holding a process-wide `SemaphoreSlim`** — so every concurrent request blocks behind a schema migration, on the request path, with whatever timeout the caller has.

`MigrationLock` is also `static` on an open generic (`IdempotencyRelationalStore<TContext>`), so it is per closed type — correct for the multi-provider case, as the comment explains.

**Fix.** Migrate at startup (`IHostedService`, or `await db.Database.MigrateAsync()` in the composition root) and let the store assume the schema exists. Keep the guard as a defensive fallback if desired, but not as the primary mechanism.

---

## ✅ C14 — `EntityConfigExtensionInfo.GetServiceProviderHashCode` ignores the assemblies {#c14}

`EfCore/DKNet.EfCore.Extensions/Internal/EntityConfigExtensionInfo.cs:16-17`  **Low–Medium**

**Applied.** Assembly identities folded into `GetServiceProviderHashCode()` (order-independent); `ShouldUseSameServiceProvider` now compares assembly sets. 4 tests added.

```csharp
public override int GetServiceProviderHashCode() =>
    nameof(EntityAutoConfigRegister).GetHashCode(StringComparison.Ordinal);
public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;
```

The hash is a constant and `ShouldUseSameServiceProvider` always returns `true`, so two `DbContext` configurations registered with **different assembly sets** via `UseAutoConfigModel(...)` are treated as equivalent for EF Core's internal service-provider and model caching. The second context can then be built against the first's cached model.

EF Core's contract is that this hash must vary with anything that affects the built model, and the assembly list definitely does.

**Fix.** Fold the assembly identities into the hash (e.g. combine `Assembly.FullName` hashes) and compare them in `ShouldUseSameServiceProvider`.

---

## ✅ C15 — `DataOwnerAuthQuery`'s filter shape defeats index use and plan reuse {#c15}

`EfCore/DKNet.EfCore.DataAuthorization/Internals/DataOwnerAuthQuery.cs:60-63`  **Low–Medium**

**Applied.** **Documented as a reviewed trade-off, expression unchanged.** `<remarks>` records the non-sargable `OR`, the variable-length `Contains` plan-cache churn, and that the sargable alternative was considered and deferred because it relocates the unrestricted-access decision into a different mechanism — a security-relevant change needing its own review. It also warns against weakening `IsIgnorable => false` as a side effect.

```csharp
return x =>
    capturedContext.IsUnrestrictedAccess
    || capturedContext.AccessibleKeys.Contains(((IOwnedBy)x).OwnedBy);
```

Two consequences, both about the generated SQL rather than the C#:

- The `OR <scalar>` disjunction means the predicate is not sargable — the optimiser generally cannot use an index on `OwnedBy` when the alternative branch might make every row qualify.
- `AccessibleKeys` is `IEnumerable<string>`, so EF Core expands it inline. A caller with 3 accessible keys and one with 4 produce **different SQL text** and therefore different cached plans. On a multi-tenant system with varying key counts, that is plan-cache churn plus a `Contains`-expansion warning in the EF logs.

`IsIgnorable => false` is correct and deliberately prevents a specification from bypassing this — good. The security posture is sound; the concern is purely how it executes.

**Fix.** Consider keeping the filter to the sargable `Contains` alone and handling the unrestricted case by *not applying* the filter (a separate `DbContext` configuration or an explicit, audited `IgnoreQueryFilters`) rather than by an `OR` inside it. That is a design change with security implications, so it needs deliberate review — not a mechanical fix.

---

## ✅ C16 — `SnapshotContext`'s documentation describes behaviour it does not have {#c16}

`EfCore/DKNet.EfCore.Extensions/Snapshots/SnapshotContext.cs:1-7`  **Low**

**Applied.** Header, class summary and `Dispose` docs rewritten to describe actual behaviour. No behaviour change.

The file header states the type "temporarily disables automatic change detection on the provided `DbContext` until the snapshot is disposed". Nothing in the class touches `ChangeTracker.AutoDetectChangesEnabled`; `Initialize()` calls `DetectChanges()` and `Dispose()` only clears a list and sets a flag.

`DataOwnerHook.UpdatingOwner` separately saves, forces, and restores `AutoDetectChangesEnabled` itself (`DataOwnerHook.cs:60,62,80`), which suggests the responsibility moved and the comment did not.

**Fix.** Correct the comment, or implement what it claims — but check which the hooks actually depend on first; `DetectChanges` is currently called from `SnapshotContext.Initialize`, `GetPossibleUpdatingEntities`, and `DataOwnerHook`, so change detection runs several times per save and the interaction is not obvious.

---

## ✅ C17 — `IsImplementOf` returns `false` for an exact type match {#c17}

`Core/DKNet.Fw.Extensions/Reflection/TypeExtensions.cs:47-49`  **Low**

**Applied.** `<remarks>` added to both `IsImplementOf` overloads documenting that an exact type match returns false. Behaviour deliberately unchanged.

```csharp
if (type == matching) return false;
```

`typeof(Foo).IsImplementOf<Foo>()` is `false`. Defensible as "strictly implements, not is" — but the name reads the other way, and `Type.IsAssignableTo` (the BCL's closest equivalent) returns `true` for identity. It is used by `TypeExtractor.IsInstanceOf`, so `Extract().IsInstanceOf<Foo>()` silently excludes `Foo` itself.

Whether that is right depends on the intended semantics. It should at minimum be documented on the method, since a caller cannot guess it.

---

## ✅ C18 — `MaxFileSizeInMb` overflows `int` above 2,147 MB {#c18}

`Services/DKNet.Svc.BlobStorage.Abstractions/IBlobService.cs:214-219`  **Low**

**Applied.** `long` arithmetic (`* 1_000_000L`). `IncludedExtensions` retyped to `IReadOnlyList<string>` (source-breaking for lazy assignment). "Mb" left as 1,000,000 — see bucket 1.

```csharp
var limitLength = _options.MaxFileSizeInMb * 1000000; //Convert Mb to Byte
if (fileLength > limitLength) throw new FileLoadException("File size is invalid.");
```

Both operands are `int`, so the multiplication is `int` arithmetic. `MaxFileSizeInMb = 2148` produces `2_148_000_000`, which exceeds `int.MaxValue` and wraps negative — so `fileLength > limitLength` is true for **every** file and all uploads are rejected. Anything from 2,148 MB up inverts the check.

`fileLength` is `int` too (`item.Data.ToMemory().Length`), which caps the comparison at ~2 GB regardless.

**Fix.** `long limitLength = _options.MaxFileSizeInMb * 1_000_000L;` and compare against `item.Data.ToMemory().Length` widened to `long`. Also worth deciding whether "Mb" means 1,000,000 (as coded) or 1,048,576 — the property name suggests the latter to most readers, and the comment says the former.

Separately, two lines up, `_options.IncludedExtensions` is typed `IEnumerable<string>` and enumerated twice per upload (`.Any()` then `.Any(e => ...)`). Declaring it `string[]` or `IReadOnlyList<string>` removes the double enumeration and the question of whether a lazy query could be assigned to it.
