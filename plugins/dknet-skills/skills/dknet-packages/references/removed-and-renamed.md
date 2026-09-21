# Removed and renamed APIs

Every API that a consumer of an older DKNet version, or an AI agent guessing from a similar
library, might reach for and not find. "Since version" is uniformly "Unreleased" (no tagged release
contains these removals yet) except the one historical row. "Verified absent" means: grepped across
the current DKNet source tree and confirmed to return zero matches, not just copied from a changelog.

| Old | Since version | Replacement | Verified absent from source |
|---|---|---|---|
| `DKNet.EfCore.Repos`, `DKNet.EfCore.Repos.Abstractions` packages | Unreleased | `DKNet.EfCore.Specifications` + `services.AddSpecRepo<TDbContext>()` | yes — no project or path matching `*Repos*` exists anywhere in the source tree |
| `IRepository<T>`, `IReadRepository<T>`, `IWriteRepository<T>`, `IRepositoryFactory`, `SetupRepository`, `RepoExtensions`, `Repository<T>`/`ReadRepository<T>`/`WriteRepository<T>`/`RepositoryFactory<TDbContext>` | Unreleased | `IRepositorySpec` (registered scoped) / `IRepositorySpecFactory` (registered singleton) via `AddSpecRepo<TDbContext>()` | yes |
| `IRepositorySpec.DeleteRange<TEntity>` | Unreleased | `repo.BulkDeleteAsync<TEntity>(predicate, ct)` — a server-side `ExecuteDeleteAsync`, no load | yes |
| `ISpecification<TEntity>.OrderByQueries` / `.OrderByDescendingQueries` (and on `Specification<TEntity>`) | Unreleased | Single declared-sequence ordering via `AddOrderBy`/`AddOrderByDescending` on `Specification<TEntity>`. A hand-written `ISpecification<TEntity>` that does not derive from `Specification<TEntity>` is unsupported. | yes |
| `AsyncEnumerableExtensions.ToListAsync(this IAsyncEnumerable<T>)` (`DKNet.Fw.Extensions`) | Unreleased | .NET 10's own `System.Linq.AsyncEnumerable.ToListAsync` (takes a `CancellationToken`) | yes |
| `EnumExtensions.GetEumInfos<T>()` / `GetEumInfo()` (typo) | Unreleased | `GetEnumInfos<T>()` / `GetEnumInfo()` | yes |
| `Base65StringExtensions` (misspelled duplicate of `Base64StringExtensions`) | Unreleased | `Base64StringExtensions` | yes |
| `IAesEncryption`, `AesEncryption`, `AddAesEncryption(keyString)` (AES-CBC, fixed IV embedded in the key) | Unreleased, security-motivated | `IAesGcmEncryption` / `AddAesGcmEncryption(base64Key)` — no ciphertext-conversion path from the old format | yes, with a caveat below |
| `HmacHashing.VerifySha256`/`VerifySha512`'s `ignoreCase` parameter | Unreleased | Drop the argument — hex decoding is already case-insensitive | yes — note `IShaHashing.VerifySha256`/`VerifySha512` (a different type) keeps its own unrelated `ignoreCase` parameter; only the `HmacHashing` pair lost it |
| `IShaHashing`/`IHmacHashing` extending `IDisposable` | Unreleased | Drop any `using`/`.Dispose()` around an injected instance — both were always stateless | yes |
| `EncryptionKeyProvider` abstract class (`DKNet.EfCore.Encryption`) | Unreleased | Implement `IEncryptionKeyProvider` directly | yes |
| `ToProblemDetails(this IResultBase, HttpStatusCode)`; `ToProblemDetails(this ModelStateDictionary)` | Unreleased | `.Response()`/`.Response<T>()` | yes |
| `ToProblemDetails(this IResultBase, ErrorResponseOptions?)` — public visibility | Unreleased | Now `internal`. Call `.Response()`/`.Response<T>()` instead | yes |
| `Response(IResultBase, ErrorResponseOptions?, bool isCreated=false)` / `Response<T>(IResult<T>, ErrorResponseOptions?, bool isCreated=false)` | Unreleased | `Response(this IResultBase, bool isCreated=false)` / `Response<T>(this IResult<T>, bool isCreated=false)` — resolves options from DI itself | yes |
| `EndpointRegistrationOptions.EnableRequestValidation`, `.SystemAccountName` | Unreleased | `EndpointRegistrationOptions.ConfigureGroup` callback (`Action<RouteGroupBuilder, IEndpointConfig>?`) | yes |
| `DKNet.SlimBus.Extensions.RequestBase` (including `ByUser`) | Unreleased | Declare the acting-user property on the request itself with an `IContextualSource` attribute (e.g. `[FromClaim(...)]`) plus `AddContextualRequestPopulation()` | yes — asserted by a reflection-based test in the DKNet repo itself |
| `AddDataOwnerProvider<TDbContext, TProvider>()` with an unconstrained `TDbContext` | Unreleased | `where TDbContext : DbContext, IDataOwnerDbContext` | yes |
| `BlobServiceOptions.IncludedExtensions` as `IEnumerable<string>` | Unreleased | `IReadOnlyList<string>` — materialize a lazy query with `.ToList()` first | yes |
| `IdempotencyDistributedCacheStore` (the old `IDistributedCache`-backed store) | Unreleased | `AddIdempotentKey()` (in-process store) or a named store package. It was `internal`, so nothing that compiled before stops compiling. | yes — the type itself no longer exists, not merely made inaccessible |
| `ContextualPopulationOptions` / `SystemAccountFallback` configure delegate | Unreleased | Register your own `IContextualValueResolver` before `AddContextualRequestPopulation()` — that method now takes no configure delegate at all | yes |
| Legacy N-Layer / generic-repository era architecture | 2024.12.0 | Full DDD/Onion rewrite — see https://github.com/baoduy/DKNet/blob/dev/docs/Architecture.md | historical; not independently re-checked against current source |

## Never existed (not a removal)

- **`AggregateRoot`** — no version of DKNet ever shipped a public `AggregateRoot` type. An aggregate
  root is an `Entity<TKey>`/`Entity`/`AuditedEntity<TKey>`/`AuditedEntity` you choose to treat as a
  consistency boundary. A few `*.Tests` projects, plus the internal `EfCore.DtoGenerator.TestEntities`
  fixture library, declare their own private `AggregateRoot` test-fixture classes for unrelated
  purposes — none of those projects is packed or published, so those types are not, and never were,
  part of any public package surface, and a consuming application cannot reference them.
- **`DKNet.EfCore.DtoEntities`** — never a published package name. The one place this name is
  documented is as the *old* name of what is now `EfCore.DtoGenerator.TestEntities`, an internal
  test-fixture project inside the DKNet repository, never published to NuGet. No external consumer
  migration is needed for it.
- **`AddDKNet()` / `DKNetOptions`** — no such aggregator call or options type has ever existed. Each
  package registers itself; nothing needs cross-package ordering beyond what this skill's `Rules`
  and `How to` sections give.

## Caveat: `AddAesEncryption`

DKNet's own `Migration-Guide.md` and `CHANGELOG.md` disagree with each other: `Migration-Guide.md`
says `IAesEncryption`/`AesEncryption`/`AddAesEncryption` are deleted outright, with no compatibility
overload; `CHANGELOG.md`'s `[Unreleased]` section instead describes an `[Obsolete]`
`AddAesEncryption(keyString)` kept "for migration only." Grepping the current
`DKNet.Svc.Encryption` source for `IAesEncryption`, `AesEncryption`, and `AddAesEncryption` returns
zero matches — only `AddRsaEncryption` and `AddAesGcmEncryption` exist. Source sides with
`Migration-Guide.md`: there is no migration shim. Re-encrypt data under `AddAesGcmEncryption`
directly rather than looking for an obsolete bridge method.

## Related

- `references/package-catalog.md` — the current, correct API surface these rows replace.
- `references/namespace-imports.md` — where the *current* replacement types and methods live.
- Full migration guide: https://github.com/baoduy/DKNet/blob/dev/docs/Migration-Guide.md
- Changelog: https://github.com/baoduy/DKNet/blob/dev/docs/CHANGELOG.md
- Repos-to-Specifications call-site mapping: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/Migrating-Repos-To-Specifications.md
