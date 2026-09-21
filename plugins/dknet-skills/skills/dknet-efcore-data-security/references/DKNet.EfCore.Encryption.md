# DKNet.EfCore.Encryption

| Field | Value |
|---|---|
| Area | EfCore |
| NuGet | `dotnet add package DKNet.EfCore.Encryption` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Encryption.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/EfCore/DKNet.EfCore.Encryption |
| Depends on (DKNet) | none |
| Depends on (3rd party) | `Microsoft.EntityFrameworkCore` |
| Target framework | `net10.0` |

## Purpose

Transparent, column-level encryption for EF Core `string` properties: mark a property `[Encrypted]`, register a key
provider, call `modelBuilder.UseColumnEncryption(keyProvider)` once from `OnModelCreating`, and EF Core's own
`ValueConverter` pipeline encrypts on write (AES-GCM, random IV) and decrypts on read. No repository, service, or LINQ
call site changes — application code always sees plaintext. It is a model-build-time mechanism only: a
`ValueConverter<string?, string?>` attached per property, not a `SaveChanges` interceptor, so it has zero interaction
with `DKNet.EfCore.Hooks`/`IHook`.

**NOT** for values you need to filter, sort, `LIKE`-search, or index on in SQL — a random IV per write makes every
encrypted column opaque to comparisons at the database layer. It is also not the same package as
`DKNet.Svc.Encryption` (general-purpose crypto under `Services`, no EF Core dependency at all — there is no
`DKNet.Svc.BlobStorage.Encryption` package anywhere in this repo).

## Entry points

| Call | Exact signature | Called on | Notes |
|---|---|---|---|
| `AddEfCoreEncryption<TKeyServiceImplementation>` | `public static IServiceCollection AddEfCoreEncryption<TKeyServiceImplementation>(this IServiceCollection services) where TKeyServiceImplementation : class, IEncryptionKeyProvider` | `IServiceCollection` | `EfCoreEncryptionSetup`, namespace `DKNet.EfCore.Encryption`. Uses `TryAddSingleton` — safe to call more than once; the first registration wins, later calls are no-ops. Register before `BuildServiceProvider()`/before the `DbContext` that consumes `IEncryptionKeyProvider` is resolved. |
| `UseColumnEncryption` | `public static void UseColumnEncryption(this ModelBuilder modelBuilder, IEncryptionKeyProvider encryptionKeyProvider)` | `ModelBuilder`, inside `OnModelCreating` | `ModelBuilderExtensions`, namespace `DKNet.EfCore.Encryption.Extensions`. Call exactly once per model build, after `base.OnModelCreating(modelBuilder)`. Throws `ArgumentNullException` on either null argument; throws `InvalidOperationException` if any `[Encrypted]` property is a PK or FK. `GetKey` is invoked once per matching property at this point — EF Core caches the compiled model, so this effectively runs once per `DbContext` type per app lifetime. |
| `[Encrypted]` | `[AttributeUsage(AttributeTargets.Property)] public sealed class EncryptedAttribute : Attribute` | property | `DKNet.EfCore.Encryption.Attributes`. No constructor arguments, no settable members — a pure marker. Discovered via reflection (`Attribute.IsDefined`) inside `UseColumnEncryption`, not through EF Core's model-building conventions, so it has no effect unless `UseColumnEncryption` runs. Only inspected on properties whose CLR type is `string`; on any other CLR type it is silently ignored. |

## Public surface

### `DKNet.EfCore.Encryption`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `EfCoreEncryptionSetup` | static class | DI registration entry point | `AddEfCoreEncryption<TKeyServiceImplementation>(this IServiceCollection services) : IServiceCollection` |

### `DKNet.EfCore.Encryption.Attributes`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `EncryptedAttribute` | attribute (sealed, `AttributeTargets.Property`) | Opts a `string` property into encryption | none beyond the default parameterless constructor |

### `DKNet.EfCore.Encryption.Converters`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `ColumnEncryptionConverter` | sealed class, extends `ValueConverter<string?, string?>` | The actual EF Core value converter; delegates to an `IColumnEncryptionProvider` | `ColumnEncryptionConverter(IColumnEncryptionProvider encryptionProvider)` — primary constructor; base `ValueConverter` wired with `v => encryptionProvider.Encrypt(v)` / `v => encryptionProvider.Decrypt(v)` |

### `DKNet.EfCore.Encryption.Encryption`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IEncryptionKeyProvider` | interface | Consumer-supplied key source, one key per entity CLR type | `byte[] GetKey(Type entityType)` |
| `AesGcmColumnEncryptionProvider` | sealed class, implements `IColumnEncryptionProvider` | Default (and only shipped) algorithm: AES-128/192/256-GCM, authenticated, random 12-byte IV per call | `AesGcmColumnEncryptionProvider(byte[] key)` (throws `ArgumentNullException`/`ArgumentException`); `string? Decrypt(string? ciphertext)`; `string? Encrypt(string? plaintext)` |

### `DKNet.EfCore.Encryption.Extensions`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `ModelBuilderExtensions` | static class | The `OnModelCreating` wiring hook | `UseColumnEncryption(this ModelBuilder modelBuilder, IEncryptionKeyProvider encryptionKeyProvider) : void` |

### `DKNet.EfCore.Encryption.Interfaces`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IColumnEncryptionProvider` | interface | The swappable encryption-algorithm abstraction consumed by `ColumnEncryptionConverter` | `string? Decrypt(string? ciphertext)`; `string? Encrypt(string? plaintext)` |

No internal types affect observable behaviour beyond what's documented above (no hidden static state, no reflection
caching layer).

## Options & defaults

There is no options/settings class and nothing binds from `appsettings.json`. The only "configuration" surface is the
`IEncryptionKeyProvider` you implement and the constructor argument to `AesGcmColumnEncryptionProvider`.

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `IEncryptionKeyProvider.GetKey(Type entityType)` | `byte[]` | none — you must implement it | Supplies the AES key for every `[Encrypted]` property on that entity type; evaluated once per property, at model-build time | Your own class, registered via `AddEfCoreEncryption<T>()` |
| `AesGcmColumnEncryptionProvider(byte[] key)` key length | `byte[]` | required, must be 16, 24, or 32 bytes | Selects AES-128/192/256; any other length throws `ArgumentException`, `null` throws `ArgumentNullException` | Constructed internally by `UseColumnEncryption`, or manually if you call `ColumnEncryptionConverter` yourself |
| DI registration lifetime | n/a | `Singleton` (via `TryAddSingleton`) | `IEncryptionKeyProvider` is resolved once and shared for the app's lifetime | `AddEfCoreEncryption<TKeyServiceImplementation>()` |
| DI registration idempotency | n/a | first registration wins | A second `AddEfCoreEncryption<TOther>()` call is a no-op if `IEncryptionKeyProvider` is already registered | `AddEfCoreEncryption<TKeyServiceImplementation>()` (uses `TryAddSingleton`, not `AddSingleton`) |

## Usage patterns

### End-to-end setup: attribute + key provider + DI + model wiring

**When**: the common case — encrypt one or more `string` columns on an entity with a single key source for the app.

```csharp
using DKNet.EfCore.Encryption;
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

public class AppDbContext(DbContextOptions<AppDbContext> options, IEncryptionKeyProvider keyProvider)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseColumnEncryption(keyProvider);
    }
}
```

```csharp
using DKNet.EfCore.Encryption;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEfCoreEncryption<AppEncryptionKeyProvider>();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
```

**Notes**: adding a `Customer` with `Ssn = "123-45-6789"` and saving stores ciphertext; reading `customer.Ssn` back
gives plaintext. `Ssn == null` is fine — nulls pass through unconverted.

### Swapping the encryption algorithm (bypassing `UseColumnEncryption`)

**When**: you need a different `IColumnEncryptionProvider` — `UseColumnEncryption` always hardcodes
`AesGcmColumnEncryptionProvider` with no DI slot to override it for `[Encrypted]`-scanned properties.

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

**Notes**: this applies per property, manually — `[Encrypted]` is irrelevant here since `UseColumnEncryption` never
runs. To use a genuinely different algorithm, implement your own `IColumnEncryptionProvider` and pass it to
`ColumnEncryptionConverter` the same way.

### Per-entity-type keys

**When**: different entities must use different keys (e.g., different tenants' data, or separating "high value"
columns from routine PII).

```csharp
using DKNet.EfCore.Encryption.Encryption;

public sealed class PerEntityKeyProvider(IReadOnlyDictionary<Type, byte[]> keysByEntityType) : IEncryptionKeyProvider
{
    public byte[] GetKey(Type entityType) =>
        keysByEntityType.TryGetValue(entityType, out var key)
            ? key
            : throw new InvalidOperationException($"No encryption key configured for {entityType.Name}.");
}
```

**Notes**: `GetKey` receives the entity's declaring CLR type, not the property name — every `[Encrypted]` property on
the same entity type shares one key. There's no per-property key support.

### Handling decrypt failures at the read boundary

**When**: a key rotation, restore from an older backup, or tampering could put ciphertext the current key can't
decrypt in front of a read.

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

**Notes**: `ColumnEncryptionConverter`/EF Core's query pipeline does not catch any of these — an unreadable row throws
out of `SaveChangesAsync`/query materialization unless you handle it yourself (e.g., in a repository method or a
migration script during key rotation).

### Testing model wiring without a database

**When**: you want to assert that `[Encrypted]` properties get a converter (or that a PK/FK misuse throws) without
standing up any `DbContext`/provider.

```csharp
using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Encryption.Attributes;
using DKNet.EfCore.Encryption.Encryption;
using DKNet.EfCore.Encryption.Extensions;
using Microsoft.EntityFrameworkCore;

public class Person
{
    [Key] public int Id { get; set; }
    [Encrypted] public string? Ssn { get; set; }
}

internal sealed class FixedKeyProvider : IEncryptionKeyProvider
{
    public byte[] GetKey(Type entityType) => new byte[32];
}

public static class ModelWiringDemo
{
    public static void AssertConverterApplied()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Person>();
        modelBuilder.UseColumnEncryption(new FixedKeyProvider());

        var property = modelBuilder.Model.FindEntityType(typeof(Person))!
            .FindProperty(nameof(Person.Ssn))!;

        _ = property.GetValueConverter(); // non-null once UseColumnEncryption has run.
    }
}
```

**Notes**: this is the exact pattern the package's own model-wiring tests use — no EF Core provider (SQL
Server/InMemory/etc.) is required to test the wiring itself.

## Runtime behaviour

1. **Model build (once per `DbContext` type, cached thereafter)**: `OnModelCreating` runs, calls
   `modelBuilder.UseColumnEncryption(keyProvider)`. It walks every entity type's every property, filters to CLR type
   `string`, then to those carrying `[Encrypted]` (via reflection on the underlying `PropertyInfo`).
2. For each matching property: if the property is a primary key or a foreign key, throws
   `InvalidOperationException` immediately — model build fails, the app does not start.
3. Otherwise calls `encryptionKeyProvider.GetKey(propertyInfo.DeclaringType!)`, constructs
   `new AesGcmColumnEncryptionProvider(key)`, wraps it in `new ColumnEncryptionConverter(...)`, and sets it as the
   property's value converter. EF Core's compiled model now carries this converter for the lifetime of the
   `DbContext` type in the process.
4. **On `SaveChangesAsync`**: for each tracked entity with a changed `[Encrypted]` property, EF Core's own value
   conversion pipeline (not a DKNet hook) calls the converter's provider-side expression, i.e.
   `AesGcmColumnEncryptionProvider.Encrypt` — generates a random 12-byte IV, encrypts with AES-GCM, writes
   `Convert.ToBase64String(iv + tag + ciphertext)` to the database parameter. `null`/empty strings are returned
   unchanged (`Encrypt` short-circuits on `string.IsNullOrEmpty`).
5. **On query materialization**: EF Core calls the converter's model-side expression, i.e.
   `AesGcmColumnEncryptionProvider.Decrypt` — Base64-decodes, slices IV/tag/ciphertext by fixed offsets
   (`IvSize = 12`, `TagSize = 16`), runs `AesGcm.Decrypt`, and returns the UTF-8 plaintext. A fresh `AesGcm` instance
   is constructed per `Encrypt`/`Decrypt` call (not cached/shared), so concurrent round-trips on the same
   `AesGcmColumnEncryptionProvider` instance are safe.
6. This entire flow is independent of `DKNet.EfCore.Hooks`/`IHook`/`AuditLogs`/`DataAuthorization` — it's a plain
   `ValueConverter<string?, string?>`, so it composes with (and doesn't need ordering relative to) any SaveChanges
   interceptor from those packages.

## Diagnostics & exceptions

No Roslyn analyzer ships in this package — all diagnostics are runtime exceptions.

| Exception | Severity | When | Fix |
|---|---|---|---|
| `ArgumentNullException` (`key`) | Fatal (throws) | `new AesGcmColumnEncryptionProvider(null)` | Supply a non-null key from `IEncryptionKeyProvider.GetKey`. |
| `ArgumentException` (`key`, "Key length must be 16, 24, or 32 bytes") | Fatal (throws) | `AesGcmColumnEncryptionProvider` constructor, key length not in `{16, 24, 32}` | Use a 128/192/256-bit key. |
| `ArgumentNullException` (`modelBuilder` or `encryptionKeyProvider`) | Fatal (throws) | `UseColumnEncryption(null, ...)` or `UseColumnEncryption(mb, null)` | Pass non-null arguments; call from inside `OnModelCreating` where `modelBuilder` is always non-null. |
| `InvalidOperationException` (names the offending `Type.Property`) | Fatal (throws at model-build time) | `[Encrypted]` applied to a primary-key or foreign-key property | Remove `[Encrypted]` from the key column; encrypt a non-key column instead, or maintain a separate deterministic value for lookups. |
| `ArgumentException` ("Invalid ciphertext format") | Fatal (throws) | `Decrypt` called with a Base64 payload shorter than `IvSize (12) + TagSize (16)` bytes | The stored value isn't ciphertext this provider produced — check for data corruption, truncation, or a schema/column mismatch. |
| `InvalidOperationException` ("Decryption failed. The data may be corrupted or the key is incorrect.") | Fatal (throws) | `Decrypt` — AES-GCM tag authentication fails (a `CryptographicException` is caught internally) | Confirm the correct key for that entity type is in use; if rotating keys, re-encrypt existing rows with the new key before switching. |
| `FormatException` | Fatal (throws, **not caught/wrapped** by this package) | `Decrypt` called with a string that isn't valid Base64 at all | Comes straight from `Convert.FromBase64String`; validate/normalize input before calling `Decrypt` if it might not be well-formed ciphertext. |

## Gotchas (source-verified)

- **Encrypted columns are opaque to SQL.** `AesGcmColumnEncryptionProvider.Encrypt` fills a fresh random 12-byte IV
  per call, so the same plaintext never produces the same ciphertext twice. `Where(c => c.Ssn == "123-45-6789")`
  compiles to a comparison against ciphertext and matches nothing; `ORDER BY`, `LIKE`, and indexes on the column
  are equally useless. Workaround: decrypt-and-compare in memory, or maintain a separate deterministic (e.g.,
  HMAC) column for lookups.
- **Only `ClrType == typeof(string)` properties are ever scanned.** `[Encrypted]` on a non-`string` property is
  silently ignored — no compile error, no runtime exception, nothing logged.
- **Marking a PK/FK `[Encrypted]` crashes app startup, not a graceful no-op.** `UseColumnEncryption` throws
  `InvalidOperationException` while the model is being built — the exception message names the exact
  `Type.Property`.
- **One key per entity type, not per property.** `IEncryptionKeyProvider.GetKey(Type entityType)` only ever
  receives the property's declaring type; two `[Encrypted]` properties on the same entity always share the same
  key material.
- **No key rotation or ciphertext versioning.** `GetKey` is called once per property at model-build time, and EF
  Core caches the compiled model — changing what your provider returns has no effect until the app restarts
  (forcing a model rebuild), and old rows still need a separate re-encryption migration; there's no
  dual-key/versioned-format support anywhere in the package.
- **Ciphertext is always longer than plaintext.** Stored value is `Base64(12-byte IV + 16-byte tag + ciphertext)`
  — roughly `plaintext-bytes + 28`, then ×~1.33 for Base64. An undersized `nvarchar`/`varchar` column silently
  truncates on write (EF Core/the DB provider decide what happens on overflow, this package does not validate
  column width).
- **`UseColumnEncryption` hardcodes the algorithm — there's no DI seam to swap it for `[Encrypted]`-driven
  properties.** `IColumnEncryptionProvider` exists as an abstraction, but `UseColumnEncryption` always constructs
  `AesGcmColumnEncryptionProvider` directly; it never resolves `IColumnEncryptionProvider` from DI. To use a
  different algorithm, skip `UseColumnEncryption` and call `.HasConversion(new ColumnEncryptionConverter(yourProvider))`
  per property yourself.
- **Invalid Base64 input to `Decrypt` throws `FormatException`, not this package's own `ArgumentException`.** The
  "Invalid ciphertext format" `ArgumentException` only fires for a payload that decodes but is too short; a
  string that isn't Base64 at all fails earlier, inside `Convert.FromBase64String`, before the package's own
  validation runs.

## Anti-patterns & hallucination traps

- There is no `AddEfCoreEncryption(IEncryptionKeyProvider instance)` overload and no non-generic
  `AddEfCoreEncryption()` — the only signature is
  `AddEfCoreEncryption<TKeyServiceImplementation>(this IServiceCollection)`, constrained to
  `class, IEncryptionKeyProvider`. It registers the *type*, not an instance you construct yourself.
- `EncryptedAttribute` takes **no constructor arguments** — there is no `[Encrypted(Algorithm = "AES256")]` or
  `[Encrypted(KeyName = "...")]`. The type is declared as `public sealed class EncryptedAttribute : Attribute;` with
  an empty body.
- `UseColumnEncryption` never resolves anything from DI and never reads `IColumnEncryptionProvider` — don't assume
  registering a custom `IColumnEncryptionProvider` in the container changes what `[Encrypted]` properties use; it
  doesn't, per the gotcha above.
- There is no `EncryptionOptions`, `ColumnEncryptionOptions`, or any `services.Configure<...>()` binding — nothing in
  this package reads `appsettings.json`.
- `IEncryptionKeyProvider` has no abstract base class to derive from and no default/no-op implementation shipped —
  you always write the whole interface yourself.
- Don't hand-roll IV/tag slicing assuming a different byte order — the fixed wire format is
  `Base64(IV[12] + Tag[16] + Ciphertext[n])`, in that exact order.
- Don't expect `Decrypt`/`Encrypt` to throw on `null` or empty string — both are explicit short-circuits that return
  the input unchanged, not an error path.
- This package's `[Encrypted]`, `IColumnEncryptionProvider`, `IEncryptionKeyProvider` etc. live only under
  `DKNet.EfCore.Encryption.*` — do not confuse with (or import types from) `DKNet.Svc.Encryption`, an unrelated
  general-purpose crypto package under `Services` with no EF Core dependency and a different API surface
  entirely. There is no `DKNet.Svc.BlobStorage.Encryption` package.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Abstractions` | Declares an audit "sensitive" marker that controls whether a value is *shown* in an audit trail. Reach for it to hide a value from logs; reach for this package to protect the same value at rest in the database. |
| `DKNet.EfCore.AuditLogs` | Records field-level changes on `SaveChangesAsync`. By default (`AuditPropertyPolicy.RedactSensitive`) it already redacts an `[Encrypted]` property's old/new value to `***REDACTED***` in the audit trail — it matches by attribute type *name* (`EncryptedAttribute`), not by referencing this package, so a same-named attribute from anywhere is also caught. Encryption and audit redaction already compose for free; `[SensitiveData]` is only for redacting properties this package never touches. |
| `DKNet.EfCore.DataAuthorization` | Row-level access control (which rows a caller may see). Orthogonal to this package, which decides whether a returned row's column is even readable. |
| `DKNet.EfCore.Extensions` | The general model-building/wiring layer for the rest of the DKNet EF Core stack. `UseColumnEncryption` is an independent `OnModelCreating` call — it does not hook into that package's auto-configuration pipeline. |
| `DKNet.EfCore.Hooks` | **Not used by this package at all.** Encryption is a `ValueConverter`, not a `SaveChanges` interceptor — there is no ordering concern with `IHook` implementations. |

## Testing notes

- Tests use `Microsoft.EntityFrameworkCore.InMemory` for the round-trip/model-wiring tests, even though the repo's
  general guidance is to avoid EF Core InMemory for integration tests. That guidance is about masking
  SQL-translation bugs; this package's behaviour (value conversion attached at model build, encrypt/decrypt
  happening in the converter, not in SQL) is identical on every provider, so InMemory is sufficient and no
  TestContainers/SQL Server fixture is used anywhere in this test project.
- Canonical round-trip pattern: build a context with a random-key test provider, `Add` + `SaveChanges` in one
  scope, dispose, open a **new** context instance against the same in-memory database name, reload, and assert the
  plaintext property values round-trip.
- Pure model-wiring assertions need no `DbContext`/provider at all: construct a bare `new ModelBuilder()`, configure
  the entity (and, for PK/FK tests, `HasKey`/`HasForeignKey` explicitly), call `UseColumnEncryption`, then inspect
  `property.GetValueConverter()` or assert the thrown `InvalidOperationException`/`ArgumentNullException` and its
  message content (entity/property names must appear in the message).
- `AesGcmColumnEncryptionProvider` is tested with zero EF Core involvement: round-trip across key lengths and
  plaintext shapes (Unicode, whitespace, newlines), tamper detection (flip a byte, expect
  `InvalidOperationException`), wrong-key detection, a Base64-encoded known-answer ciphertext to pin the wire format
  across refactors, and a concurrent round-trip test (many tasks × many iterations) to pin the construct-per-call
  `AesGcm` design.
- A reflection-based architecture test guards that every public static extension method on a static class in the
  assembly takes `IServiceCollection`, never the concrete `ServiceCollection` — worth copying verbatim when adding a
  new DI extension to any DKNet package.
