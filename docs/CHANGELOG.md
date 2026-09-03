# DKNet Framework Changelog

All notable changes to the DKNet Framework will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `[RaisesEvent]` convention forms now accept `Exclude`/`Include` named arguments to shape the automatically
  composed payload record — mutually exclusive, resolved against the entity's properties at build time
  (`DKRAISEVT009`/`DKRAISEVT010`/`DKRAISEVT011`), and never affecting the composed event name.
- Consolidated repository-wide documentation in `docs/` folder
- Comprehensive getting started guide
- Configuration and setup documentation
- Examples and recipes section
- API reference documentation
- Migration guide for breaking changes
- FAQ and best practices section
- `Base64StringExtensions`' five methods (`DKNet.Svc.Encryption`) are now `this string` extension methods;
  existing static-style call sites still compile.
- Non-generic `AddIdempotentKey(Action<IdempotencyOptions>? config = null)` (`DKNet.AspCore.Idempotency`) enables
  idempotency with no store named and no infrastructure at all — no database, cache, Redis, or connection string.
  It registers a new in-process store that reserves each key atomically within the process, adds no options
  (it reuses `Expiration` and `InFlightReservationTimeout`) and no package reference, and holds no key past its
  `Expiration`. Keys are process-local, lost on restart, and not shared between instances, so it is for local
  development and unit tests only; the app logs one startup warning saying so while it is the store serving
  requests. An explicitly named store replaces it whichever order the two registrations run in, so existing
  registrations keep behaving exactly as before.

### Changed
- Automatically composed `[RaisesEvent]` convention-form payloads now honour the project-wide
  `DtoGeneratorExclusions` MSBuild list (the same list `[GenerateDto]` DTOs already respect), so composed
  event payloads narrow in any project that configures it — unless overridden by a non-empty `Include`.
- Improved documentation organization and navigation
- Enhanced main README.md to be more concise and point to docs/
- **Breaking (binary only):** `EfCoreEncryptionSetup.AddEfCoreEncryption<T>()` now takes and returns
  `IServiceCollection` instead of the concrete `ServiceCollection`, so it is callable on `builder.Services`.
  Source-compatible for existing callers; pre-compiled assemblies referencing the old signature must be recompiled.
- **Breaking:** `AddEncryptionServices()` no longer registers `IRsaEncryption` — it previously resolved to a new,
  throwaway random key pair on every DI resolution, so keys never survived across resolutions. Callers that need
  RSA must opt in explicitly with `services.AddRsaEncryption(privateKeyBase64)`, which registers `IRsaEncryption`
  as a singleton built from a caller-supplied key.
- **Breaking:** `AddEncryptionServices()` no longer registers `IAesGcmEncryption` or the obsolete `IAesEncryption`
  either — both were transients over a constructor that generates a fresh random key per instance. Every injection
  therefore got a different key, the key was persisted nowhere, and any ciphertext written through one resolution
  became permanently unreadable, silently. Callers must now opt in with
  `services.AddAesGcmEncryption(base64Key)` — a singleton `IAesGcmEncryption` over a plain Base64 128/192/256-bit
  key — or, for migration only, `services.AddAesEncryption(keyString)`, a singleton `IAesEncryption` over the
  combined Base64 `key:iv` value that `AesEncryption.Key` returns (that method is itself `[Obsolete]`). Both throw `ArgumentException` on a null, empty,
  or whitespace key. `AddEncryptionServices()` now registers only the keyless `IShaHashing` and `IHmacHashing`
  transients; `new AesGcmEncryption()` still generates an ephemeral key and stays valid for data that never
  outlives the process.
- **Breaking:** `EndpointRegistrationOptions.EnableRequestValidation` and `EndpointRegistrationOptions.SystemAccountName`
  have been removed from `DKNet.AspCore.Extensions`, and `UseEndpointConfigs()` no longer stamps `RequestBase.ByUser`
  or applies FluentValidation auto-validation on its own. A consumer that had set either setting gets a compile
  failure; one relying on defaults loses automatic validation silently, and — because the stamping filter is gone —
  a caller-supplied `ByUser` — regardless of binding source (query, `[AsParameters]`, or JSON body) — now reaches
  the handler unchanged, so a host that does not re-add stamping via `ConfigureGroup` must treat `ByUser` as
  caller-influenced. Supply both
  through the new `EndpointRegistrationOptions.ConfigureGroup` callback instead. Versioning is now a switch
  (`EnableVersioning`, default `true`) and `IEndpointConfig.Version` is optional (defaults to `1`).
- **Breaking:** `AddDataOwnerProvider<TDbContext, TProvider>()` in `DKNet.EfCore.DataAuthorization` now constrains
  `TDbContext` to `DbContext, IDataOwnerDbContext`. This is source-breaking: a consumer whose `DbContext` does not
  implement `IDataOwnerDbContext` no longer compiles. Previously it compiled and silently lost row-level ownership
  isolation at runtime in Release builds. Migration: implement `IDataOwnerDbContext` (supply `AccessibleKeys`;
  override `IsUnrestrictedAccess` only for admin/system contexts) on the `DbContext` type you register — see the
  [Migration Guide](Migration-Guide.md#upgrading-dknetefcoredataauthorization-idataownerdbcontext-is-now-required).
- **Breaking (binary only, source-compatible):** `SpecRepoExtensions.ToListAsync`, `ModelSpecRepoExtensions.ToListAsync`
  and both `SpecRepoExtensions.ToKeysetPageAsync` overloads (`DKNet.EfCore.Specifications`) now return
  `Task<List<T>>` instead of `Task<IList<T>>`. Existing call sites compile unchanged; a precompiled assembly
  referencing the old return type must be recompiled.
- **Breaking (source):** `BlobServiceOptions.IncludedExtensions` (`DKNet.Svc.BlobStorage.Abstractions`) is now
  `IReadOnlyList<string>` instead of `IEnumerable<string>`. A caller assigning a lazy query to it no longer compiles.
- `EfCoreExceptionHandler` (`DKNet.EfCore.Extensions`) gained an optional `ILogger<EfCoreExceptionHandler>?`
  constructor parameter; parameterless construction still works.
- `NextSeqValue`/`NextSeqValueWithFormat` (`DKNet.EfCore.Extensions`) gained an optional `CancellationToken`.

### Removed
- **Breaking:** `DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions` packages, and the `Mapster.EFCore`
  central package entry they alone used. Use `DKNet.EfCore.Specifications` + `AddSpecRepo<TDbContext>()` instead —
  see the [Migration Guide](Migration-Guide.md#entity-framework-core-migration) and
  [`Migrating-Repos-To-Specifications.md`](EfCore/Migrating-Repos-To-Specifications.md).
- **Breaking:** `AsyncEnumerableExtensions.ToListAsync(this IAsyncEnumerable<T>)` (`DKNet.Fw.Extensions`). Use .NET
  10's `System.Linq.AsyncEnumerable.ToListAsync`, which also accepts a `CancellationToken`.
- **Breaking:** `Base65StringExtensions` (`DKNet.Svc.Encryption`), the misspelled duplicate of
  `Base64StringExtensions`. Use `Base64StringExtensions`.
- **Breaking, security-motivated:** `IAesEncryption`, `AesEncryption`, and `AddAesEncryption` (`DKNet.Svc.Encryption`).
  The cipher was AES-CBC with a fixed IV embedded in the key, so identical plaintexts always produced identical
  ciphertext. Migrate to `IAesGcmEncryption` / `AddAesGcmEncryption`; there is no automated conversion of existing
  ciphertext.
- **Breaking:** `EncryptionKeyProvider` abstract class (`DKNet.EfCore.Encryption`). Implement
  `IEncryptionKeyProvider` directly — the abstract class re-declared the interface's only member with no shared
  implementation.
- **Breaking:** `IShaHashing` and `IHmacHashing` (`DKNet.Svc.Encryption`) no longer extend `IDisposable` — both were
  stateless wrappers over static hashing calls, and `Dispose()` did nothing.
- **Breaking:** `HmacHashing.VerifySha256`/`VerifySha512` (`DKNet.Svc.Encryption`) lost their `ignoreCase`
  parameter — it was read but could not change the result, since hex decoding is already case-insensitive.
- **Breaking:** `IRepositorySpec.DeleteRange<TEntity>` (`DKNet.EfCore.Specifications`). Use `BulkDeleteAsync`.
- **Breaking:** `ISpecification<TEntity>.OrderByQueries`/`.OrderByDescendingQueries`, and the equivalent properties
  on `Specification<TEntity>` (`DKNet.EfCore.Specifications`). Ordering is now a single declared-sequence model.
  **An `ISpecification<TEntity>` implementation that does not derive from `Specification<TEntity>` is no longer
  supported**, and the legacy "all ascending clauses applied first, then all descending" fallback ordering is gone —
  that path produced different SQL than the declared-sequence path for the same specification.
- `IdempotencyDistributedCacheStore` (`DKNet.AspCore.Idempotency`), the `IDistributedCache`-backed store whose
  check-then-act reservation was never atomic. Not a breaking change: the type was `internal` and no public
  registration selected it. The parameterless `AddIdempotentKey()` overload that used to default to it is **not**
  removed — it now registers the atomic in-process store described under [Added](#added).
- **Breaking:** `EnumExtensions.GetEumInfos<T>()`/`GetEumInfo()` (`DKNet.Fw.Extensions`) renamed to
  `GetEnumInfos<T>()`/`GetEnumInfo()` — the old names were a typo.

### Fixed
- `[RaisesEvent]` convention-form composed payloads no longer pull a navigation/complex-type property into the
  record when a non-empty `Include` names it — `Include` narrowed the entity's *own* scalar properties but was
  silently reusing the property as-is when it named a navigation, shipping every property of the referenced type.
  Navigation properties are now omitted unconditionally, matching `Exclude`/no-filter behaviour.
- `[GenerateDto]`'s `Exclude`/`Include` and `[RaisesEvent]`'s narrowing `params` argument, when written as
  `new[] { nameof(...) }` (a classic array-initializer, as opposed to the `[nameof(...)]` collection-expression
  form), previously resolved to an empty filter and silently kept the named property instead of dropping/narrowing
  it. This is now resolved like every other form: an affected `[GenerateDto]` DTO narrows as declared, and an
  affected `[RaisesEvent]` narrowing list both narrows the raise condition and changes the composed event name
  (e.g. `OrderUpdatedEvent` → `OrderStatusUpdatedEvent`) — re-check any declaration using this exact array syntax.
- Data seeding (`IDataSeedingConfiguration`, `DKNet.EfCore.Extensions`) compared entities by reference equality, so
  every seed row was re-inserted on every application start. Comparison is now by primary key; existing databases
  that accumulated duplicate seed rows are not cleaned up automatically.
- `DisableHooks()` (`DKNet.EfCore.Hooks`) suppressed hooks process-wide via a static dictionary keyed by `DbContext`
  type name, so one request's `using (db.DisableHooks())` silently disabled audit logging, event dispatch, and
  owner stamping for every concurrent request against that `DbContext` type. Suppression is now scoped to the
  logical call context via `AsyncLocal`.
- `TransformerService` (`DKNet.Svc.Transformation`) cached token values on the instance keyed only by token text, so
  a second `Transform`/`TransformAsync` call on the same instance with different parameters returned the first
  call's values. The cache is now local to each call.
- Azure `ListItemsAsync` (`DKNet.Svc.BlobStorage.AzureStorage`) returned the searched-for prefix as every item's
  name instead of the item's own name, so a folder listing came back with every result sharing one name —
  including through `BlobService.GetItemAsync`.
- S3 `ListItemsAsync` (`DKNet.Svc.BlobStorage.AwsS3`) never read `IsTruncated`/`NextContinuationToken`, so any
  prefix with more than 1,000 objects silently dropped the rest; it also classified a 0-byte or 1-byte file as a
  directory. Listing now paginates fully and classifies by key shape, not size.
- `LocalBlobService`'s relative-path computation (`DKNet.Svc.BlobStorage.Local`) used `string.Replace` against the
  root folder name, which strips every occurrence — a file under a subfolder that happens to repeat the root
  folder's name resolved to the wrong relative path. Now uses `Path.GetRelativePath`.
- `TableExistsAsync` (`DKNet.EfCore.Relational.Helpers`) treated any `DbException` from a probe query as "table
  does not exist", so a permissions failure or timeout silently reported `false`. It now queries
  `INFORMATION_SCHEMA.TABLES` directly and lets infrastructure errors propagate.
- `EfCoreExceptionHandler` (`DKNet.EfCore.Extensions`) decided retryability by matching EF Core's English exception
  text; it now inspects `exception.Entries`, which retries a slightly broader set of concurrency conflicts.
- `TypeExtractor`'s fluent API (`DKNet.Fw.Extensions`) mutated shared predicate state, so branching one extractor
  (e.g. `var abstracts = e.Abstract(); var concretes = e.NotAbstract();` off the same instance) made both branches
  empty. Each fluent call now returns an independent extractor.
- `DateTimeExtensions.LastDayOfMonth` (`DKNet.Fw.Extensions`) reconstructed the `DateTime` and hardcoded
  `DateTimeKind.Local`, silently converting a UTC input to Local and dropping sub-millisecond precision. It now
  shifts the date with `AddDays`, preserving `Kind` and full precision.
- `EnumExtensions.GetEnumInfos`/`GetEnumInfo` (`DKNet.Fw.Extensions`) filtered the enum backing field by assuming an
  `int` backing type, so a `byte`- or `long`-backed enum leaked a spurious `value__` entry into the results.
- `GetEntityKeyValues` (`DKNet.EfCore.Extensions`) read primary-key values via `PropertyInfo!.GetValue(...)`, which
  threw for shadow properties and backing-field-only keys — reached from audit logging. It now reads through EF's
  `CurrentValues`, with no reflection and no crash case.
- Idempotency schema migration (`DKNet.AspCore.Idempotency.Relational`) now runs once at application startup via a
  hosted service instead of on the first incoming request; the per-request check remains as a defensive fallback.

### Security
- Fixed `IRsaEncryption` resolving to an unmanaged, silently discarded random key pair per resolution
  (DKNet.Svc.Encryption).
- Fixed `IAesGcmEncryption` and `IAesEncryption` resolving to a random, never-persisted key per resolution, which
  made every value they encrypted unrecoverable (DKNet.Svc.Encryption).
- `DKNet.EfCore.DataAuthorization` now fails closed when a `DbContext` does not implement `IDataOwnerDbContext`.
  `DataOwnerAuthQuery.HasQueryFilter` previously guarded that case with `Debug.Fail(...)` and returned `null`;
  `Debug.Fail` is compiled out in Release, and a `null` filter means "apply nothing", so in Release builds every
  `IOwnedBy` entity was left with no ownership filter and every caller could read every owner's rows — a complete
  row-level isolation bypass. It now throws `InvalidOperationException` at model-build time, and the tightened
  `AddDataOwnerProvider` constraint (see **Changed**) stops the mistake at compile time.

## [2024.12.0] - 2024-12-XX

### Added
- Complete framework redesign with Domain-Driven Design principles
- Onion Architecture implementation across all components
- SlimBus template for rapid API development with CQRS patterns
- Comprehensive Entity Framework Core extensions
- Repository pattern with specifications support
- Domain event handling and dispatching
- Data authorization and tenant-aware filtering
- Blob storage abstractions for Azure, AWS S3, and local storage
- Comprehensive testing strategy with 99% coverage goals
- .NET 10.0 support across all packages with C# 14 language features

### Core Framework (DKNet.Fw.Extensions)
- Added comprehensive extension methods for types, properties, and enums
- Async enumerable extensions with full support
- Property utilities for dynamic access and manipulation
- Type checking and conversion utilities
- Enhanced error handling patterns

### Entity Framework Core Extensions
- **DKNet.EfCore.Abstractions**: Core interfaces and base classes
- **DKNet.EfCore.Extensions**: EF Core functionality enhancements
- **DKNet.EfCore.Repos**: Repository pattern implementations
- **DKNet.EfCore.Repos.Abstractions**: Repository interface definitions
- **DKNet.EfCore.Hooks**: Entity lifecycle hooks and interceptors
- **DKNet.EfCore.Events**: Domain event handling system
- **DKNet.EfCore.DataAuthorization**: Row-level security and filtering
- **DKNet.EfCore.Specifications**: Specification pattern for queries

### Messaging & CQRS
- **DKNet.SlimBus.Extensions**: SlimMessageBus integration for EF Core
- Command and query handler patterns
- Event-driven architecture support
- Message bus integration with domain events

### Service Layer
- **DKNet.Svc.BlobStorage.Abstractions**: File storage service interfaces
- **DKNet.Svc.BlobStorage.AzureStorage**: Azure Blob Storage implementation
- **DKNet.Svc.BlobStorage.AwsS3**: AWS S3 storage implementation
- **DKNet.Svc.BlobStorage.Local**: Local file system storage
- **DKNet.Svc.Transformation**: Data transformation utilities

### Templates
- **SlimBus.ApiEndpoints**: Complete API template with:
  - Minimal API endpoints with versioning
  - CQRS pattern implementation
  - Domain-driven design structure
  - Entity Framework Core integration
  - Authentication and authorization
  - Testing examples with TestContainers
  - Docker support and deployment configurations

### Infrastructure
- **.NET Aspire integration**: Service discovery and orchestration
- **Comprehensive CI/CD**: GitHub Actions workflows
- **Code quality**: SonarCloud, CodeQL, and Codecov integration
- **Package management**: Centralized version management
- **Documentation**: Auto-generated API docs and GitHub Pages

### Testing
- **99% code coverage** targets for core libraries
- **TestContainers** integration for reliable integration tests
- **Shouldly** assertions throughout test suite
- **Architecture tests** to enforce design constraints
- **Performance benchmarking** capabilities

## Package Version History

### Core Packages
- `DKNet.Fw.Extensions`: 1.0.0+ (Core framework extensions)
- `DKNet.RandomCreator`: 1.0.0+ (Random data generation utilities)

### Entity Framework Core Packages
- `DKNet.EfCore.Abstractions`: 1.0.0+
- `DKNet.EfCore.Extensions`: 1.0.0+
- `DKNet.EfCore.Repos`: 1.0.0+
- `DKNet.EfCore.Repos.Abstractions`: 1.0.0+
- `DKNet.EfCore.Hooks`: 1.0.0+
- `DKNet.EfCore.Events`: 1.0.0+
- `DKNet.EfCore.DataAuthorization`: 1.0.0+
- `DKNet.EfCore.Specifications`: 1.0.0+
- `DKNet.EfCore.Relational.Helpers`: 1.0.0+

### Messaging Packages
- `DKNet.SlimBus.Extensions`: 1.0.0+

### Service Packages
- `DKNet.Svc.BlobStorage.Abstractions`: 1.0.0+
- `DKNet.Svc.BlobStorage.AzureStorage`: 1.0.0+
- `DKNet.Svc.BlobStorage.AwsS3`: 1.0.0+
- `DKNet.Svc.BlobStorage.Local`: 1.0.0+
- `DKNet.Svc.Transformation`: 1.0.0+

### Aspire Packages
- `Aspire.Hosting.ServiceBus`: 1.0.0+

## Breaking Changes

### From Legacy to 2024.12.0
This represents a complete rewrite of the framework with focus on:

1. **Architecture**: Migration to Domain-Driven Design and Onion Architecture
2. **Technology**: Upgrade to .NET 10.0 with C# 14 language features
3. **Patterns**: Introduction of CQRS, Event Sourcing, and Repository patterns
4. **Testing**: Comprehensive test coverage with modern testing approaches
5. **Documentation**: Complete documentation overhaul with practical examples

### Migration Path
For existing users of legacy DKNet packages:

1. **Review New Architecture**: Understand DDD and Onion Architecture principles
2. **Use Templates**: Start with SlimBus template for new projects
3. **Gradual Migration**: Replace components incrementally
4. **Follow Examples**: Use provided examples and recipes for implementation
5. **Testing Strategy**: Adopt new testing patterns with TestContainers

## Security Updates

All packages include security enhancements:
- Secure defaults for all configurations
- Input validation and sanitization
- Protection against common vulnerabilities
- Regular dependency updates
- Security scanning in CI/CD pipeline

## Performance Improvements

- Optimized Entity Framework Core queries
- Efficient domain event dispatching
- Minimal allocations in hot paths
- Async/await patterns throughout
- Lazy loading and caching strategies

---

## Individual Package Changelogs

For detailed package-specific changes, see:

### Core
- [DKNet.Fw.Extensions Changelog](https://github.com/baoduy/DKNet/blob/main/src/Core/DKNet.Fw.Extensions/CHANGELOG.md)

### Entity Framework Core
- [DKNet.EfCore.Abstractions Changelog](https://github.com/baoduy/DKNet/blob/main/src/EfCore/DKNet.EfCore.Abstractions/CHANGELOG.md)
- [DKNet.EfCore.Extensions Changelog](https://github.com/baoduy/DKNet/blob/main/src/EfCore/DKNet.EfCore.Extensions/CHANGELOG.md)
- [DKNet.EfCore.Repos Changelog](https://github.com/baoduy/DKNet/blob/main/src/EfCore/DKNet.EfCore.Repos/CHANGELOG.md)
- [DKNet.EfCore.Hooks Changelog](https://github.com/baoduy/DKNet/blob/main/src/EfCore/DKNet.EfCore.Hooks/CHANGELOG.md)

### Messaging
- [DKNet.SlimBus.Extensions Changelog](https://github.com/baoduy/DKNet/blob/main/src/SlimBus/DKNet.SlimBus.Extensions/CHANGELOG.md)

### Services
- [DKNet.Svc.BlobStorage.Abstractions Changelog](https://github.com/baoduy/DKNet/blob/main/src/Services/DKNet.Svc.BlobStorage.Abstractions/CHANGELOG.md)
- [DKNet.Svc.BlobStorage.AzureStorage Changelog](https://github.com/baoduy/DKNet/blob/main/src/Services/DKNet.Svc.BlobStorage.AzureStorage/CHANGELOG.md)
- [DKNet.Svc.BlobStorage.AwsS3 Changelog](https://github.com/baoduy/DKNet/blob/main/src/Services/DKNet.Svc.BlobStorage.AwsS3/CHANGELOG.md)
- [DKNet.Svc.BlobStorage.Local Changelog](https://github.com/baoduy/DKNet/blob/main/src/Services/DKNet.Svc.BlobStorage.Local/CHANGELOG.md)

---

> 📝 **Note**: This consolidated changelog provides an overview of the entire framework. For detailed, package-specific changes, please refer to the individual package changelogs linked above.