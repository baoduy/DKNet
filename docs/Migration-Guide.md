# Migration Guide

This guide helps you migrate between different versions of DKNet Framework and provides guidance for handling breaking changes.

## Table of Contents

- [Current migration scenarios](#current-migration-scenarios)
- [Version-specific migrations](#version-specific-migrations)
  - [Off `DKNet.EfCore.Repos` onto Specifications](#entity-framework-core-migration)
  - [`DKNet.Svc.Encryption` — AES ciphers are now opt-in](#dknetsvcencryption--aes-ciphers-are-now-opt-in)
  - [`DKNet.EfCore.DataAuthorization` — `IDataOwnerDbContext` is now required](#upgrading-dknetefcoredataauthorization-idataownerdbcontext-is-now-required)
  - [Behaviour changes that fail silently](#behaviour-changes-that-fail-silently)
  - [`DKNet.Svc.Encryption` / `DKNet.EfCore.Encryption` — removed types and hashing signatures](#dknetsvcencryption--dknetefcoreencryption--removed-types-and-hashing-signatures)
  - [`DKNet.EfCore.Specifications` — ordering model, `DeleteRange`, and return types](#dknetefcorespecifications--ordering-model-deleterange-and-return-types)
  - [`DKNet.Fw.Extensions` — removed and renamed members](#dknetfwextensions--removed-and-renamed-members)
  - [`DKNet.AspCore.Idempotency` — no store needed for local development](#dknetaspcoreidempotency--no-store-needed-for-local-development)
  - [`DKNet.Svc.BlobStorage.Abstractions` — `IncludedExtensions` is now `IReadOnlyList<string>`](#dknetsvcblobstorageabstractions--includedextensions-is-now-ireadonlyliststring)
- [Architecture migration](#architecture-migration)
- [CQRS migration](#cqrs-migration)
- [Database migration](#database-migration)
- [Testing migration](#testing-migration)
- [Migration tools](#migration-tools)
- [Common issues](#common-issues)
- [Migration checklist](#migration-checklist)

---

## Current Migration Scenarios

### From Legacy DKNet to 2024.12.0+

This is a **major architectural migration** from legacy packages to the new Domain-Driven Design framework.

#### Key Changes
- **Architecture**: complete shift to DDD/Onion Architecture — see the [Architecture Guide](Architecture.md)
- **Technology**: .NET 10.0; every package targets `net10.0` with `LangVersion=latest`, except the two Roslyn
  source generators (`DKNet.EfCore.DtoGenerator`, `DKNet.SlimBus.Generators`) which target `netstandard2.0` to be
  loadable by the compiler
- **Patterns**: CQRS via SlimMessageBus, the specification pattern instead of generic repositories, and domain
  events dispatched from the `SaveChanges` pipeline
- **Testing**: TestContainers.MsSql for anything touching persistence — see [Testing Strategy](Testing-Strategy.md)

Released versions and their breaking changes are listed in the [Changelog](CHANGELOG.md).

#### Migration Strategy

**1. Assessment Phase**
```bash
# Analyze your current usage
git grep -r "DKNet" --include="*.cs" src/
# Review dependencies
dotnet list package --include-transitive | grep DKNet
```

**2. Incremental Migration**
- Start with new projects using the SlimBus.ApiEndpoints template in the [DKNet.Templates](https://github.com/baoduy/DKNet.Templates) repository
- Migrate existing projects component by component
- Run both old and new implementations in parallel during transition

**3. Component Migration Order**
1. **Core Extensions** → `DKNet.Fw.Extensions`
2. **Data Access** → `DKNet.EfCore.*` packages
3. **Business Logic** → Domain entities and services
4. **API Layer** → Controllers/endpoints
5. **Infrastructure** → External service integrations

---

## Version-Specific Migrations

### Upgrading to .NET 10.0

**Prerequisites** — install the .NET 10 SDK from
[dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0), then pin it. DKNet's own
`src/global.json` uses `rollForward` so a newer patch SDK still builds:

```json
{
  "sdk": {
    "version": "10.0.0",
    "rollForward": "latestMajor",
    "allowPrerelease": false
  }
}
```

**Project Files**
```xml
<!-- Update target framework -->
<TargetFramework>net10.0</TargetFramework>

<!-- Update package references -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.AspNetCore.App" />
```

**Language version.** Every DKNet project sets `LangVersion=latest`, so on .NET 10 the packages are built with
C# 14 — including the C# 14 extension-member syntax several of them use for their public API
(`SpecRepoExtensions`, `SetupEfCoreHook`, `PropertyExtensions`). Your own project does not have to match: the
compiled API surface is ordinary extension methods, callable from any language version that can consume
`net10.0`.

### Entity Framework Core Migration

**Before (Legacy)**
```csharp
using Microsoft.EntityFrameworkCore;

public class ProductRepository
{
    private readonly DbContext _context;
    
    public ProductRepository(DbContext context)
    {
        _context = context;
    }
    
    public async Task<Product> GetAsync(int id)
    {
        return await _context.Set<Product>().FindAsync(id);
    }
}
```

**After — `DKNet.EfCore.Specifications`**

`DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions` have been **removed** — the packages no longer exist, so
a project reference to either will not restore. `IRepositorySpec` (registered via
`services.AddSpecRepo<AppDbContext>()`) is the current repository surface. It is not generic over the entity — the
entity type comes from the `Specification<T>` passed to each call:

```csharp
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;

public sealed class ProductService(IRepositorySpec repo)
{
    // Specification support
    public Task<Product?> FindAsync(Specification<Product> spec, CancellationToken cancellationToken = default) =>
        repo.FirstOrDefaultAsync(spec, cancellationToken);
}
```

See [`Migrating-Repos-To-Specifications.md`](./EfCore/Migrating-Repos-To-Specifications.md) for the full call-site mapping.

### DKNet.Svc.Encryption — AES ciphers are now opt-in

**Symptom**

The DI container throws `InvalidOperationException` ("Unable to resolve service for type
`DKNet.Svc.Encryption.Ciphers.IAesGcmEncryption`") where injecting the cipher used to work. `AddEncryptionServices()`
registered `IAesGcmEncryption` and the obsolete `IAesEncryption` as transients over a randomly generated key that was
never persisted, so the resolution that used to succeed was already producing ciphertext nothing could decrypt.

**Before**
```csharp
using DKNet.Svc.Encryption;

builder.Services.AddEncryptionServices(); // also registered IAesGcmEncryption and IAesEncryption
```

**After** — supply the key from configuration or a key vault:
```csharp
using DKNet.Svc.Encryption;

builder.Services.AddEncryptionServices();                                       // IShaHashing, IHmacHashing
builder.Services.AddAesGcmEncryption(builder.Configuration["Crypto:AesKey"]!);  // singleton IAesGcmEncryption
```

`AddAesGcmEncryption` takes a plain Base64 AES key (128/192/256-bit); a `key:iv` string is rejected.

**`IAesEncryption`, `AesEncryption`, and `AddAesEncryption` are now deleted, not just obsolete.** They implemented
AES-CBC with the IV fixed and embedded in the key, so identical plaintexts always produced identical ciphertext — a
security defect, not just a design smell. There is no compatibility overload left to call: migrate to
`AddAesGcmEncryption` above. Ciphertext produced under the old cipher cannot be decrypted by AES-GCM; re-encrypt it
during migration if it must be kept.

### Upgrading DKNet.EfCore.DataAuthorization: IDataOwnerDbContext is now required

**Symptom** — after upgrading, a registration that used to build now fails to compile with a generic-constraint
error (`CS0311`) on the `AddDataOwnerProvider<TDbContext, TProvider>()` call, saying your `DbContext` cannot be used
as `TDbContext` because it is not convertible to `DKNet.EfCore.DataAuthorization.IDataOwnerDbContext`.

**Cause** — `AddDataOwnerProvider<TDbContext, TProvider>()` is now declared
`where TDbContext : DbContext, IDataOwnerDbContext` (it previously constrained `TDbContext` to `DbContext` only).
The old signature let you register a `DbContext` that the ownership query filter could not read: the filter was then
skipped for every `IOwnedBy` entity in Release builds, so every caller saw every owner's rows. The compile error is
the fix surfacing a real, previously silent, data-isolation hole.

**Fix** — implement `IDataOwnerDbContext` on the exact `DbContext` type you register:

```csharp
using DKNet.EfCore.DataAuthorization;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    public IEnumerable<string> AccessibleKeys { get; init; } = [];
    // IsUnrestrictedAccess defaults to false via the interface —
    // override it only for system/admin contexts that must bypass ownership filtering.
}
```

Keep `AccessibleKeys` declared as `IEnumerable<string>` — EF Core cannot translate `ICollection<string>.Contains`
inside a query filter.

The ownership filter is attached by `UseAutoConfigModel<TContext>()`, which is a
`DbContextOptionsBuilder<TContext>` extension — it belongs in the registration callback, **not** in
`OnModelCreating`, where no `DbContextOptionsBuilder` exists:

```csharp
using DKNet.EfCore.DataAuthorization;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;

services
    .AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>()
    .AddDbContextWithHook<AppDbContext>(options => options
        .UseSqlServer(connectionString)
        .UseAutoConfigModel<AppDbContext>());   // required for the filter to be attached
```

**Also check every other `DbContext` in the process** — the compile error is not the only way this upgrade bites.
The same release makes `DataOwnerAuthQuery` throw `InvalidOperationException` at model-build time when a context
reaches the filter without the interface, instead of quietly applying no filter. And `AddDataOwnerProvider`
registers that filter in `EfCoreSetup.GlobalModelBuilders`, which is **static**: every `DbContext` calling
`UseAutoConfigModel()` applies it, not only the one you passed as `TDbContext`. A second context — an audit or
reporting `DbContext`, say — whose model contains `IOwnedBy` entities and that does not implement
`IDataOwnerDbContext` therefore starts throwing at model-build time after this upgrade, with no compile error
pointing at it. Implement `IDataOwnerDbContext` on that context too, or keep `IOwnedBy` entities out of its model.
Details: [DKNet.EfCore.DataAuthorization](./EfCore/DKNet.EfCore.DataAuthorization.md).

### Behaviour changes that fail silently

These ship with no compile error and no exception — the code keeps running, just with different (correct) results.
Check whether you depended on the old behaviour before you move on to the API removals below.

**Data seeding now compares by primary key, not reference.** `IDataSeedingConfiguration` (`DKNet.EfCore.Extensions`)
compared seed entities by reference equality, which a freshly materialized row can never satisfy — so every
seeded row was re-inserted on every application start. It now compares by primary key. Nothing in your code
changes, but if seed data has been accumulating duplicates, clean up the extra rows once after upgrading:

```sql
-- Example: keep the lowest Id per duplicate key, delete the rest
;WITH ranked AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY /* your seed key columns */ ORDER BY Id) AS rn
    FROM YourSeededTable
)
DELETE FROM ranked WHERE rn > 1;
```

**`DisableHooks()` is now scoped to the call, not the process.** `DKNet.EfCore.Hooks`' `context.DisableHooks()`
used a static dictionary keyed by `DbContext` type name, so one request's `using (db.DisableHooks())` silently
disabled audit logging, event dispatch, and owner stamping for **every concurrent request** against that
`DbContext` type. It is now scoped via `AsyncLocal` to the logical call context that opened it:

```csharp
// Same call shape — but a concurrent request on another AsyncLocal context is no longer affected.
using (db.DisableHooks())
{
    await db.SaveChangesAsync(); // hooks suppressed only here
}
```

If a background job relied on the old process-wide suppression to also silence hooks for concurrent HTTP requests,
that side effect is gone — disable hooks explicitly wherever you need it now.

**`TransformerService` no longer caches across calls.** `DKNet.Svc.Transformation`'s token cache was keyed by
token text alone, so a second `Transform`/`TransformAsync` call on the same instance with different parameters
silently returned the first call's values:

```csharp
// Before the fix, both lines returned "Hello Alice":
svc.Transform("Hello [Name]", new { Name = "Alice" });
svc.Transform("Hello [Name]", new { Name = "Bob" });   // now correctly returns "Hello Bob"
```

The service is registered `AddTransient`, so most callers were unaffected — this only bit an instance held across
multiple `Transform` calls (a scoped/singleton wrapper, or a loop reusing one injected instance).

**Blob storage listing results were wrong, not just slow.** Three separate defects, all fixed with no API change:

- `DKNet.Svc.BlobStorage.AzureStorage`'s `ListItemsAsync` returned the searched-for **prefix** as every item's
  name instead of the blob's own name — a folder listing came back with every result sharing one name, and
  `BlobService.GetItemAsync` (which takes the first listed item) inherited the bug.
- `DKNet.Svc.BlobStorage.AwsS3`'s `ListItemsAsync` never read `IsTruncated`/`NextContinuationToken`, so any prefix
  with more than 1,000 objects silently dropped the rest with no error; it also classified 0-byte and 1-byte files
  as directories.
- `DKNet.Svc.BlobStorage.Local`'s relative-path computation used `string.Replace` against the root folder name,
  which strips **every** occurrence — a file under a subfolder that happens to repeat the root folder's name
  (e.g. root `/var/store`, file `/var/store/tenants/store/a.txt`) resolved to the wrong relative path.

If you list more than 1,000 S3 objects under one prefix, list Azure blobs and rely on the returned `Name`, or run
`LocalBlobService` with a root folder name that recurs as a subfolder elsewhere in the tree, re-verify those paths
after upgrading — the old results were silently incomplete or wrong, not erroring.

### DKNet.Svc.Encryption / DKNet.EfCore.Encryption — removed types and hashing signatures

**Before**
```csharp
using DKNet.Svc.Encryption;

// Base65StringExtensions — misspelled duplicate class, correctly-spelled methods
"dGVzdA==".FromBase64String(); // resolved to Base65StringExtensions.FromBase64String

// HmacHashing.VerifySha256/VerifySha512 took an ignoreCase flag that could not change the result
bool ok = hmac.VerifySha256(message, secretKey, expectedSignature, ignoreCase: true);
```

```csharp
using DKNet.EfCore.Encryption.Encryption;

// EncryptionKeyProvider — abstract class re-declaring the interface's only member
public class MyKeyProvider : EncryptionKeyProvider
{
    public override byte[] GetKey(Type entityType) => ...;
}
```

**After**
```csharp
using DKNet.Svc.Encryption;

// Use the correctly-spelled type
"dGVzdA==".FromBase64String();

// ignoreCase is gone — drop the argument
bool ok = hmac.VerifySha256(message, secretKey, expectedSignature);
```

```csharp
using DKNet.EfCore.Encryption.Encryption;

// Implement the interface directly — no base class to derive from
public class MyKeyProvider : IEncryptionKeyProvider
{
    public byte[] GetKey(Type entityType) => ...;
}
```

Also: `IShaHashing` and `IHmacHashing` no longer extend `IDisposable`. If you wrapped an injected instance in a
`using` block or called `.Dispose()` explicitly, remove that — both interfaces were always stateless wrappers over
static hashing calls, and `Dispose()` did nothing.

### DKNet.EfCore.Specifications — ordering model, DeleteRange, and return types

**`OrderByQueries`/`OrderByDescendingQueries` are removed.** `ISpecification<TEntity>` and `Specification<TEntity>`
now carry a single declared-sequence ordering model. If your specifications derive from `Specification<TEntity>`
and call `AddOrderBy`/`AddOrderByDescending`, nothing changes at the call site. **If you wrote a custom
`ISpecification<TEntity>` implementation that does not derive from `Specification<TEntity>`, it is no longer
supported** — the legacy fallback that applied all ascending clauses first and all descending clauses second is
gone, and that path produced different SQL than the declared-sequence path for the same specification. Derive from
`Specification<TEntity>` instead.

**Before**
```csharp
using DKNet.EfCore.Specifications.Repositories;

await repo.DeleteRange<Product>(p => p.IsDiscontinued, cancellationToken);
```

**After**
```csharp
using DKNet.EfCore.Specifications.Repositories;

await repo.BulkDeleteAsync<Product>(p => p.IsDiscontinued, cancellationToken);
```

**Return-type change (binary-breaking, source-compatible).** `SpecRepoExtensions.ToListAsync`,
`ModelSpecRepoExtensions.ToListAsync`, and both `SpecRepoExtensions.ToKeysetPageAsync` overloads now return
`Task<List<T>>` instead of `Task<IList<T>>`. Source code compiles unchanged (`List<T>` implements `IList<T>`); a
precompiled assembly built against the old signature must be recompiled.

### DKNet.Fw.Extensions — removed and renamed members

**Before**
```csharp
using System.Collections.Generic; // DKNet's ToListAsync lived here
using System.Linq;

IAsyncEnumerable<Product> products = GetProductsAsync();
List<Product> list = await products.ToListAsync(); // ambiguous even before removal — see below

var infos = EnumExtensions.GetEumInfos<Status>();   // typo'd name
```

**After**
```csharp
using System.Linq; // .NET 10's System.Linq.AsyncEnumerable.ToListAsync

IAsyncEnumerable<Product> products = GetProductsAsync();
List<Product> list = await products.ToListAsync(cancellationToken); // now takes a CancellationToken

var infos = EnumExtensions.GetEnumInfos<Status>();  // corrected name
```

`AsyncEnumerableExtensions.ToListAsync` deliberately lived in the ambient `System.Collections.Generic` namespace so
it resolved without an import — which meant any file with implicit usings had **two** applicable `ToListAsync()`
overloads in scope (DKNet's and .NET 10's own `System.Linq.AsyncEnumerable.ToListAsync`), an ambiguity error
waiting to happen. It is removed; call the BCL version.

Two more fixes in this package need no code change, only awareness if you compensated for the old behaviour:
`DateTimeExtensions.LastDayOfMonth` now preserves the input's `DateTimeKind` and sub-millisecond precision (it
previously forced `DateTimeKind.Local`), and `TypeExtractor`'s fluent filters (`Abstract()`, `NotAbstract()`, etc.)
no longer mutate shared state, so branching one extractor into two filtered results now works instead of both
branches coming back empty.

### DKNet.AspCore.Idempotency — no store needed for local development

No code change is required: every existing registration keeps working exactly as it did.

`services.AddIdempotentKey()` — no type argument, no connection string — is supported and enables idempotency end
to end on a new in-process store:

```csharp
using DKNet.AspCore.Idempotency;

services.AddIdempotentKey();                                  // optional: AddIdempotentKey(o => ...)
```

That store reserves each key atomically within the process, so two concurrent requests with the same key can never
both reach the handler. It keeps those keys in the process's own memory, which means they are lost on restart and
are not shared between instances — it is for local development and unit tests, never for production. While it is
the store serving requests, the app logs one startup warning saying exactly that.

For deployed traffic, pick a store package and call its own registration method (each internally wires up
`AddIdempotentKey<TSoreImplement>()` with its own store type, which is `internal` and cannot be named directly):
```csharp
using DKNet.AspCore.Idempotency.MsSqlStore;   // or .NpgsqlStore / .RedisStore

services.AddIdempotencyWithMsSqlStore(builder.Configuration.GetConnectionString("IdempotencyDb")!);
// services.AddIdempotencyWithNpgsqlStore(connectionString);
// services.AddIdempotencyWithRedisStore(connectionString);
```

A named store like that one wins over the no-store default whichever order the two calls run in, and it is the
named registration's `config` delegate that decides any option both set. So shared composition code can call
`AddIdempotentKey()` unconditionally and a test fixture or deployed environment can still layer a real store on
top. Between two explicitly *named* stores, first registration wins, as before.

**If you hand-wrote an `IIdempotencyKeyStore` purely to get a no-infrastructure store for local development or
tests, you can delete it** and call `AddIdempotentKey()` instead. Keep a store of your own only when it backs onto
something the shipped stores do not cover; `AddIdempotentKey<TSoreImplement>()` is unchanged and still registers it:

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Store;

services.AddIdempotentKey<MyIdempotencyKeyStore>();           // your own IIdempotencyKeyStore implementation
```

`IdempotencyDistributedCacheStore`, the old `IDistributedCache`-backed store, is deleted. It was `internal` with no
public registration left, so nothing that compiled before stops compiling.

Separately, informational only — no code change needed: idempotency schema migration
(`DKNet.AspCore.Idempotency.Relational`) now runs once at application startup via a hosted service instead of on
the first incoming request, so the first request after a deploy no longer pays the migration cost.

### DKNet.Svc.BlobStorage.Abstractions — IncludedExtensions is now IReadOnlyList<string>

**Before**
```csharp
using DKNet.Svc.BlobStorage.Abstractions;

var options = new BlobServiceOptions
{
    IncludedExtensions = someQuery.Where(x => x.Enabled).Select(x => x.Extension) // IEnumerable<string>, lazy
};
```

**After**
```csharp
using DKNet.Svc.BlobStorage.Abstractions;

var options = new BlobServiceOptions
{
    IncludedExtensions = someQuery.Where(x => x.Enabled).Select(x => x.Extension).ToList() // materialize first
};
```

`IncludedExtensions` is now `IReadOnlyList<string>`; assigning an array or a `List<string>` still compiles. A
lazy `IEnumerable<string>` query no longer compiles — materialize it first.

---

## Architecture Migration

### From N-Layer to Onion Architecture

**Legacy Structure**
```
Solution/
├── Web/              # Presentation
├── Business/         # Business Logic
├── Data/            # Data Access
└── Common/          # Shared
```

**New Structure (Onion)**
```
Solution/
├── Api/             # Presentation Layer
├── AppServices/     # Application Layer
├── Domains/         # Domain Layer
└── Infra/           # Infrastructure Layer
```

### Domain Entity Migration

**Before**
```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**After** — there is no separate `AggregateRoot` type; derive from `AuditedEntity` (or plain `Entity` if you don't
need audit fields) — see [DKNet.EfCore.Abstractions](EfCore/DKNet.EfCore.Abstractions.md):
```csharp
using DKNet.EfCore.Abstractions.Entities;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Products", Schema = "catalog")]
public class Product : AuditedEntity
{
    private Product() { } // EF Core

    public static Product Create(string name, decimal price, string createdBy)
    {
        var product = new Product { Name = name, Price = price };
        product.SetCreatedBy(createdBy);
        return product;
    }

    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }

    public void UpdatePrice(decimal newPrice, string updatedBy)
    {
        Price = newPrice;
        SetUpdatedBy(updatedBy);
        AddEvent(new ProductPriceChangedEvent(Id, Price));
    }
}
```

---

## CQRS Migration

### Command/Query Separation

**Before (Traditional Service)**
```csharp
public class ProductService
{
    public async Task<Product> CreateProductAsync(CreateProductDto dto)
    {
        // Create logic
    }
    
    public async Task<Product> GetProductAsync(int id)
    {
        // Get logic
    }
}
```

**After (CQRS via `DKNet.SlimBus.Extensions`)** — see
[DKNet.SlimBus.Extensions](Messaging/DKNet.SlimBus.Extensions.md) for the full contract set:
```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;

// Command
public record CreateProductCommand(string Name, decimal Price) : Fluents.Requests.IWitResponse<Guid>;

internal sealed class CreateProductHandler(AppDbContext db) : Fluents.Requests.IHandler<CreateProductCommand, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Command handling logic — no explicit SaveChangesAsync; the auto-save interceptor does it on success.
        return Result.Ok(Guid.NewGuid());
    }
}

// Query
public record GetProductQuery(Guid Id) : Fluents.Queries.IWitResponse<ProductResult>;

internal sealed class GetProductHandler(AppDbContext db) : Fluents.Queries.IHandler<GetProductQuery, ProductResult>
{
    public async Task<ProductResult?> OnHandle(GetProductQuery request, CancellationToken cancellationToken)
    {
        // Query handling logic
        return null;
    }
}
```

---

## Database Migration

### Schema Updates

**Add Migration for New Structure**
```bash
# Create migration
dotnet ef migrations add UpgradeToOnionArchitecture

# Review generated migration
# Update database
dotnet ef database update
```

**Data Migration Script**
```sql
-- Migrate existing data to new schema
-- Add audit fields
ALTER TABLE Products ADD CreatedBy NVARCHAR(255) NOT NULL DEFAULT 'SYSTEM';
ALTER TABLE Products ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
ALTER TABLE Products ADD UpdatedBy NVARCHAR(255) NULL;
ALTER TABLE Products ADD UpdatedAt DATETIME2 NULL;

-- Convert INT IDs to GUIDs (if needed)
-- This is a complex migration - consider keeping INT IDs if possible
```

---

## Testing Migration

DKNet's own suite uses xUnit, Shouldly, and TestContainers.MsSql, and the same reasoning applies to your tests of
DKNet-backed persistence: the EF Core in-memory provider does not translate global query filters, generated SQL,
or sequences, so it silently passes tests that would fail against a real database. See
[Testing Strategy](Testing-Strategy.md).

**Before** — in-memory, so the ownership filter and the generated SQL are never exercised:

```csharp
using Microsoft.EntityFrameworkCore;
using Xunit;

[Fact]
public async Task ActiveProducts_AreReturned()
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase("TestDb")
        .Options;

    await using var context = new AppDbContext(options);

    // Test logic
}
```

**After** — a real SQL Server, with the model built the way production builds it:

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

[Fact]
public async Task ActiveProducts_AreReturned()
{
    await using var container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    await container.StartAsync();

    var services = new ServiceCollection();
    services.AddDbContextWithHook<AppDbContext>(options => options
        .UseSqlServer(container.GetConnectionString())
        // Keep this: dropping it removes the global query filters, and the test then
        // passes for the wrong reason.
        .UseAutoConfigModel<AppDbContext>());
    services.AddSpecRepo<AppDbContext>();

    // Test against the real provider
}
```

> `mcr.microsoft.com/mssql/server` publishes no ARM64 image. On an ARM development machine, run these tests on an
> x64 runner rather than substituting a different engine — DKNet does this via the `remote-tests.yml` workflow.

## Migration Tools

### Automated Migration Helper

```csharp
using Microsoft.Extensions.DependencyInjection;

public class MigrationHelper
{
    public static async Task MigrateDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();
        
        // Run custom migrations
        await MigrateProductsAsync(context);
        await MigrateUsersAsync(context);
    }
    
    private static async Task MigrateProductsAsync(AppDbContext context)
    {
        // Custom migration logic for products
        var products = await context.Set<OldProduct>().ToListAsync();
        foreach (var oldProduct in products)
        {
            var newProduct = Product.Create(
                oldProduct.Name, 
                oldProduct.Price, 
                "MIGRATION");
            context.Set<Product>().Add(newProduct);
        }
        await context.SaveChangesAsync();
    }
}
```

### Configuration Migration

There is no `DKNetOptions` and no single aggregator to configure DKNet through — each package exposes its own
strongly-typed options, bound from your own config section via that package's own `Add*` extension. See
[Configuration & Setup](Configuration.md) for the full list; for example, migrating blob storage config:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ConfigurationMigration
{
    public static IServiceCollection MigrateFromLegacy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Point each package's own Add* method at your configuration, then let it bind itself.
        // The section name is the options type's own constant — LocalDirectoryOptions.Name is
        // "BlobStorage:LocalFolder"; the Azure and S3 adapters use "BlobService:AzureStorage"
        // and "BlobService:S3".
        services.AddLocalDirectoryBlobService(configuration);

        return services;
    }
}
```

---

## Common Issues

### 1. ID Type Changes

**Issue**: Converting from `int` to `Guid` IDs
**Solution**: 
- Keep existing `int` IDs if possible
- Use mapping layer for external APIs
- Consider gradual migration with both ID types

### 2. Breaking API Changes

**Issue**: Public API contracts change
**Solution**: 
- Version your APIs (`/api/v1/`, `/api/v2/`)
- Maintain compatibility layer
- Use deprecation warnings

### 3. Performance Issues

**Issue**: New patterns may impact performance
**Solution**: 
- Profile before and after migration
- Optimize hot paths
- Use caching where appropriate

### 4. Dependency Injection Changes

**Issue**: Service registration patterns change
**Solution**: 
```csharp
using DKNet.EfCore.Specifications;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

// Old (e.g. a hand-rolled service, or a MediatR-based handler)
services.AddScoped<ProductService>();

// New: one IRepositorySpec for every entity, plus SlimBus (DKNet's MediatR-free CQRS package).
// No per-entity repository registration, and no per-handler registration either.
services.AddSpecRepo<AppDbContext>();
services.AddSlimMessageBus(mbb => mbb
    .AddJsonSerializer()
    .AddServicesFromAssembly(typeof(CreateProductHandler).Assembly)
    .AddChildBus("Memory", builder => builder
        .WithProviderMemory()
        .AutoDeclareFrom(typeof(CreateProductHandler).Assembly)));
```

---

## Migration Checklist

### Pre-Migration
- [ ] Backup production databases
- [ ] Document current architecture
- [ ] Identify critical business logic
- [ ] Plan rollback strategy
- [ ] Set up staging environment

### During Migration
- [ ] Migrate in small increments
- [ ] Maintain comprehensive tests
- [ ] Monitor performance metrics
- [ ] Document changes as you go
- [ ] Regular communication with stakeholders

### Post-Migration
- [ ] Verify all functionality works
- [ ] Performance testing
- [ ] Update documentation
- [ ] Train team on new patterns
- [ ] Plan for ongoing maintenance

---

## Getting Help

If you encounter issues during migration:

1. **Check Documentation**: Review [Getting Started](Getting-Started.md) and [Examples](Examples/README.md)
2. **Search Issues**: Look for similar problems in [GitHub Issues](https://github.com/baoduy/DKNet/issues)
3. **Ask Questions**: Create a new issue with the `migration` label
4. **Join Discussions**: Participate in [GitHub Discussions](https://github.com/baoduy/DKNet/discussions)

---

> 💡 **Migration Tip**: Take your time with migration. It's better to migrate correctly in stages than to rush and introduce bugs. Use the SlimBus template as your reference implementation!