# Simplification findings — custom code the platform already provides

Each item names what to delete and what replaces it. Line counts are approximate net deletions.

Effort key: **S** = under an hour, **M** = half a day to a day, **L** = multi-day / breaking.

**Status key:** ✅ fixed and verified in the working tree · ✖ cancelled (recommendation withdrawn — the reason is recorded in the item) · ❓ awaiting your decision.

---

## Replaceable by the BCL

### ✅ S2 — `AsyncEnumerableExtensions.ToListAsync` is now in the .NET 10 BCL {#s2}

`Core/DKNet.Fw.Extensions/Collections/AsyncEnumerableExtensions.cs:25` — 15 lines

**Applied.** File deleted. Grep classified all ~300 `ToListAsync` hits: nearly all are EF Core's `IQueryable` extension. The only real callers of DKNet's were its own tests (deleted) and `EfCore.Repos.Tests`, which was removed with the retired Repos packages. `Core/DKNet.Fw.Extensions/README.md` updated.

.NET 10 ships `System.Linq.AsyncEnumerable`, which includes `ToListAsync<T>(this IAsyncEnumerable<T>, CancellationToken)` returning `ValueTask<List<T>>`.

DKNet's version deliberately declares itself in namespace `System.Collections.Generic` (an ambient namespace, per the folder-per-concern exception in `CLAUDE.md`) so it resolves without an import. That is now a liability rather than a convenience: **any file that has both `System.Collections.Generic` and `System.Linq` in scope — which is every file with implicit usings — sees two applicable extension methods for a no-argument `ToListAsync()` call.** They differ in return type (`Task<IList<T>>` vs `ValueTask<List<T>>`), so this is an ambiguity error waiting for a consumer to hit, not a silent overload resolution.

**Action.** Delete it. The BCL version is also better: it takes a `CancellationToken` (DKNet's does not) and returns `List<T>`, which satisfies `IList<T>` for existing callers. Check `src/` and the docs for callers first — a `grep` for `.ToListAsync()` on non-`IQueryable` receivers.

**Effort:** S. **Risk:** it is a public API removal, so it belongs in a major version or behind an `[Obsolete]` cycle.

---

### ✅ S4 — response value extraction has an interface for it {#s4}

`AspNet/DKNet.AspCore.Idempotency/Filtering/IdempotencyEndpointFilter.cs:119` — 1 line, removes a reflection dependency

**Applied.** `result is IValueHttpResult v ? v.Value ?? result : result` — the `?? result` preserves the old fallback when `Value` is null. Reflection using removed. 2 end-to-end tests added.

```csharp
// now
var resultValue = result is null ? null : result.GetPropertyValue("Value") ?? result;
// instead
var resultValue = result is IValueHttpResult v ? v.Value : result;
```

`IValueHttpResult` is ASP.NET Core's declared contract for "a result that carries a value", implemented by `Ok<T>`, `Json<T>`, `Created<T>`, and the rest of `TypedResults`. String-based reflection is guessing at what the interface states. This also removes `DKNet.Fw.Extensions.Reflection` as a dependency of the filter. (Also listed as [P24](performance.md#p24).)

**Effort:** S. **Risk:** none.

---

### ✅ S5 — `Base64StringExtensions.IsBase64String` has a one-line stdlib form {#s5}

`Services/DKNet.Svc.Encryption/Base65Extensions.cs:70-77` — 8 lines → 1

**Applied.** `Base64.IsValid`. Equivalence with `Convert.TryFromBase64String` checked empirically across every existing test case, including embedded whitespace — no divergence.

```csharp
public static bool IsBase64String(string s) =>
    !string.IsNullOrWhiteSpace(s) && System.Buffers.Text.Base64.IsValid(s);
```

`Base64.IsValid` (.NET 8) validates without allocating the scratch buffer the current implementation heap-allocates. The file already imports `System.Buffers.Text` for `Base64Url`.

**Effort:** S. **Risk:** none.

---

### ✅ S6 — hex-lowercase has a dedicated API {#s6}

`Services/DKNet.Svc.Encryption/Hashing/ShaHashing.cs:86-87`, `AspNet/DKNet.AspCore.Idempotency.RedisStore/Store/IdempotencyRedisStore.cs:183`, `AspNet/DKNet.AspCore.Idempotency/Filtering/IdempotencyKeyScopeResolver.cs:36`

**Applied.** `Convert.ToHexStringLower` at all three sites; the no-op `.ToUpperInvariant()` deleted from `HmacHashing`; `Verify` now compares raw hash bytes instead of decoding its own output. Redis cache-key output proven byte-identical by a test computing the old formula independently.

`Convert.ToHexString(bytes).ToLowerInvariant()` → `Convert.ToHexStringLower(bytes)` (.NET 9). Three sites, one allocation saved each.

And `Services/DKNet.Svc.Encryption/Hashing/HmacHashing.cs:96` calls `.ToUpperInvariant()` on `Convert.ToHexString`'s output, which is **already uppercase** — delete the call outright.

**Effort:** S. **Risk:** none.

---

### ✅ S7 — `File.WriteAllBytesAsync` takes a `ReadOnlyMemory<byte>` {#s7}

`Services/DKNet.Svc.BlobStorage.Local/LocalBlobService.cs:260`

**Applied.** `blob.Data.ToMemory()`. The undisposed `FileStream` in `GetAsync` was fixed in the same pass (`await using`).

`blob.Data.ToArray()` → `blob.Data.ToMemory()`. The .NET 9 overload avoids the full payload copy. (Also listed as [P12](performance.md#p12).)

**Effort:** S. **Risk:** none.

---

### ✅ S8 — relative paths and path checks have stdlib forms {#s8}

`Services/DKNet.Svc.BlobStorage.Local/LocalBlobService.cs:190`

**Applied.** Path helpers, char overloads, the `CA1867` suppression, and the Turkish-I `ToLowerInvariant` all applied, with unused `using`s cleaned up. **The three blob DI guards were deliberately NOT converted** — they match on `ImplementationType`, so an app registering both Local and S3 as `IBlobService` works today, whereas `TryAddScoped` checks only `ServiceType` and would silently drop the second.

```csharp
private string GetRelativePath(string fullPath) =>
    fullPath.Replace(_rootFolder, string.Empty, ...);
```

`string.Replace` removes **every** occurrence of the root folder name anywhere in the path, not just the leading one — so a file at `/store/data/store/x.txt` under root `/store` yields `/data/x.txt`. `Path.GetRelativePath(_rootFolder, fullPath)` is the stdlib answer and is correct by construction.

Same file, `LocalDirectorySetup.IsDirectory` uses `File.GetAttributes` in a `try/catch` over two exception types as control flow; `Directory.Exists(path)` is the one-liner.

`Services/DKNet.Svc.BlobStorage.Abstractions/IBlobService.cs:GetBlobLocation` builds a `StringBuilder` to conditionally prefix a `'/'`:

```csharp
protected virtual string GetBlobLocation(BlobRequest item) =>
    item.Name.StartsWith('/') ? item.Name : "/" + item.Name;
```

`Services/DKNet.Svc.BlobStorage.AzureStorage/AzureStorageExtensions.cs` — `EnsureTrailingSlash`/`RemoveHeadingSlash` use the `string` overloads of `EndsWith`/`StartsWith` with `OrdinalIgnoreCase`, which is why the file carries `[SuppressMessage("Performance", "CA1867:Use char overload")]`. Using `EndsWith('/')`/`StartsWith('/')` makes the suppression unnecessary — the analyzer is right.

**Effort:** S. **Risk:** low; `GetRelativePath` is a behaviour *fix* (see [correctness-notes.md](correctness-notes.md#c9)).

---

### ✅ S9 — `DateTime.LastDayOfMonth` reconstructs the date instead of shifting it {#s9}

`Core/DKNet.Fw.Extensions/Primitives/DateTimeExtensions.cs:33-46` — 13 lines → 1

**Applied.** See [C5](correctness-notes.md#c5).

```csharp
public static DateTime LastDayOfMonth(this DateTime date) =>
    date.AddDays(DateTime.DaysInMonth(date.Year, date.Month) - date.Day);
```

`AddDays` preserves the time-of-day and — importantly — the original `DateTimeKind`. The current implementation enumerates all seven date/time components into a new `DateTime` and hardcodes `DateTimeKind.Local`, silently converting a UTC input into a Local one (see [correctness-notes.md](correctness-notes.md#c5)).

**Effort:** S. **Risk:** it changes the returned `Kind`, which is the point. Check the tests, which may assert the current (wrong) behaviour.

---

### ✖ ~~S14~~ — `IsNumber` reimplements number parsing — **RECOMMENDATION WITHDRAWN, LEAVE AS-IS** {#s14}

`Core/DKNet.Fw.Extensions/Primitives/StringExtensions.cs:56-61`

**Investigated and rejected.** I proposed replacing the four-pass hand-rolled check with `decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out _)`. The existing tests are the specification here, and they rule it out.

`IsNumberTests` asserts **both** `"123,456.789"` (US: `,` groups, `.` decimal) **and** `"123.456,789"` (European: `.` groups, `,` decimal) are numbers. `TryParse` under `InvariantCulture` accepts the first and **rejects the second** — it permits only one canonical separator ordering. The current implementation deliberately accepts both by checking "at most one `.`" and "no `,,`" rather than validating separator order, so the dual-locale tolerance is intentional behaviour, not an accident of the checks.

Every other case matched `TryParse` exactly (single and multi-comma grouping, leading-only `-`, letters, multi-dot rejection, empty/whitespace/null) — verified empirically against each test string, not reasoned about.

So the four passes buy something real. If the allocation ever matters, the fix is a single-pass span scan preserving the dual-locale rule, not `TryParse`.

---

## Replaceable by EF Core

### ✅ S1 — the legacy two-phase ordering path is dead weight {#s1}

**Applied.** Foreign `ISpecification<TEntity>` implementations are not supported. `OrderByQueries`/`OrderByDescendingQueries` removed from the interface *and* from `Specification<TEntity>` (the `_orderByQueries`/`_orderByDescendingQueries` backing lists too); `_orderByClauses` is now the single model. The legacy two-phase branch in `ApplySpecs` and the foreign-spec synthesis branch in the copy constructor are deleted; `EnsureSpecHasOrdering` tests one collection. `OrderByClauses`/`SkipCount`/`TakeCount`/`IsReadOnly` stay `internal` — no production consumer outside the package reads them, so widening the public surface to save one `is Specification<TEntity>` cast in `ApplySpecs` wasn't warranted; that single cast remains. Four tests in `OrderingWindowTrackingTests.cs` encoded the removed legacy two-phase SQL directly (`ApplySpecs_ForeignSpecification_With*Ordering_*`) and were deleted; the copy-constructor foreign-spec test was rewritten to assert the (now-empty) result instead of synthesis. One cross-project fallout: `AspCore.Extensions.Tests/Endpoints/EntityListSpecificationTests.cs` asserted directly on the removed properties — rewritten to assert on generated SQL (`ToQueryString()`) and materialized row order instead, per the repo's established SQL-verification pattern.

`EfCore/DKNet.EfCore.Specifications/Extensions/SpecificationExtensions.cs:63-108` and `Definitions/Specification.cs:68-90,147-160` — ~110 lines

`Specification<TEntity>` maintains **three** ordering collections for the same information:

- `_orderByQueries` (ascending only),
- `_orderByDescendingQueries` (descending only),
- `_orderByClauses` (the declared sequence, mixed directions).

`AddOrderBy` and `AddOrderByDescending` write to two of them each. `ApplySpecs` then has two complete implementations: the correct declared-sequence path for `Specification<TEntity>`, and a 45-line "legacy two-phase" fallback that applies all ascending clauses first and all descending clauses second — reachable only by a **foreign** `ISpecification<TEntity>` implementation, i.e. one written by a consumer that does not derive from `Specification<TEntity>`.

The copy constructor carries a third branch (`Specification.cs:80-90`) to synthesise a declared sequence for such foreign specifications.

**Action.** Decide whether foreign `ISpecification` implementations are supported. If not — and the abstract base class plus `internal`-only `OrderByClauses` suggests they are not — remove `OrderByQueries`/`OrderByDescendingQueries` from the interface, keep `_orderByClauses` as the single model, and delete the legacy branch, the copy-constructor branch, and the dual writes. `EnsureSpecHasOrdering` (`:118`) then tests one collection instead of two.

This is the largest single deletion available in the repo and it collapses two ordering semantics into one, which is the real win — the two paths produce **different SQL** for the same specification depending on the implementing type.

**Effort:** S to delete; the decision is the work. **Risk:** it is a public interface change (breaking), so it belongs in a major version.

---

### ✅ S3 — EF Core provides the provider checks {#s3}

`EfCore/DKNet.EfCore.Extensions/Extensions/EfCoreExtensions.cs:118,126` and `EfCore/DKNet.EfCore.Relational.Helpers/DbContextHelpers.cs:88`

**Applied.** Consolidated to the single implementation in `EfCoreExtensions`; the private copy in `DbContextHelpers` deleted. Both packages stay provider-reference-free as decided — no SqlServer/Npgsql package added. `Relational.Helpers` gained a `ProjectReference` to `Extensions` (it was already calling `context.IsSqlServer()` and coincidentally resolving its own private copy). Public `IsSqlServer()`/`IsNpgsql()` signatures unchanged.

```csharp
public bool IsSqlServer() =>
    string.Equals(context.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", ...);
```

`Microsoft.EntityFrameworkCore.SqlServer` ships `DatabaseFacade.IsSqlServer()` and Npgsql ships `IsNpgsql()`; both are the supported, string-free forms. The hand-rolled version is duplicated in two projects with a third private copy in `DbContextHelpers`.

Caveat worth stating: the official extensions live in the *provider* packages, so using them means `DKNet.EfCore.Extensions` would take a provider reference it currently avoids. If keeping the abstraction provider-free is deliberate, the fix is to deduplicate the three copies into one internal helper rather than to adopt the provider APIs. Either way the current state — three implementations of the same string comparison — should not persist.

**Effort:** S. **Risk:** low; the package-reference question needs a decision.

---

### ✅ S15 — entity key values are available without reflection {#s15}

`EfCore/DKNet.EfCore.Extensions/Extensions/EfCoreExtensions.cs:47` and `:80`

**Applied.** See [C10](correctness-notes.md#c10).

```csharp
return primaryKey.Properties.ToDictionary(
    p => p.Name,
    p => p.PropertyInfo!.GetValue(entityEntry.Entity),   // reflection
    StringComparer.OrdinalIgnoreCase);
```

EF Core exposes the same values through its compiled accessors: `entityEntry.CurrentValues[p]` (or `entry.Property(p.Name).CurrentValue`). Besides being faster, it is **correct for shadow properties and backing-field-only keys**, where `PropertyInfo` is `null` and the `!` produces a `NullReferenceException`.

`GetPrimaryKeyValues(object entity)` at `:80` has the same shape with an extra `type.GetProperty(key)` per key. `NavigationExtensions` in the same project already does this the right way (`GetCurrentKeyValues` uses `entry.CurrentValues[p]`, `:126`), so the correct pattern is already in the codebase one file over.

**Effort:** S. **Risk:** low, and it fixes a crash case.

---

### ✅ S16 — sequence value retrieval hand-rolls connection management {#s16}

`EfCore/DKNet.EfCore.Extensions/Extensions/EfCoreExtensions.cs:160-186`

```csharp
await context.Database.OpenConnectionAsync();
await using var result = await command.ExecuteReaderAsync();
object? rs = null;
if (await result.ReadAsync()) rs = await result.GetFieldValueAsync<object>(0);
await context.Database.CloseConnectionAsync();
```

Three issues, all solved by existing APIs:

- `ExecuteReaderAsync` + `ReadAsync` + `GetFieldValueAsync` to read one scalar — `ExecuteScalarAsync()` is the single call for this.
- ~~Explicit `Open`/`CloseConnection` will **close a connection the caller still needs** inside an ambient transaction.~~ **This diagnosis was wrong.** EF Core's `RelationalConnection` reference-counts open/close via `_openedCount`, so a nested Open/Close pair does not physically close a connection an outer scope still holds. The real defect, found while fixing it, is that there is **no `try`/`finally`** around the pair: any exception in between (a bad sequence name, say) leaks the open reference count permanently, so that `DbContext`'s connection may never truly close again.
- No `CancellationToken` is accepted or forwarded on any of the four `await`s.

**Applied.** `ExecuteScalarAsync` replaces the reader triple; Open/Close is now wrapped in `try`/`finally`; a `CancellationToken` is accepted and forwarded on all three methods; `NextSeqValueWithFormat` switched to `InvariantCulture` to match its own determinism claim. The `CA2100` suppression stays, now justified in place — the method returns a boxed `object?`, so `SqlQuery<T>` would need a compile-time scalar `T` it cannot have, and T-SQL cannot parameterise a sequence identifier in `NEXT VALUE FOR`. Covered by `NextSeqValue_WithinExplicitTransaction_DoesNotCloseAmbientConnection`.

**Effort:** M. **Risk:** medium — it is a behaviour change around transactions, which is exactly why it is worth fixing.

---

### ✅ S17 — `TableExistsAsync` uses an exception as a boolean {#s17}

`EfCore/DKNet.EfCore.Relational.Helpers/DbContextHelpers.cs:100-110`

**Applied.** Replaced with a real `INFORMATION_SCHEMA.TABLES` count via `Database.SqlQuery<int>`, schema and table name passed as parameters. ANSI-standard, so no provider branching. The pre-existing "entity not in model throws" behaviour is preserved explicitly (a live test asserts it). `CreateTableAsync` behaviour deliberately unchanged — its doc now states plainly that it creates every table in the model and will not backfill tables added later; building per-table DDL generation was judged out of proportion for a bootstrap helper whose only callers are tests.

```csharp
try { await dbContext.Set<TEntity>().AnyAsync(ct); return true; }
catch (DbException) { return false; }
```

This issues a real query against the table to discover whether it exists, and treats **any** `DbException` as "does not exist" — so a permissions failure, a timeout, or a broken connection all report `false`. It also produces a logged error and a first-chance exception per call.

`IRelationalDatabaseCreator.HasTablesAsync()`, or a query against `INFORMATION_SCHEMA.TABLES` via `Database.SqlQuery<int>`, answers the question without the exception and without the false negatives.

Related, in `CreateTableAsync` just above: `databaseCreator.CreateTablesAsync()` creates **all** tables in the model, not the one requested, so it throws if any of them already exists. The method's name promises something it cannot deliver for a partially migrated database.

**Effort:** M. **Risk:** medium; check who calls it (it appears to be a test/bootstrap helper).

---

### ✅ S18 — DI registration guards reimplement `TryAdd` {#s18}

Twelve sites, e.g. `EfCore/DKNet.EfCore.Specifications/SpecSetup.cs:20`, `Services/DKNet.Svc.Encryption/EncryptionSetup.cs:14,18`, `Services/DKNet.Svc.BlobStorage.Local/LocalDirectorySetup.cs`, `Services/DKNet.Svc.PdfGenerators/PdfGeneratorSetup.cs`, `Services/DKNet.Svc.Transformation/TransformSetup.cs`, `EfCore/DKNet.EfCore.Encryption/EfCoreEncryptionSetup.cs`, `AspNet/DKNet.AspCore.Idempotency/IdempotencySetup.cs:22`

**Applied.** **Partially applied.** Converted: the two hashing registrations in `EncryptionSetup`, `PdfGeneratorSetup`, `SpecSetup`, `EfCoreEncryptionSetup`. **Deliberately not converted:** the three blob guards (see S8), `TransformSetup` (its guard blocks the config *delegate* from running, not just its result — `TryAdd` would newly invoke a second caller's `optionFactory` for side effects), and `IdempotencySetup` (its guard also gates option validation, so `TryAdd` would change when validation throws).

The pattern is consistently:

```csharp
if (!services.Any(s => s.ServiceType == typeof(IFoo))) services.AddScoped<IFoo, Foo>();
```

`Microsoft.Extensions.DependencyInjection.Extensions.TryAddScoped<IFoo, Foo>()` is the same check-then-add, already written, already tested, and it makes the intent explicit. `DKNet.Fw.Extensions.ServiceCollectionRegistrationExtensions.IsRegistered<T>()` — which several of these call — becomes unnecessary for this purpose.

Two of these guards have a behavioural wrinkle worth deciding on rather than preserving by accident:

- `SpecSetup.AddSpecRepo<TDbContext>` guards on `IRepositorySpec` being registered at all, so a **second** call with a *different* `TDbContext` silently does nothing. If multi-context support is intended, the guard needs to be per-context (keyed, as the hook and audit registrations already do).
- `IdempotencySetup.AddIdempotentKey<TStore>` guards on `IIdempotencyKeyStore` and therefore also discards the second call's `IdempotencyOptions` — `AspCore.Idempotency.Tests/IdempotencySetupTests.cs:23-24` documents this as intended ("first wins"), so it is deliberate; a comment saying so at the call site would help.

**Effort:** S. **Risk:** low, provided the two guards above are treated as decisions rather than mechanical replacements.

---

### ✅ S19 — `AddSingleton(Options.Create(...))` instead of the options pattern {#s19}

`EfCore/DKNet.EfCore.AuditLogs/EfCoreAuditLogSetup.cs:64`, `AspNet/DKNet.AspCore.Idempotency/IdempotencySetup.cs:31`, `Services/DKNet.Svc.Transformation/TransformSetup.cs`

**Applied.** `IdempotencySetup`'s 45-line hand-rolled validator replaced by ten declarative `.Validate()` calls plus `ValidateOnStart()`, so misconfiguration still fails fast at startup. The first-wins registration guard was **deliberately kept** — `Configure<T>` accumulates delegates, which would have silently changed documented behaviour — and that is now stated in `<remarks>`.

Registering a pre-built `IOptions<T>` instance means repeated registrations stack (last one wins on resolve), configuration reloading cannot work, and `IOptionsMonitor`/`IValidateOptions` are unavailable. `services.Configure<T>(...)` plus `AddOptions<T>().Validate(...)` is the framework's mechanism, and it would replace `IdempotencySetup.ValidateOptions` (a 45-line hand-rolled validator, `:60-105`) with declarative `Validate` calls or `[Required]`/`[Range]` annotations plus `ValidateDataAnnotations()`.

**Effort:** M. **Risk:** low-medium; the eager-throw timing changes (options validation runs on first resolve, not at registration), which some consumers may rely on. `ValidateOnStart()` restores eager failure.

---

## Replaceable by an already-referenced package

### ✅ S10 — the blob abstraction is buffer-only; ~~keyset helpers duplicate their own dependency~~ {#s10}

**Applied 2026-09-03 (additively — nothing breaks).** `IBlobService` gained `OpenReadAsync(BlobRequest)` and `SaveAsync(BlobStreamData)` as default interface methods with virtual base implementations, so existing implementors keep compiling; all three providers override them with their SDK's streaming API instead of buffering through `BinaryData`. `SizeLimitedStream` enforces the configured size ceiling on non-seekable streams, which buffering previously gave for free. The `BinaryData` overloads are deliberately **not** deprecated — retiring them is the breaking half and belongs in the major-version list ([S20](#s20)).

**Blob (L, breaking).** `IBlobService` exchanges `BinaryData` for both upload and download (`Services/DKNet.Svc.BlobStorage.Abstractions/BlobData.cs`), so every implementation buffers the entire object in memory: `BinaryData.FromStreamAsync` on S3 and local reads, `DownloadContentAsync` on Azure, and `blob.Data.ToStream()` on S3 upload. All three SDKs underneath expose streaming APIs. Adding `Task<Stream> OpenReadAsync(BlobRequest, ...)` and `Task<string> SaveAsync(string name, Stream content, ...)` alongside the existing methods would let large-object callers avoid the buffer without breaking anyone; the `BinaryData` methods become thin wrappers.

**Keyset pagination — ✅ defects fixed, but the "duplicates its own dependency" framing is WITHDRAWN.** I claimed the ~130 hand-built lines in `KeysetQueryExtensions.cs` could be deleted in favour of `MR.EntityFrameworkCore.KeysetPagination`, already referenced in the same file. Decompiling the library (1.6.0) shows it cannot serve these signatures:

- `KeysetPaginate(..., object? reference)` resolves cursor values by reflecting over `reference`'s properties and matching by **name**. It needs a real CLR property called e.g. `Id`; there is no bare-`TKey` overload anywhere in its public surface. Bridging `(keySelector, cursor)` to it would mean emitting a type per `(entity, property, key type)` combination — more code than the 40 lines it would replace.
- An existing test, `AfterKeyset_SingleKey_ComputedKeySelector_TranslatesToPredicateInsteadOfThrowing`, requires these methods to work with computed selectors such as `p => p.Name.Length`. Name-based reference matching cannot express that at all, so the library could not replace the tested contract even ignoring the cursor problem.
- **My claim that the library "emits row-value comparison where the provider supports it" was false.** Its `KeysetFilterPredicateStrategy.Default` is hardcoded to `KeysetFilterPredicateStrategyMethod1` — OR-of-ANDs, the same shape as the hand-rolled composite predicate, with no provider detection and no `ROW(...)` SQL.

What was genuinely wrong, and is now fixed:

- **✅ The cursor was inlined as a literal.** `Expression.Constant(cursor, typeof(TKey))` produced `WHERE Id > 42`, so every distinct cursor yielded distinct SQL and a distinct server-side plan — plan-cache pollution on precisely the high-cardinality pagination path. Now routed through a `CursorBox<TValue>` holder read via `Expression.Property`, the closure shape EF Core's parameter extraction recognises. Verified with `ToQueryString()`: `WHERE "p"."Id" > @Value`, and two different cursor values now produce byte-identical command text.
- **✅ The composite predicate lacked an index-seek aid.** Not the row-value rewrite I proposed (nothing emits that), but the one real advantage the library does have: a redundant leading bound, `key1 >= cursor1 AND (key1 > cursor1 OR (key1 = cursor1 AND key2 > cursor2))`. Ported, mirrored for the backward direction, and the XML docs that claimed row-value comparison were corrected.

Six tests added (parameter-not-literal both directions, identical command text across two cursors for single and composite keys, leading-bound presence both directions). Suite 412 → 418.

**Effort:** L for blob, M for keyset. **Risk:** blob is a public interface addition (safe) with an eventual deprecation (breaking); keyset is a public API removal.

---

### ❓ S11 — `System.Linq.Dynamic.Core` is only needed for the raw-string overloads {#s11}

**Deferred by decision (2026-09-02).** This item is not independent work — it *is* [P4](performance.md#p4). Implementing it means replacing the typed `(property, Ops, value)` path with hand-built expression trees, which touches the dynamic-filter hot path every list endpoint runs. The maintainer chose to keep it in the performance review batch rather than pull it forward, so it stays open here and moves with P4. Revisit after the performance pass.

Covered as [P4](performance.md#p4). Worth restating as a simplification: replacing the typed `(property, Ops, value)` path with hand-built expression trees deletes `BuildClause` (35 lines of string templating), `DangerousExpressionPatterns` + `ValidateExpression` (30 lines of substring blacklist), and the `ParseException` handling in three places — and the package already contains the expression-building idiom it needs, in `Specification.AddOrderBy` and `KeysetQueryExtensions`.

---

## Dead or duplicated code

### ✅ S12 — `IDisposable` implementations that dispose nothing {#s12}

- `Core/DKNet.RandomCreator/StringCreator.cs:15` — empty `Dispose()`; `RandomCreators.NewString`/`NewChars` wrap it in `using` for no effect. The whole `StringCreator` class is a stateful wrapper around what is naturally a static method (see [P34](performance.md#p34)) — the two public methods plus one private generator would fit in `RandomCreators` directly, removing a class and a `using`.

**Applied.** **Docs only.** `StringCreator`'s empty `Dispose` and the pointless `using` in `RandomCreators` are gone (internal, so non-breaking), and the false "release cached algorithms" comment on `IShaHashing` is deleted. The public `IDisposable` on `IShaHashing`/`IHmacHashing` and the `EncryptionKeyProvider` abstract class remain — breaking, deferred to the next major. ❓ **Also deferred (breaking), needs your call:** `ShaHashing.VerifySha256`/`VerifySha512` keep an `ignoreCase` parameter that cannot change the result — it is read and passed to `ComputeHash` as `!ignoreCase`, but only selects upper- vs lower-case hex for an intermediate string that `Convert.FromHexString` decodes case-insensitively before the `FixedTimeEquals` comparison. Same defect as the `HmacHashing` parameter already removed, harder to spot because the value is passed somewhere rather than literally unused.
- `Services/DKNet.Svc.Encryption/Hashing/ShaHashing.cs` and `HmacHashing.cs` — both declare `IShaHashing : IDisposable` / `IHmacHashing : IDisposable` with empty `Dispose()`. The interface even carries the comment "now disposable so we can release cached algorithms" — there are no cached algorithms; every method is `static` internally, calling `SHA256.HashData`/`HMACSHA256.HashData`. These are **stateless static helpers wrapped in DI interfaces**. Making them static classes removes two interfaces, two implementations, two DI registrations, and the misleading `IDisposable`. If DI injectability must be preserved for testability, keep the interfaces but drop `IDisposable`.
- `EfCore/DKNet.EfCore.Encryption/Encryption/EncryptionKeyProvider.cs` — an abstract class whose only member re-declares the interface's only member, with no shared implementation. Consumers can implement `IEncryptionKeyProvider` directly.

**Effort:** S. **Risk:** public API removals; batch them into a major version.

---

### ✅ S13 — `TypeExtractor`'s fluent API mutates shared state {#s13}

`Core/DKNet.Fw.Extensions/TypeExtractors/TypeExtractor.cs:81-85`

**Applied.** `FilterBy` now returns a new `TypeExtractor` with a copied predicate list instead of mutating and returning `this`. Predicate type kept as `Expression<Func<Type,bool>>` (P17 is separate). Test `FilterBy_TwoBranchesFromSameExtractor_AreIndependent` reproduces the `Abstract()`/`NotAbstract()` trap and asserts the branches are disjoint and sum to the base count.

```csharp
private TypeExtractor FilterBy(Expression<Func<Type, bool>>? predicate)
{
    if (predicate != null) _predicates.Add(predicate);
    return this;   // same instance
}
```

Every builder method returns `this` after appending to a shared `List`. So:

```csharp
var extractor = assemblies.Extract().Classes();
var abstracts = extractor.Abstract();      // mutates extractor
var concretes = extractor.NotAbstract();   // now Abstract() AND NotAbstract() — empty
```

Fluent APIs that look immutable but are not are a 3am problem. Either return a new `TypeExtractor` with a copied predicate list (a few extra allocations at startup, correct semantics), or rename the type to make its builder nature obvious. Combine with the `Func` change in [P17](performance.md#p17), since both touch the same three lines.

The 27-member `ITypeExtractor` interface is also worth a look: `Abstract`/`NotAbstract`, `Classes`/`NotClass`, `Enums`/`NotEnum`, `Generic`/`NotGeneric`, `Interfaces`/`NotInterface`, `Nested`/`NotNested`, `Publics`/`NotPublic` are seven negation pairs, each one line wrapping a single property test — all reachable through the public `Where(...)`. Whether that convenience earns 14 interface members is a taste call, but it is the kind of surface that is cheaper to remove now than after more consumers depend on it.

**Effort:** S for the mutation fix. **Risk:** the mutation fix changes observable behaviour for any code that (accidentally) relies on it.

---

### ✅ S20 — obsolete duplicates awaiting removal {#s20}

Already correctly marked `[Obsolete]`; listing them so the next major version has the set in one place:

| Type / member | Location | Note |
|---|---|---|
| ✅ `Base65StringExtensions` | `Services/DKNet.Svc.Encryption/Base65Extensions.cs:88` | Misspelled duplicate of `Base64StringExtensions`; pure forwarding |
| ✅ `IAesEncryption` / `AesEncryption` | `Services/DKNet.Svc.Encryption/Ciphers/AesEncryption.cs` | AES-CBC with a **fixed IV embedded in the key**, so identical plaintexts produce identical ciphertext. Security footgun; deleting is better than deprecating |
| ✅ `AddAesEncryption` | `Services/DKNet.Svc.Encryption/EncryptionSetup.cs:33` | Registers the above |
| ✖ ~~`RequestBase`~~ | `SlimBus/DKNet.SlimBus.Extensions/RequestBase.cs` | **Recommendation withdrawn — keep it.** Not a duplicate awaiting removal: its `[Obsolete]` message says it is deliberately "retained for existing consumers", and `AspCore.Extensions.Tests` has a test (`ConfigureGroup_ManualByUserStamping_RequestBaseBasedCommand_StampedByUserReachesHandler`) that exists specifically to guarantee the pre-DRK-565 manual-stamping pattern still works, with a `#pragma warning disable CS0618` to use it. Deleting ~15 lines is not worth breaking a tested migration path |
| ✅ `IRepositorySpec.DeleteRange` | `EfCore/DKNet.EfCore.Specifications/Repositories/IRepositorySpec.cs:60` | **Applied.** Superseded by `BulkDeleteAsync`; interface member and `RepositorySpec` implementation removed. No test called it directly (the misleadingly-named `DeleteRange_WithMultipleEntities_ShouldDeleteAll` test actually exercised `BulkDeleteAsync`, left as-is). |
| ✅ `AddIdempotentKey()` (no type arg) | `AspNet/DKNet.AspCore.Idempotency/IdempotencySetup.cs:41` | Defaults to the non-atomic `IdempotencyDistributedCacheStore` |
| ✅ `DKNet.EfCore.Repos`, `DKNet.EfCore.Repos.Abstractions` | `EfCore/` | Documented as retired in `CLAUDE.md`; the projects are still in the solution |

**Effort:** S. **Risk:** breaking by definition — this is a major-version list.

---

## Smaller consolidations

### ✅ S21 — three async wrappers that only exist to widen a return type {#s21}

**Applied.** Both `ToListAsync` overloads in `SpecRepoExtensions.cs`, the one in `ModelSpecRepoExtensions.cs`, and — same pattern, same fix — both `ToKeysetPageAsync` overloads in `SpecRepoExtensions.cs` now return `Task<List<T>>` directly with no `async`/`await`; the `pageSize` guard on the keyset overloads throws synchronously before the `Task` is returned. `KeysetQueryExtensions.ToKeysetPageAsync` (a different return shape, `Task<KeysetPage<TEntity>>`) was left untouched.

`EfCore/DKNet.EfCore.Specifications/Extensions/SpecRepoExtensions.cs:95,108` and `ModelSpecRepoExtensions.cs:68`

```csharp
public async Task<IList<TEntity>> ToListAsync<TEntity>(...) =>
    await repo.Query(specification).ToListAsync(cancellationToken);
```

The `async`/`await` exists only to convert `Task<List<T>>` to `Task<IList<T>>`, at the cost of a compiler-generated state machine per call. Returning `Task<List<TEntity>>` directly drops the `async` keyword and the state machine — `List<T>` implements `IList<T>`, so existing callers are unaffected at the call site (it is a source-compatible, binary-breaking change).

**Effort:** S. **Risk:** binary-breaking; source-compatible.

---

### ✅ S22 — the four `DynamicAnd`/`DynamicOr` string overloads are written four times {#s22}

`EfCore/DKNet.EfCore.Specifications/Dynamics/DynamicPredicateExtensions.cs:114-140` and `:158-184`

**Applied.** Extracted `ParseDynamicExpression<T>`; all four public bodies are now one-line forwarders. Signatures and XML docs unchanged. The pre-existing `ToQueryString()` assertions in `DynamicPredicateExpressionOverloadTests` serve as the equivalence proof — 415 tests green.

The `ExpressionStarter<T>` extension block and the `Expression<Func<T,bool>>` extension block each contain the same two `(string expression, params object?[] values)` bodies — four identical implementations of two methods. A shared private helper taking the parsed lambda would leave four two-line forwarders.

**Effort:** S. **Risk:** none.

---

### ✅ S23 — `EfCoreExceptionHandler` writes to `Console` and matches on an English message {#s23}

`EfCore/DKNet.EfCore.Extensions/Extensions/EfCoreExceptionHandler.cs:61,63`

**Applied.** `ILogger<EfCoreExceptionHandler>?` optional ctor param (parameterless construction in `EfSaveChangesExtension` still works); `Console.WriteLine` gone; the English-string match replaced by `exception.Entries.Count == 0`. **Note this widens when a retry happens** — see bucket 3. The `ConsoleUsageArchitectureTests` allow-list is now empty; its self-check test was removed.

```csharp
Console.WriteLine($"EfCoreExceptionHandler:HandlingAsync - {exception.Message}");
if (!exception.Message.Contains("but actually affected 0 row(s)", StringComparison.OrdinalIgnoreCase))
    return EfConcurrencyResolution.RethrowException;
```

Two problems in three lines. `Console.WriteLine` bypasses `ILogger` entirely — no structured logging, no level filtering, no sink routing, and it writes on a hot failure path. And the concurrency decision hinges on **matching EF Core's English exception text**, which is a localisable, version-specific string; when it changes, every concurrency conflict silently becomes a rethrow.

`DbUpdateConcurrencyException` already carries the structured answer: `exception.Entries` is non-empty precisely for the affected-zero-rows case this string is probing for. Inject `ILogger<EfCoreExceptionHandler>` and branch on `exception.Entries.Count`.

**Effort:** S. **Risk:** low — but it is a behaviour change on the concurrency path, so it needs a test.

---

## Net effect

| Category | Approx. lines removed |
|---|---|
| Legacy ordering path + redundant backing lists (S1) | 110 |
| Offset-paging enumerator (P1) | 56 |
| Dynamic LINQ string building + blacklist (S11) | 65 |
| Hand-rolled keyset predicates (S10) | 130 |
| Empty-`IDisposable` wrappers and dead abstractions (S12) | 60 |
| Obsolete duplicates (S20) | 250 |
| Duplicated overloads, DI guards, stdlib one-liners (S3–S9, S18, S21, S22) | 150 |
| `FindWritableProperty`, reflection key readers (P7, S15) | 40 |
| Generator pipeline models (P14) | 60 |
| **Total** | **~920** |

Roughly 3.6% of the non-test source, and — more to the point — it removes two competing ordering semantics, two competing AES-GCM package formats, three copies of `IsSqlServer`, and one reflection-based property setter that cannot see shadow properties.
