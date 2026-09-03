# Breaking changes for downstream consumers

Everything below is derived from the actual public API diff between `334f314` (the last `dev` tip before this work merged) and the current `dev`, not from the review reports alone. Where a report finding did **not** produce a consumer-visible change, it is not listed here.

Severity key:

| Marker | Meaning |
|---|---|
| 🔴 | **Compile break.** Consumer code stops building until it is changed. |
| 🟠 | **Runtime behaviour change.** Compiles unchanged, behaves differently. |
| 🟡 | **Binary break only.** Source-compatible; matters if you ship pre-compiled assemblies against DKNet without recompiling. |
| 🟢 | **Additive.** Listed only so you know it exists. |

This is a **major-version** set. Do not ship it as a patch or minor bump.

---

## 1. Packages removed entirely

### 🔴 `DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions`

Both packages are gone, along with every type in them: `IRepository<T>`, `IReadRepository<T>`, `IWriteRepository<T>`, `Repository<T>`, `ReadRepository<T>`, `WriteRepository<T>`, `IRepositoryFactory`, `RepositoryFactory<T>`, `RepoExtensions`, and the `SetupRepository` DI extensions `AddGenericRepositories<TDbContext>()` / `AddRepoFactory<TDbContext>()`.

**What to do.** Move to `DKNet.EfCore.Specifications` and `AddSpecRepo<TDbContext>()`. The migration guide is `docs/EfCore/Migrating-Repos-To-Specifications.md`.

This is the largest single item here. If you inject `IRepository<T>` anywhere, budget for it before starting the upgrade — nothing else in this list is comparable in size.

---

## 2. Types removed

### 🔴 `IAesEncryption` / `AesEncryption` / `AddAesEncryption()`

Removed rather than deprecated, deliberately. The implementation was AES-CBC with a **fixed IV embedded in the key**, so identical plaintexts produced identical ciphertext — a real confidentiality defect, not a style issue. It carried `[Obsolete]` before removal.

**What to do.** Use `IAesGcmEncryption` / `AddAesGcmEncryption()`. Note the ciphertext formats are not interchangeable: anything encrypted with the old type must be decrypted with the old code and re-encrypted. Plan a data migration if you persisted its output.

### 🔴 `Base65StringExtensions`

A misspelled pure-forwarding duplicate. Use `Base64StringExtensions`. Method names and behaviour are identical.

### 🔴 `AsyncEnumerableExtensions.ToListAsync` (namespace `System.Collections.Generic`)

Removed because .NET 10 ships `System.Linq.AsyncEnumerable.ToListAsync`, and having both visible made every no-argument `ToListAsync()` call on an `IAsyncEnumerable<T>` an ambiguous-reference compile error for consumers with implicit usings.

**What to do.** The BCL version is a drop-in improvement — it takes a `CancellationToken` and returns `ValueTask<List<T>>` instead of `Task<IList<T>>`. If you `await` the result you likely need no change beyond the removed `using`.

> This only affects calls on `IAsyncEnumerable<T>`. EF Core's `IQueryable` `ToListAsync` is a different extension and is untouched.

### 🔴 `EncryptionKeyProvider` (abstract class)

An abstract class whose only member re-declared the interface's only member, with no shared implementation.

**What to do.** Implement `IEncryptionKeyProvider` directly. If you had `class MyProvider : EncryptionKeyProvider`, change it to `class MyProvider : IEncryptionKeyProvider` and drop the `override` on `GetKey`.

---

## 3. Members removed or changed

### 🔴 `IShaHashing` and `IHmacHashing` no longer implement `IDisposable`

Both declared `IDisposable` with an empty `Dispose()`. The interface comment claimed it existed to "release cached algorithms"; there were none — every method is internally static, calling `SHA256.HashData` / `HMACSHA256.HashData`.

**What breaks.** `using (var h = _shaHashing) { … }` and any explicit `.Dispose()` call stop compiling.

**What to do.** Delete the `using` / `Dispose()` call. There was never anything to release.

### 🔴 `IRepositorySpec.DeleteRange<TEntity>(IEnumerable<TEntity>)`

Removed along with its `RepositorySpec` implementation. It was already `[Obsolete]`.

**What to do.** Use `BulkDeleteAsync`.

### 🔴 `AddIdempotentKey()` — the parameterless overload

Removed. It was the only public way to select the built-in `IdempotencyDistributedCacheStore`, **which is not atomic under concurrency** — two simultaneous requests with the same key could both proceed.

**What to do.** Use a store package's extension — `AddIdempotencyWithMsSqlStore`, `AddIdempotencyWithNpgsqlStore`, `AddIdempotencyWithRedisStore` — or implement `IIdempotencyKeyStore` and pass it to `AddIdempotentKey<TStore>()`.

Since the store types are internal, `IdempotencyDistributedCacheStore` now has no public entry point at all. That is intentional: it was never safe for the job.

### 🔴 `GetEumInfo()` → `GetEnumInfo()`, `GetEumInfos<T>()` → `GetEnumInfos<T>()`

Typo fix in `DKNet.Fw.Extensions`. Rename at the call site; behaviour is unchanged.

### 🟡 Specification repository list methods now return `Task<List<T>>` instead of `Task<IList<T>>`

Affects `SpecRepoExtensions.ToListAsync` (both overloads), `ModelSpecRepoExtensions.ToListAsync`, and both `ToKeysetPageAsync` overloads.

`List<T>` satisfies `IList<T>`, so ordinary calls — `await …ToListAsync()`, assignment to `IList<T>`, `foreach` — keep compiling. It is a **binary** break: recompile rather than drop new assemblies alongside old ones. Source breaks are possible but narrow (explicit overload resolution on the return type, or reflection over the signature).

---

## 4. Dependency-injection lifetime changes

### 🟠 `S3BlobService` and `AzureStorageBlobService` are now registered `AddSingleton`, previously `AddScoped`

The underlying SDK clients (`AmazonS3Client`, `BlobContainerClient`) are thread-safe and designed to be long-lived; the previous per-scope registration rebuilt the client and re-ran a `ListBuckets` probe on every scope. One-time initialisation is now a race-safe `Lazy<Task<T>>` that runs exactly once.

**What breaks.** If you subclassed either service and injected a **scoped** dependency into it (a `DbContext`, an `IHttpContextAccessor`-derived scoped service, a per-request tenant accessor), that dependency is now captured once for the life of the process. This is the classic captive-dependency bug and the container will not warn you about it.

**What to do.** Audit any subclass or decorator of these two services for scoped constructor dependencies. `LocalBlobService` is unchanged and remains `AddScoped`.

### 🟠 `IPdfGenerator` is now `TryAddSingleton` via a factory, and `PdfGenerator` implements `IAsyncDisposable`

Previously a Chromium process was launched per document. One browser is now reused, relaunched automatically if it dies, and disposed at host shutdown.

The registration deliberately uses a **factory** (`TryAddSingleton<IPdfGenerator>(_ => new PdfGenerator(options))`) rather than a pre-built instance, because the built-in container never disposes instances it did not create — a pre-built registration would leak the browser process.

**What to do.** If you registered `IPdfGenerator` yourself with a pre-built instance, switch to a factory or you will leak a Chromium process. If you resolve and dispose it manually, stop — the container owns it now.

---

## 5. Behaviour changes that still compile

These are the dangerous ones. Nothing fails to build; something behaves differently at runtime.

### 🟠 Azure blob `GetAsync` returns `null` for a missing blob instead of throwing

Collapsing three round trips into one `DownloadContentAsync` wrapped in `catch (RequestFailedException e) when (e.Status == 404)` also fixed a latent bug — a missing blob previously threw rather than returning `null`, contradicting the nullable return type.

**What to do.** If you wrapped `GetAsync` in `try`/`catch (RequestFailedException)` to detect a missing blob, that catch is now dead code and your null path is live. Check the null path actually works.

### 🟠 Navigation auto-add no longer scans entities in `Unchanged` state

`AddNewEntitiesFromNavigations` previously walked entries in `Detached`, `Modified` **and `Unchanged`** state. `Unchanged` is dropped.

**What breaks.** A brand-new child added to the collection navigation of a parent that is itself `Unchanged`, where the child is not otherwise reachable, is no longer auto-added.

**Worth knowing:** a spike test added during this work shows EF Core's own relationship fixup already inserts a new child added to a tracked parent's collection, without this mechanism at all. So in the common case nothing changes. The narrow case is a parent that EF considers untouched *and* a child EF cannot reach through fixup.

**What to do.** If you rely on adding children to untouched parents and letting DKNet find them, verify with an integration test against a real database.

### 🟠 Idempotency: schema migration moved off the request path

`IdempotencyMigrationHostedService` now runs once at startup, registered by the MsSql and Npgsql setup extensions. Previously the first request after startup could run `GetPendingMigrations` and `Migrate` while holding a process-wide semaphore, blocking every concurrent request behind it.

**What to do.** If your host skips or reorders hosted services, the per-request guard remains as a documented fallback — but you should fix the host. If your deployment applies migrations out-of-band and denies the app DDL rights, confirm the hosted service tolerates that.

### 🟠 Idempotency: misconfiguration now throws at host start, not at registration

`IdempotencyOptions` moved to the options pattern (`AddOptions` + `Configure` + declarative `Validate` + `ValidateOnStart`), replacing a hand-rolled validator. You now get `OptionsValidationException` at host start instead of `ArgumentException` at registration.

Additionally, `IdempotencyKeyPattern` compiles its `Regex` in the setter, so an **invalid pattern throws when the option is set** rather than on first request.

**What to do.** If you have a test asserting `ArgumentException` from `AddIdempotentKey(...)`, it needs updating. The first-call-wins registration guard is deliberately kept.

### 🟠 Idempotency relational store: completion uses `ExecuteUpdateAsync`

`MarkKeyAsProcessedAsync` completes via a single `ExecuteUpdateAsync` instead of a tracked `SELECT` + `SaveChanges` (2 round trips → 1). Reservation likewise inserts first instead of `SELECT`-then-`INSERT`.

**What to do.** `ExecuteUpdateAsync` bypasses the change tracker, so **`SaveChanges` interceptors, audit hooks and domain-event dispatch no longer fire for the idempotency row**. If you attached auditing to the idempotency `DbContext` and expected rows there, you will stop seeing them. The single-winner guarantee is unchanged — it comes from the unique index, and is still covered by concurrency tests.

### 🟠 Generated SQL text changes for dynamic filters and keyset pagination

Filter values and keyset cursors are now passed as **parameters** rather than inlined literals. This is the point of the change — every distinct value previously produced distinct SQL and a distinct server-side plan.

**What to do.** If you assert on `ToQueryString()` output, pin query plans, use plan guides, or match SQL text in monitoring rules, those will need updating. Results are unchanged. Keyset predicates also gained a redundant leading bound (`key1 >= cursor1 AND (…)`) to help the index seek — same rows, different plan shape.

### 🟠 `TypeExtractor` filter methods return a new instance instead of mutating

`FilterBy` used to append to a shared list and return `this`, so a "fluent" API silently mutated the source:

```csharp
var extractor = assemblies.Extract().Classes();
var abstracts  = extractor.Abstract();     // used to mutate extractor
var concretes  = extractor.NotAbstract();  // used to become Abstract() AND NotAbstract() — empty
```

Both branches are now independent and correct.

**What to do.** Nothing, unless you depended on the mutation — in which case you were almost certainly getting wrong results already.

### 🟠 `LazyResult.Errors` / `Successes` / `IsFailed` are memoised

Computed once on first read and cached. `Reasons` is `init`-only and nothing in the codebase mutates it after construction, so results are unchanged. Flagged only because a consumer mutating `Reasons` through reflection would no longer see updates.

### 🟠 `GlobalQueryFilter.IgnorableFilterKeys` returns a cached snapshot

The type (`IReadOnlyCollection<string>`) is unchanged; the array is now rebuilt when `Apply` registers a key rather than re-materialised on every read. The set only changes during model building, so consumers reading it after startup see identical values.

---

## 6. Explicitly *not* breaking

Called out because they touched sensitive code and you may otherwise assume the worst.

- 🟢 **AES-GCM column encryption wire format is unchanged.** The buffer handling was rewritten to use spans, and the `iv‖tag‖ciphertext` layout is pinned byte-identical by a known-answer test. Existing encrypted columns decrypt normally.
- 🟢 **`AesGcmEncryption`'s stored format is unchanged.** The double-base64 packaging was left alone precisely because collapsing it would make existing ciphertexts undecryptable.
- 🟢 **Source generator output is byte-identical.** `DtoGenerator`'s pipeline change was verified by building three real consumers with `EmitCompilerGeneratedFiles=true` before and after and diffing the generated trees — zero differences. `CrudGenerator` is unchanged.
- 🟢 **`IBlobService` gained `OpenReadAsync(BlobRequest)` and `SaveAsync(BlobStreamData)`** as default interface methods with virtual base implementations. Existing implementors keep compiling without change. The `BinaryData` overloads are **not** deprecated and continue to work.
- 🟢 **Hash outputs are unchanged.** The hex/base64 allocation work in `DKNet.Svc.Encryption` produces identical strings; round-trip and known-answer tests cover it.

---

## Suggested upgrade order

1. **`DKNet.EfCore.Repos` → Specifications first**, on its own, before touching anything else. It is the only item large enough to hide other failures behind it.
2. Fix the compile breaks in §2 and §3 — all mechanical, all caught by the compiler.
3. Audit the DI lifetime changes in §4. The compiler will not help you here; a captive scoped dependency in a blob service subclass fails at runtime, under load, intermittently.
4. Re-run integration tests against a real database for §5 — particularly the navigation auto-add and idempotency items. These are the changes a unit test suite will miss.

---

## Verification status

Full solution builds with zero warnings; 2,138 tests pass across 25 projects.

Two gaps you should know about before publishing packages:

- **`AspCore.Idempotency.MsSqlStore.Tests` has not been run.** SQL Server has no working container on the ARM64 development host. The reservation logic is shared with `NpgsqlStore` and is verified there against a real Postgres database; only the SQL-Server-specific unique-violation detection (error 2601/2627 vs SQLState 23505) is unexercised, and this work did not modify it. Re-validate on the x64 CI runner before release.
- **14 of 185 `Svc.PdfGenerators.Tests` fail locally** on ARM64 — PuppeteerSharp fetches an x64 Chromium that the host cannot exec. The same suite passed 185/185 on the x64 runner, including the browser-reuse path.
