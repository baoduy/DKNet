---
name: dknet-efcore-data-security
description: Covers DKNet.EfCore.DataAuthorization and DKNet.EfCore.Encryption. DataAuthorization gives a multi-tenant row filter over IOwnedBy entities via IDataOwnerProvider and IDataOwnerDbContext.AccessibleKeys, wired with AddDataOwnerProvider<TDbContext,TProvider>; the global query filter fails closed (empty AccessibleKeys denies, not allows) and a SaveChanges hook stamps OwnedBy. Encryption encrypts a column transparently by marking a string property [Encrypted], registering AddEfCoreEncryption<TKeyProvider> plus an IEncryptionKeyProvider, then calling modelBuilder.UseColumnEncryption — a ValueConverter encryption pipeline producing an AES-GCM column with a random IV per write. Use this for tenant isolation EF Core wiring, rows leaking across tenants, an InvalidOperationException from UseColumnEncryption or DataOwnerAuthQuery, or key-rotation and IColumnEncryptionProvider questions.
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.EfCore.DataAuthorization, DKNet.EfCore.Encryption"
---

# DKNet row-level data authorization and column encryption

This skill teaches the two DKNet packages that protect data *inside* a `DbContext`: `DKNet.EfCore.DataAuthorization`
(who can see/write a row) and `DKNet.EfCore.Encryption` (whether a column's value is even readable at the storage
layer). They are independent, compose cleanly, and are both model-build-time mechanisms layered on top of
`DKNet.EfCore.Extensions`/`DKNet.EfCore.Hooks`. Open `references/DKNet.EfCore.DataAuthorization.md` or
`references/DKNet.EfCore.Encryption.md` for the full API surface, diagnostics, and testing notes behind any rule
below.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.EfCore.DataAuthorization` | `dotnet add package DKNet.EfCore.DataAuthorization` | Row-level, ownership-based multi-tenant filtering: a non-ignorable global query filter over `IOwnedBy` entities, plus a `SaveChanges` hook that stamps `OwnedBy` and guards against silent reassignment. | `DKNet.EfCore.Extensions`, `DKNet.EfCore.Hooks`, `DKNet.EfCore.AuditLogs` | [references/DKNet.EfCore.DataAuthorization.md](references/DKNet.EfCore.DataAuthorization.md) |
| `DKNet.EfCore.Encryption` | `dotnet add package DKNet.EfCore.Encryption` | Transparent column-level encryption for `string` properties via an EF Core `ValueConverter` — AES-GCM, random IV, zero repository/LINQ changes. | `Microsoft.EntityFrameworkCore` only (no DKNet project references) | [references/DKNet.EfCore.Encryption.md](references/DKNet.EfCore.Encryption.md) |

## Quick start

### Row-level tenant isolation

```csharp
using DKNet.EfCore.DataAuthorization;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class Invoice : IOwnedBy
{
    public Guid Id { get; private set; }
    public string OwnedBy { get; private set; } = string.Empty;

    public static Invoice Create(string ownerKey) => new() { Id = Guid.NewGuid(), OwnedBy = ownerKey };

    public void Reassign(string ownerKey) => OwnedBy = ownerKey;
}

public interface ICurrentTenant
{
    string? TenantId { get; }
    IReadOnlyCollection<string> AccessibleTenantIds { get; }
}

public sealed class CurrentTenantAccessor : ICurrentTenant
{
    public string? TenantId => "tenant-a";
    public IReadOnlyCollection<string> AccessibleTenantIds => [TenantId!];
}

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    public IEnumerable<string> AccessibleKeys { get; init; } = [];
    // IsUnrestrictedAccess defaults to false via the interface — deny by default.
}

public sealed class TenantOwnerProvider(ICurrentTenant currentTenant) : IDataOwnerProvider
{
    public string? GetOwnershipKey() => currentTenant.TenantId;
}

public static class InvoiceEndpoints
{
    public static async Task CreateInvoiceAsync(AppDbContext db, string ownerKey)
    {
        db.Invoices.Add(Invoice.Create(ownerKey));
        await db.SaveChangesAsync();
    }
}
```

```csharp
using DKNet.EfCore.DataAuthorization;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddScoped<ICurrentTenant, CurrentTenantAccessor>()
    .AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>()
    .AddDbContextWithHook<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
               .UseAutoConfigModel<AppDbContext>());
```

Every query against `db.Invoices` now filters to `AppDbContext.AccessibleKeys`; every `Invoice.Create(...)` added
and saved gets `OwnedBy` stamped automatically. Both require **all three** of `AddDataOwnerProvider`,
`AddDbContextWithHook` (or `UseHooks` yourself), and `UseAutoConfigModel` — see Gotchas.

### Transparent column encryption

```csharp
using DKNet.EfCore.Encryption.Attributes;
using DKNet.EfCore.Encryption.Encryption;
using DKNet.EfCore.Encryption.Extensions;
using Microsoft.EntityFrameworkCore;

public sealed class AppEncryptionKeyProvider : IEncryptionKeyProvider
{
    private readonly byte[] _key = Convert.FromBase64String(
        Environment.GetEnvironmentVariable("APP_ENCRYPTION_KEY")!); // 16, 24, or 32 bytes

    public byte[] GetKey(Type entityType) => _key;
}

public class Customer
{
    public int Id { get; set; }

    [Encrypted]
    public string? Ssn { get; set; }
}

public class SecureDbContext(DbContextOptions<SecureDbContext> options, IEncryptionKeyProvider keyProvider)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseColumnEncryption(keyProvider);
    }
}

public static class CustomerEndpoints
{
    public static async Task CreateCustomerAsync(SecureDbContext db, string ssn)
    {
        db.Customers.Add(new Customer { Ssn = ssn });
        await db.SaveChangesAsync();
    }
}
```

```csharp
using DKNet.EfCore.Encryption;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEfCoreEncryption<AppEncryptionKeyProvider>();
builder.Services.AddDbContext<SecureDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
```

`Customer.Ssn` is stored as ciphertext and read back as plaintext — no change to any query or repository code.

## Rules

1. Always call `UseAutoConfigModel<TContext>()` for any `DbContext` with `IOwnedBy` entities — `AddDataOwnerProvider` alone never attaches the filter, and skipping it is silent (every tenant sees every row).
2. Always wire `AddDbContextWithHook<TDbContext>(...)` (or call `options.UseHooks<TDbContext>(provider)` yourself) — otherwise `DataOwnerHook` never runs and new rows save with a blank `OwnedBy`.
3. Declare `IDataOwnerDbContext.AccessibleKeys` as `IEnumerable<string>`, never `ICollection<string>`/`List<string>` — EF Core cannot translate `ICollection<string>.Contains` in a query filter.
4. Treat an empty `AccessibleKeys` as deny-all, not allow-all — the only bypass is `IsUnrestrictedAccess => true`.
5. Implement `IDataOwnerDbContext` on every `DbContext` in the process with an `IOwnedBy` entity, not only the one passed to `AddDataOwnerProvider` — the filter registration is process-wide.
6. Never register `ICurrentUserProvider` (`DKNet.EfCore.AuditLogs`) without checking who else shares the process — it stops every `DataOwnerHook` from stamping `CreatedBy`/`UpdatedBy` from the ownership key.
7. Never mark a primary-key or foreign-key property `[Encrypted]` — `UseColumnEncryption` throws `InvalidOperationException` at model-build time and the app fails to start.
8. Only apply `[Encrypted]` to `string` properties — any other CLR type is silently ignored, no error, no log.
9. Never filter, sort, `LIKE`, or index an `[Encrypted]` column in SQL — a random IV per write makes identical plaintext produce different ciphertext every time.
10. Treat `IEncryptionKeyProvider.GetKey(Type entityType)` as one key per entity type, not per property — two `[Encrypted]` properties on the same entity always share a key.
11. Plan for key rotation yourself — there is no versioning or dual-key support; a provider change only takes effect after a restart, and existing rows need a separate re-encryption migration.
12. Never construct `DataOwnerAuthQuery`/`DataOwnerHook` directly — both are `internal sealed`; wire everything through `AddDataOwnerProvider<TDbContext, TProvider>()`.

## How to ...

### Let a caller access more than one tenant key

**When**: a head-office/admin user legitimately spans several tenant/branch keys.

```csharp
using DKNet.EfCore.DataAuthorization;
using System.Linq;

public sealed class HeadOfficeOwnerProvider(ICurrentTenant currentTenant) : IDataOwnerProvider
{
    public string? GetOwnershipKey() => currentTenant.TenantId;

    public ICollection<string> GetAccessibleKeys() => currentTenant.AccessibleTenantIds.ToList();
}
```

- `GetOwnershipKey()` still decides what gets stamped on a brand-new row.
- `GetAccessibleKeys()` decides what the query filter (via `AccessibleKeys`) and the reassignment guard treat as legal.

### Give an admin or background job unrestricted read access

**When**: a migration, seed script, or admin API needs to see every tenant's rows.

```csharp
using DKNet.EfCore.DataAuthorization;
using Microsoft.EntityFrameworkCore;

public class AdminDbContext(DbContextOptions<AdminDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    public IEnumerable<string> AccessibleKeys { get; init; } = [];
    public bool IsUnrestrictedAccess => true; // deliberate, greppable opt-in
}
```

- This is the *only* supported bypass — an empty `AccessibleKeys` with `IsUnrestrictedAccess` still `false` denies every row; it never grants access.

### Add a second DbContext to a process that already has one

**When**: a reporting/read-side `DbContext` shares the process with a `DbContext` already passed to `AddDataOwnerProvider`, and its model also contains an `IOwnedBy` entity.

```csharp
using DKNet.EfCore.DataAuthorization;
using Microsoft.EntityFrameworkCore;

public class ReportingDbContext(DbContextOptions<ReportingDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    public IEnumerable<string> AccessibleKeys { get; init; } = [];
    public bool IsUnrestrictedAccess => true; // a reporting context typically wants everything
}
```

- The filter registration is process-wide (a static bag of global model builders); any other context with an `IOwnedBy` entity that calls `UseAutoConfigModel()` gets it too, and its model build throws unless it also implements `IDataOwnerDbContext`.

### Make CreatedBy/UpdatedBy name the signed-in user, not the tenant key

**When**: `OwnedBy` should still record the tenant, but audit fields should record the human.

```csharp
using DKNet.EfCore.AuditLogs;
using Microsoft.AspNetCore.Http;

public sealed class SignedInUserProvider(IHttpContextAccessor accessor) : ICurrentUserProvider
{
    public string? GetCurrentUser() => accessor.HttpContext?.User?.FindFirst("sub")?.Value;
}
```

```csharp
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.DataAuthorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>()
    .AddCurrentUserProvider<AppDbContext, SignedInUserProvider>();
```

- When `GetCurrentUser()` returns non-empty, `DataOwnerHook` stamps `OwnedBy` only and the audit hook stamps `CreatedBy`/`UpdatedBy`; with no provider registered (or one returning null/empty), `DataOwnerHook` keeps filling audit fields from the ownership key.
- `AddCurrentUserProvider` registers `ICurrentUserProvider` un-keyed and process-wide — see Rule 6.

### Use a different encryption key per entity type

**When**: different entities need different key material (per-tenant keys, or separating "high value" columns).

```csharp
using DKNet.EfCore.Encryption.Attributes;
using DKNet.EfCore.Encryption.Encryption;

public class Payment
{
    public int Id { get; set; }

    [Encrypted]
    public string? CardNumber { get; set; }
}

public sealed class PerEntityKeyProvider(IReadOnlyDictionary<Type, byte[]> keysByEntityType) : IEncryptionKeyProvider
{
    public byte[] GetKey(Type entityType) =>
        keysByEntityType.TryGetValue(entityType, out var key)
            ? key
            : throw new InvalidOperationException($"No encryption key configured for {entityType.Name}.");
}
```

- `GetKey` receives the entity's declaring CLR type, never the property name — every `[Encrypted]` property on one entity type shares that key. There is no per-property key.

### Swap the encryption algorithm instead of using `[Encrypted]`

**When**: you need an algorithm other than AES-GCM — `UseColumnEncryption` never resolves `IColumnEncryptionProvider` from DI, so registering one there has no effect on `[Encrypted]`-scanned properties.

```csharp
using DKNet.EfCore.Encryption.Converters;
using DKNet.EfCore.Encryption.Encryption;
using Microsoft.EntityFrameworkCore;

public class ManualDbContext(DbContextOptions<ManualDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var key = Convert.FromBase64String(Environment.GetEnvironmentVariable("APP_ENCRYPTION_KEY")!);

        modelBuilder.Entity<Customer>()
            .Property(c => c.Ssn)
            .HasConversion(new ColumnEncryptionConverter(new AesGcmColumnEncryptionProvider(key)));
    }
}
```

- `[Encrypted]` is irrelevant here since `UseColumnEncryption` never runs for this property — implement your own `IColumnEncryptionProvider` and pass it to `ColumnEncryptionConverter` for a non-AES-GCM algorithm entirely.

### Handle decrypt failures safely during key rotation or tampering

**When**: a restore from an older backup, a mid-rotation key mismatch, or tampering could put ciphertext the current key cannot decrypt in front of a read.

```csharp
using DKNet.EfCore.Encryption.Encryption;

public static class DecryptSafely
{
    public static string? TryDecrypt(byte[] currentKey, string? storedCiphertext)
    {
        var provider = new AesGcmColumnEncryptionProvider(currentKey);

        try
        {
            return provider.Decrypt(storedCiphertext);
        }
        catch (FormatException)
        {
            return null; // storedCiphertext was not valid Base64 at all.
        }
        catch (ArgumentException)
        {
            return null; // Base64-decoded, but shorter than IV(12) + Tag(16) — not a real payload.
        }
        catch (InvalidOperationException)
        {
            return null; // valid-shaped payload, but AES-GCM authentication failed — wrong key or tampered data.
        }
    }
}
```

- EF Core's own query pipeline does not catch any of these — an unreadable row throws out of query materialization unless you guard it yourself (a repository method, or a migration script during key rotation).

## Runtime behaviour

**DataAuthorization**, in order:
1. Model build: `UseAutoConfigModel` applies `DataOwnerAuthQuery` to every `IOwnedBy` entity type; it casts the `DbContext` to `IDataOwnerDbContext` and throws `InvalidOperationException` immediately if that fails.
2. Every query: EF Core evaluates `IsUnrestrictedAccess || AccessibleKeys.Contains(OwnedBy)` fresh against the live, scoped context, expanding `.Contains` into SQL `IN (...)`.
3. `SaveChangesAsync` (hooks wired): `DataOwnerHook` stamps `OwnedBy` (and `CreatedBy`/`CreatedOn`, unless a signed-in-user provider is present) on `Added` rows, and reverts a `Modified` row's `OwnedBy` back to its original value if the new value isn't in `GetAccessibleKeys()`.

**Encryption**, in order:
1. Model build (once per `DbContext` type, cached): `UseColumnEncryption` scans every `string` property for `[Encrypted]`, rejects PK/FK matches with `InvalidOperationException`, and attaches a `ColumnEncryptionConverter` backed by `AesGcmColumnEncryptionProvider` to each match.
2. `SaveChangesAsync`: EF Core's own value-conversion pipeline calls `Encrypt` — random 12-byte IV, AES-GCM, `Base64(iv + tag + ciphertext)` written to the parameter.
3. Query materialization: EF Core calls `Decrypt` — Base64-decode, slice IV/tag/ciphertext, `AesGcm.Decrypt`, return UTF-8 plaintext. No DKNet hook runs at either step — this is orthogonal to `DataOwnerHook`/`IHook`.

## Gotchas

- **Empty `AccessibleKeys` denies access, it does not mean unrestricted.** The predicate is `IsUnrestrictedAccess || AccessibleKeys.Contains(...)`; a half-initialized provider fails closed, not open.
- **Forgetting `UseAutoConfigModel` disables the filter with no runtime error.** Every `IOwnedBy` query then silently returns every owner's rows, forever.
- **Forgetting `UseHooks`/`AddDbContextWithHook` means new rows are never stamped.** `AddHook` alone only registers the hook in DI; new `IOwnedBy` rows save with a blank `OwnedBy` and vanish behind the deny-by-default filter, even for their own creator.
- **The filter registration is process-wide, not per-`TDbContext`.** `AddDataOwnerProvider` adds `DataOwnerAuthQuery` to a bag shared by the whole process; any other `DbContext` with an `IOwnedBy` entity and `UseAutoConfigModel()` gets it too.
- **A registered `ICurrentUserProvider` changes audit stamping application-wide, silently.** It is registered un-keyed; once any call registers one, every `DataOwnerHook` stops stamping `CreatedBy`/`UpdatedBy` from the ownership key whenever `GetCurrentUser()` returns non-empty.
- **The reassignment guard only sees changes EF Core's own `ChangeTracker` sees.** Raw SQL, `ExecuteUpdate`, or any bulk-update path bypassing `SaveChanges` is invisible to it — it is not a database-level constraint.
- **Encrypted columns are opaque to SQL.** A random IV per write means identical plaintext never produces identical ciphertext; `WHERE`, `ORDER BY`, `LIKE`, and indexes on an `[Encrypted]` column are all useless.
- **Marking a PK/FK `[Encrypted]` crashes app startup, not a graceful no-op.** `UseColumnEncryption` throws `InvalidOperationException` while the model is being built, naming the exact `Type.Property`.
- **One encryption key per entity type, not per property.** `GetKey(Type entityType)` never sees the property — two `[Encrypted]` properties on the same entity always share a key.
- **There is no key rotation or ciphertext versioning.** `GetKey` runs once per property at model-build time on a cached compiled model; a provider change has zero effect until the process restarts, and old rows need a separate re-encryption migration.
- **`UseColumnEncryption` hardcodes `AesGcmColumnEncryptionProvider` — it never reads `IColumnEncryptionProvider` from DI.** A custom implementation registered in the container changes nothing; call `.HasConversion(new ColumnEncryptionConverter(yourProvider))` per property instead.

## Do not

- `services.AddDataOwnerFilter(...)`, `AddOwnershipFilter(...)`, `AddRowLevelSecurity(...)` — none exist; the one call is `AddDataOwnerProvider<TDbContext, TProvider>()`.
- `modelBuilder.HasDataOwnerFilter<T>()` or any per-entity `ModelBuilder` opt-in for ownership — does not exist; `UseAutoConfigModel` applies `DataOwnerAuthQuery` automatically.
- `[Encrypted(Algorithm = "AES256")]` or `[Encrypted(KeyName = "...")]` — `EncryptedAttribute` takes no constructor arguments at all.
- `AddEfCoreEncryption(someProviderInstance)` — there is no non-generic overload; the only signature is `AddEfCoreEncryption<TKeyServiceImplementation>()`, which registers a *type*, not an instance.
- Registering a custom `IColumnEncryptionProvider` in DI and expecting `UseColumnEncryption` to pick it up — it never resolves one from the container (see Gotchas).
- `DKNet.Svc.Encryption` and `[Encrypted]`/`IEncryptionKeyProvider` are unrelated APIs in unrelated packages — do not mix their namespaces. There is no `DKNet.Svc.BlobStorage.Encryption` package.

```csharp
// no-compile
public class BadDbContext(DbContextOptions<BadDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    // Wrong: this does not implement IDataOwnerDbContext.AccessibleKeys (IEnumerable<string>), and even if the
    // types matched, EF Core cannot translate ICollection<string>.Contains inside a query filter.
    public ICollection<string> AccessibleKeys { get; init; } = new List<string>();
    public bool IsUnrestrictedAccess => false;
}
```

```csharp
// no-compile
services.AddDataOwnerFilter<AppDbContext, TenantOwnerProvider>(); // no such method — it's AddDataOwnerProvider
var query = new DataOwnerAuthQuery(); // internal sealed — not constructible outside the assembly
```

## Related skills

- `dknet-packages` — start here to pick the right package before wiring anything.
- `dknet-efcore-domain-model` — entity base classes and `UseAutoConfigModel`/global-filter mechanics this skill depends on but does not itself teach.
- `dknet-efcore-specifications` — the query surface built on top of this filter; use it to query, not to defeat the filter (`IsIgnorable => false`).
- `dknet-efcore-save-pipeline` — `IHook`, domain events, and the `AuditLogs` `ICurrentUserProvider`/`CreatedBy`/`UpdatedBy` mechanics this skill only touches at the seam.
- `dknet-codegen` — DTO/CRUD source generators; a `[GenerateDto]` DTO for an `IOwnedBy`/`[Encrypted]` entity still round-trips plaintext, generators know nothing about either package.
- `dknet-slimbus-cqrs` — CQRS handlers reading/writing `IOwnedBy`/`[Encrypted]` entities; both apply transparently underneath the handler.
- `dknet-aspcore-api` — minimal-API endpoint conventions on top of a `DbContext` secured by this skill.
- `dknet-idempotency` — idempotent endpoints and key stores; an unrelated data path.
- `dknet-blob-storage` — blob storage adapters; for encrypting blob content, not database columns.
- `dknet-services` — `DKNet.Svc.Encryption` for explicit application-level crypto outside EF Core, not `[Encrypted]` columns.
- `dknet-core-utilities` — foundation helpers (e.g. `DKNet.RandomCreator`) for generating tenant/owner keys or key material.
- `dknet-testing` — fixture patterns for testing filters/hooks (SQLite in-memory) and encryption round-trips (EF Core InMemory), and testing inside the DKNet repo itself.

## References

- [references/DKNet.EfCore.DataAuthorization.md](references/DKNet.EfCore.DataAuthorization.md) — full entry points, public surface, options, runtime behaviour, diagnostics, gotchas, anti-patterns, and testing notes for the row-level ownership filter and its `SaveChanges` hook.
  Docs: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.DataAuthorization.md ·
  NuGet: https://www.nuget.org/packages/DKNet.EfCore.DataAuthorization
- [references/DKNet.EfCore.Encryption.md](references/DKNet.EfCore.Encryption.md) — the same depth for column encryption: the converter pipeline, key-provider contract, ciphertext layout, and PK/FK restrictions.
  Docs: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Encryption.md ·
  NuGet: https://www.nuget.org/packages/DKNet.EfCore.Encryption
- Full docs site: https://baoduy.github.io/DKNet/
