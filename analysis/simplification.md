# Simplification findings — custom code the platform already provides

Each item names what to delete and what replaces it. Line counts are approximate net deletions.

Effort key: **S** = under an hour, **M** = half a day to a day, **L** = multi-day / breaking.

**Status key:** ✅ = applied in the working tree; read its **Applied** note, since three items were only partially applied and the reasons matter. Unmarked = still open.

---

## Replaceable by the BCL

### S2 — `AsyncEnumerableExtensions.ToListAsync` is now in the .NET 10 BCL {#s2}

`Core/DKNet.Fw.Extensions/Collections/AsyncEnumerableExtensions.cs:25` — 15 lines

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

### S14 — `IsNumber` reimplements number parsing {#s14}

`Core/DKNet.Fw.Extensions/Primitives/StringExtensions.cs:56-61` — 6 lines → 1

Four passes plus a LINQ chain approximating "is this a number", with hand-rolled rules about `.`, `,`, and `-` placement. `decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out _)` is one call and handles grouping, sign placement, and whitespace correctly.

The current rules also accept strings the parser rejects (`"1,,2"` is guarded but `"1.2.3"` passes the `<= 1` dot count only by accident of ordering, and `"1,2,3"` passes entirely). Verify against the existing tests before switching — the semantics genuinely differ.

**Effort:** S. **Risk:** medium (behaviour change); needs a test review.

---

## Replaceable by EF Core

### S1 — the legacy two-phase ordering path is dead weight {#s1}

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

### S3 — EF Core provides the provider checks {#s3}

`EfCore/DKNet.EfCore.Extensions/Extensions/EfCoreExtensions.cs:118,126` and `EfCore/DKNet.EfCore.Relational.Helpers/DbContextHelpers.cs:88`

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

### S16 — sequence value retrieval hand-rolls connection management {#s16}

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
- Explicit `Open`/`CloseConnection` around it will **close a connection the caller still needs** if this runs inside an ambient transaction or an already-open connection scope. EF Core's connection lifetime management (or `Database.SqlQuery<T>($"...")`, which composes with the context's connection and transaction) handles this correctly.
- No `CancellationToken` is accepted or forwarded on any of the four `await`s.

**Effort:** M. **Risk:** medium — it is a behaviour change around transactions, which is exactly why it is worth fixing.

---

### S17 — `TableExistsAsync` uses an exception as a boolean {#s17}

`EfCore/DKNet.EfCore.Relational.Helpers/DbContextHelpers.cs:100-110`

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

### S19 — `AddSingleton(Options.Create(...))` instead of the options pattern {#s19}

`EfCore/DKNet.EfCore.AuditLogs/EfCoreAuditLogSetup.cs:64`, `AspNet/DKNet.AspCore.Idempotency/IdempotencySetup.cs:31`, `Services/DKNet.Svc.Transformation/TransformSetup.cs`

Registering a pre-built `IOptions<T>` instance means repeated registrations stack (last one wins on resolve), configuration reloading cannot work, and `IOptionsMonitor`/`IValidateOptions` are unavailable. `services.Configure<T>(...)` plus `AddOptions<T>().Validate(...)` is the framework's mechanism, and it would replace `IdempotencySetup.ValidateOptions` (a 45-line hand-rolled validator, `:60-105`) with declarative `Validate` calls or `[Required]`/`[Range]` annotations plus `ValidateDataAnnotations()`.

**Effort:** M. **Risk:** low-medium; the eager-throw timing changes (options validation runs on first resolve, not at registration), which some consumers may rely on. `ValidateOnStart()` restores eager failure.

---

## Replaceable by an already-referenced package

### S10 — the blob abstraction is buffer-only; keyset helpers duplicate their own dependency {#s10}

**Blob (L, breaking).** `IBlobService` exchanges `BinaryData` for both upload and download (`Services/DKNet.Svc.BlobStorage.Abstractions/BlobData.cs`), so every implementation buffers the entire object in memory: `BinaryData.FromStreamAsync` on S3 and local reads, `DownloadContentAsync` on Azure, and `blob.Data.ToStream()` on S3 upload. All three SDKs underneath expose streaming APIs. Adding `Task<Stream> OpenReadAsync(BlobRequest, ...)` and `Task<string> SaveAsync(string name, Stream content, ...)` alongside the existing methods would let large-object callers avoid the buffer without breaking anyone; the `BinaryData` methods become thin wrappers.

**Keyset pagination (M).** `EfCore/DKNet.EfCore.Specifications/Extensions/KeysetQueryExtensions.cs` contains ~130 lines of hand-built `AfterKeyset`/`BeforeKeyset` expression trees for one- and two-key cursors — in a file that *also* references `MR.EntityFrameworkCore.KeysetPagination` (used by `ToKeysetPageAsync` in the same class). The library handles arbitrary key counts, mixed sort directions, and `HasPrevious`/`HasNext`, none of which the hand-rolled pair does.

Two concrete gaps in the hand-rolled version:

- `Expression.Constant(cursor, typeof(TKey))` (`:249`, `:283`) embeds the cursor as a **literal** in the query, so EF Core emits `WHERE Id > 42` rather than a parameter. Every distinct cursor value produces a distinct SQL string and a distinct server-side plan — plan-cache pollution on a pagination endpoint, which is precisely the high-cardinality case.
- The composite predicate uses the `k1 > c1 OR (k1 = c1 AND k2 > c2)` form (`:296-303`). Row-value comparison (`(k1, k2) > (c1, c2)`), which the library emits where the provider supports it, is materially more index-friendly.

**Effort:** L for blob, M for keyset. **Risk:** blob is a public interface addition (safe) with an eventual deprecation (breaking); keyset is a public API removal.

---

### S11 — `System.Linq.Dynamic.Core` is only needed for the raw-string overloads {#s11}

Covered as [P4](performance.md#p4). Worth restating as a simplification: replacing the typed `(property, Ops, value)` path with hand-built expression trees deletes `BuildClause` (35 lines of string templating), `DangerousExpressionPatterns` + `ValidateExpression` (30 lines of substring blacklist), and the `ParseException` handling in three places — and the package already contains the expression-building idiom it needs, in `Specification.AddOrderBy` and `KeysetQueryExtensions`.

---

## Dead or duplicated code

### ✅ S12 — `IDisposable` implementations that dispose nothing {#s12}

- `Core/DKNet.RandomCreator/StringCreator.cs:15` — empty `Dispose()`; `RandomCreators.NewString`/`NewChars` wrap it in `using` for no effect. The whole `StringCreator` class is a stateful wrapper around what is naturally a static method (see [P34](performance.md#p34)) — the two public methods plus one private generator would fit in `RandomCreators` directly, removing a class and a `using`.

**Applied.** **Docs only.** `StringCreator`'s empty `Dispose` and the pointless `using` in `RandomCreators` are gone (internal, so non-breaking), and the false "release cached algorithms" comment on `IShaHashing` is deleted. The public `IDisposable` on `IShaHashing`/`IHmacHashing` and the `EncryptionKeyProvider` abstract class remain — breaking, deferred to the next major.
- `Services/DKNet.Svc.Encryption/Hashing/ShaHashing.cs` and `HmacHashing.cs` — both declare `IShaHashing : IDisposable` / `IHmacHashing : IDisposable` with empty `Dispose()`. The interface even carries the comment "now disposable so we can release cached algorithms" — there are no cached algorithms; every method is `static` internally, calling `SHA256.HashData`/`HMACSHA256.HashData`. These are **stateless static helpers wrapped in DI interfaces**. Making them static classes removes two interfaces, two implementations, two DI registrations, and the misleading `IDisposable`. If DI injectability must be preserved for testability, keep the interfaces but drop `IDisposable`.
- `EfCore/DKNet.EfCore.Encryption/Encryption/EncryptionKeyProvider.cs` — an abstract class whose only member re-declares the interface's only member, with no shared implementation. Consumers can implement `IEncryptionKeyProvider` directly.

**Effort:** S. **Risk:** public API removals; batch them into a major version.

---

### S13 — `TypeExtractor`'s fluent API mutates shared state {#s13}

`Core/DKNet.Fw.Extensions/TypeExtractors/TypeExtractor.cs:81-85`

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

### S20 — obsolete duplicates awaiting removal {#s20}

Already correctly marked `[Obsolete]`; listing them so the next major version has the set in one place:

| Type / member | Location | Note |
|---|---|---|
| `Base65StringExtensions` | `Services/DKNet.Svc.Encryption/Base65Extensions.cs:88` | Misspelled duplicate of `Base64StringExtensions`; pure forwarding |
| `IAesEncryption` / `AesEncryption` | `Services/DKNet.Svc.Encryption/Ciphers/AesEncryption.cs` | AES-CBC with a **fixed IV embedded in the key**, so identical plaintexts produce identical ciphertext. Security footgun; deleting is better than deprecating |
| `AddAesEncryption` | `Services/DKNet.Svc.Encryption/EncryptionSetup.cs:33` | Registers the above |
| `RequestBase` | `SlimBus/DKNet.SlimBus.Extensions/RequestBase.cs` | Superseded by `[FromClaim]` + `AddContextualRequestPopulation` |
| `IRepositorySpec.DeleteRange` | `EfCore/DKNet.EfCore.Specifications/Repositories/IRepositorySpec.cs:60` | Superseded by `BulkDeleteAsync` |
| `AddIdempotentKey()` (no type arg) | `AspNet/DKNet.AspCore.Idempotency/IdempotencySetup.cs:41` | Defaults to the non-atomic `IdempotencyDistributedCacheStore` |
| `DKNet.EfCore.Repos`, `DKNet.EfCore.Repos.Abstractions` | `EfCore/` | Documented as retired in `CLAUDE.md`; the projects are still in the solution |

**Effort:** S. **Risk:** breaking by definition — this is a major-version list.

---

## Smaller consolidations

### S21 — three async wrappers that only exist to widen a return type {#s21}

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
