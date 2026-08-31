# DKNet.EfCore.Encryption

Transparent, column-level encryption for EF Core `string` properties, applied at the database boundary via a standard
`ValueConverter`.

> Scope note: this page covers `src/EfCore/DKNet.EfCore.Encryption` only — column-level encryption for EF Core
> entities. It is unrelated to `DKNet.Svc.Encryption` / `DKNet.Svc.BlobStorage.Encryption` under `src/Services`, which
> are general-purpose application/blob cryptography packages with no EF Core dependency.

## ✨ Why use it?

- **Application code never sees ciphertext** — mark a `string` property `[Encrypted]` and EF Core encrypts on the way
  to the database and decrypts on the way back. No repository, service, or query call site changes.
- **Column-level, not whole-database** — protects the specific sensitive columns (SSNs, card numbers, tokens, PII)
  that need to be unreadable in raw database files, backups, and to anyone with direct SQL access, without adopting
  TDE for everything.
- **Authenticated encryption by default** — `AesGcmColumnEncryptionProvider` uses AES-GCM with a random IV per call,
  so tampered ciphertext fails loudly instead of decrypting to garbage.
- **Key sourcing is yours, wiring is one call** — implement `IEncryptionKeyProvider` (Key Vault, KMS, config,
  whatever) and register it once with `AddEfCoreEncryption<T>()`; the model-build hook does the rest.
- **No dependency on the rest of the stack** — the package references only `Microsoft.EntityFrameworkCore`, so it drops
  into any EF Core project independent of the DKNet hook pipeline.

Do **not** reach for it when you need to filter, sort, or `LIKE`-search on the encrypted value in SQL — a random IV
per write makes encrypted columns opaque to the database. See
[Gotchas & limits](#️-gotchas--limits).

## 🚀 Quick Start

```bash
dotnet add package DKNet.EfCore.Encryption
```

Three steps get a property encrypted end-to-end:

**a) Supply a key.** Implement `IEncryptionKeyProvider` (or derive from the abstract `EncryptionKeyProvider` base, which just implements the interface abstractly — pick whichever base fits your DI style):

```csharp
public sealed class AppEncryptionKeyProvider : EncryptionKeyProvider
{
    private readonly byte[] _key = Convert.FromBase64String(
        Environment.GetEnvironmentVariable("APP_ENCRYPTION_KEY")!); // 16, 24, or 32 bytes

    public override byte[] GetKey(Type entityType) => _key;
}
```

**b) Register it in DI**, via `EfCoreEncryptionSetup.AddEfCoreEncryption<TKeyServiceImplementation>`:

```csharp
var services = new ServiceCollection();
services.AddEfCoreEncryption<AppEncryptionKeyProvider>(); // registers IEncryptionKeyProvider as a singleton
```

Note the extension's exact signature: `public static ServiceCollection AddEfCoreEncryption<TKeyServiceImplementation>(this ServiceCollection services) where TKeyServiceImplementation : class, IEncryptionKeyProvider`. It extends the **concrete** `ServiceCollection` class, not `IServiceCollection` — see [Gotchas](#️-gotchas--limits).

**c) Apply it in `OnModelCreating`** via `ModelBuilderExtensions.UseColumnEncryption`, and mark the property:

```csharp
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

From here, `context.Customers.Add(new Customer { Ssn = "123-45-6789" })` and `SaveChangesAsync()` stores ciphertext; reading `customer.Ssn` back gives the plaintext.

## 🧩 Features

### `[Encrypted]` attribute (`DKNet.EfCore.Encryption.Attributes.EncryptedAttribute`)
A property-only marker attribute with no data — `[AttributeUsage(AttributeTargets.Property)]`. `UseColumnEncryption` scans every `string` property in the model and only touches ones decorated with it:

```csharp
public class Employee
{
    public int Id { get; set; }

    [Encrypted]
    public string? TaxId { get; set; }

    public string Name { get; set; } = string.Empty; // untouched
}
```
It is only inspected on properties whose CLR type is `string`; applying it to a non-string property has no effect (silently ignored, not an error).

### `ColumnEncryptionConverter` (`DKNet.EfCore.Encryption.Converters`)
The actual EF Core value converter doing the work — a thin `ValueConverter<string?, string?>` that delegates to an `IColumnEncryptionProvider`:

```csharp
public sealed class ColumnEncryptionConverter(IColumnEncryptionProvider encryptionProvider)
    : ValueConverter<string?, string?>(
        v => encryptionProvider.Encrypt(v),
        v => encryptionProvider.Decrypt(v));
```

You normally never construct this yourself — `UseColumnEncryption` creates and applies one per matching property. You could still apply it manually to a single property for finer control:

```csharp
modelBuilder.Entity<Customer>()
    .Property(c => c.Ssn)
    .HasConversion(new ColumnEncryptionConverter(new AesGcmColumnEncryptionProvider(key)));
```

### `IColumnEncryptionProvider` (`DKNet.EfCore.Encryption.Interfaces`)
The encryption algorithm abstraction consumed by `ColumnEncryptionConverter`:

```csharp
public interface IColumnEncryptionProvider
{
    string? Decrypt(string? ciphertext);
    string? Encrypt(string? plaintext);
}
```

The package ships exactly one implementation (`AesGcmColumnEncryptionProvider`) and `UseColumnEncryption` hardcodes it — there's no DI slot to swap the algorithm for `[Encrypted]`-driven properties. To use a different provider you'd write your own model-building extension that builds `ColumnEncryptionConverter` with your `IColumnEncryptionProvider` instead of calling `UseColumnEncryption`.

### `AesGcmColumnEncryptionProvider` (`DKNet.EfCore.Encryption.Encryption`) — the default provider
AES-256/192/128-GCM (authenticated encryption). Constructor takes the raw key:

```csharp
public AesGcmColumnEncryptionProvider(byte[] key) // key.Length must be 16, 24, or 32
```

- `Encrypt(string? plaintext)` — generates a random 12-byte IV per call, encrypts with AES-GCM, and returns Base64 of `IV (12 bytes) + Tag (16 bytes) + ciphertext`. Null/empty input passes through unchanged (never encrypted).
- `Decrypt(string? ciphertext)` — reverses the packing; throws `ArgumentException` if the Base64 payload is shorter than `IV + Tag` (invalid format), or `InvalidOperationException` if AES-GCM authentication fails (wrong key or corrupted/tampered data).

Because the IV is random per call, encrypting the same plaintext twice produces different ciphertext — this is by design (semantic security) but has query implications, see [Gotchas](#️-gotchas--limits).

### `IEncryptionKeyProvider` / `EncryptionKeyProvider` (`DKNet.EfCore.Encryption.Encryption`) — where key material comes from
```csharp
public interface IEncryptionKeyProvider
{
    byte[] GetKey(Type entityType);
}

public abstract class EncryptionKeyProvider : IEncryptionKeyProvider
{
    public abstract byte[] GetKey(Type entityType);
}
```
The package supplies **no concrete key source** — no config binding, no Key Vault client, nothing that reads a connection string or secret store for you. You always write the implementation and decide where the bytes come from (environment variable, `IConfiguration`, Azure Key Vault SDK, a secrets file, etc.). `EncryptionKeyProvider` is purely a convenience base (implements the interface, still abstract) — implementing `IEncryptionKeyProvider` directly is equivalent.

`GetKey` receives the **entity's CLR type** (the property's `DeclaringType`), not the property name — so you can vary keys per entity type, but every `[Encrypted]` property on the same entity shares one key.

### `ModelBuilderExtensions.UseColumnEncryption` (`DKNet.EfCore.Encryption.Extensions`) — the wiring hook
```csharp
public static void UseColumnEncryption(this ModelBuilder modelBuilder, IEncryptionKeyProvider encryptionKeyProvider)
```
Called once from `OnModelCreating`. For every `string` property across every entity type in the model that carries `[Encrypted]`:
1. Throws `InvalidOperationException` if the property is a primary key or foreign key column (encrypting join/identity columns is unsupported).
2. Otherwise calls `encryptionKeyProvider.GetKey(propertyInfo.DeclaringType)`, builds `new ColumnEncryptionConverter(new AesGcmColumnEncryptionProvider(key))`, and calls `property.SetValueConverter(converter)`.

Throws `ArgumentNullException` if either argument is null.

### `EfCoreEncryptionSetup.AddEfCoreEncryption<TKeyServiceImplementation>` — DI registration
```csharp
public static ServiceCollection AddEfCoreEncryption<TKeyServiceImplementation>(this ServiceCollection services)
    where TKeyServiceImplementation : class, IEncryptionKeyProvider
```
Registers `TKeyServiceImplementation` as the singleton `IEncryptionKeyProvider`, but only if one isn't already registered (idempotent — safe to call more than once, or alongside a manual registration you added yourself).

## ⚙️ Configuration reference

There is no options/settings class and no `appsettings.json` binding shipped by this package — the only
"configuration" surface is the `IEncryptionKeyProvider` implementation you write, and the constructor argument to
`AesGcmColumnEncryptionProvider`.

| Knob | Type | Default | Effect |
|---|---|---|---|
| `IEncryptionKeyProvider.GetKey(Type entityType)` | `byte[]` | none — you must implement it | Supplies the AES key for every `[Encrypted]` property on that entity type. Evaluated once per property at model-build time. |
| `AesGcmColumnEncryptionProvider(byte[] key)` | `byte[]` | none — required | Key material. Must be exactly 16, 24, or 32 bytes; anything else throws `ArgumentException`, `null` throws `ArgumentNullException`. |
| `AddEfCoreEncryption<TKeyServiceImplementation>()` | `ServiceCollection` extension | — | Registers `TKeyServiceImplementation` as the singleton `IEncryptionKeyProvider`, only if one is not already registered. |
| `ModelBuilderExtensions.UseColumnEncryption(...)` | `ModelBuilder` extension | — | Must be called from `OnModelCreating`; without it, `[Encrypted]` has no effect. |

Behaviour worth knowing beyond the table:

- **Key source**: entirely up to your `IEncryptionKeyProvider`; nothing is read from configuration automatically.
- **Key length**: must be exactly 16, 24, or 32 bytes (AES-128/192/256); `AesGcmColumnEncryptionProvider`'s constructor throws `ArgumentException` otherwise, and `ArgumentNullException` for a null key.
- **Key granularity**: one key per entity CLR type (`GetKey(Type)`), applied to all of that entity's `[Encrypted]` properties — no built-in per-property key.
- **Key rotation**: not implemented by the package. `GetKey` is evaluated once per property, at model-building time (effectively once per `DbContext` type per application lifetime, since EF Core caches the compiled model). Rotating a key means: change what your `IEncryptionKeyProvider` returns, restart the app (or otherwise force EF Core to rebuild the model), and run a data migration that decrypts existing rows with the old key and re-encrypts with the new one — there's no dual-key/versioned-ciphertext support built in.
- **Nullable handling**: both `Encrypt` and `Decrypt` short-circuit on `null`/empty string and return the input unchanged — empty/null values are never turned into ciphertext.

## 🧱 Where it fits

- Built directly on EF Core's own `Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<TModel,TProvider>` — the package's `.csproj` has exactly one `PackageReference`, `Microsoft.EntityFrameworkCore`. It does **not** depend on `DKNet.EfCore.Abstractions`, `DKNet.EfCore.Extensions`, or `DKNet.EfCore.Hooks`.
- It is a **model-build-time** mechanism (a `ValueConverter` applied in `OnModelCreating`), not a `SaveChanges` interceptor/hook — it does not participate in the `DKNet.EfCore.Hooks` pipeline and has no interaction with `IHook`/`SaveChangesAsync` interception.
- `[Encrypted]` and every other type documented above live entirely inside `DKNet.EfCore.Encryption` — there's no dependency on (or hook into) `DKNet.EfCore.Abstractions` for model-building.
- Because it's just another `SetValueConverter` call, it composes with any other EF Core model configuration on the same property as long as nothing else also calls `SetValueConverter`/`HasConversion` on that property afterward (last write wins — don't double-convert the same column).
- It coexists independently with `DKNet.EfCore.AuditLogs` and `DKNet.EfCore.DataAuthorization` on the same `DbContext` — they operate on different model/pipeline surfaces (auditing/authorization are SaveChanges/query-filter concerns; this is a value-conversion concern) and don't need explicit ordering relative to each other.

## ⚠️ Gotchas & limits

- **Encrypted columns are opaque to SQL.** `AesGcmColumnEncryptionProvider` uses a random IV per encryption call, so the same plaintext produces different ciphertext every time. `context.Customers.Where(c => c.Ssn == "123-45-6789")` translates to a SQL comparison against ciphertext and will not match — equality, `LIKE`, `ORDER BY`, and indexes on encrypted columns are all unusable. Decrypt-and-compare in memory, or maintain a separate deterministic value (e.g., a HMAC hash column) if you need to search by an encrypted field.
- **Primary/foreign keys can't be encrypted.** `UseColumnEncryption` throws `InvalidOperationException` at model-build time if `[Encrypted]` is applied to a PK or FK property.
- **Only `string` properties are considered.** The scan filters on `ClrType == typeof(string)`; `[Encrypted]` on any other type is silently ignored — no compile or runtime error tells you it wasn't applied.
- **One key per entity type, not per property.** `GetKey(Type entityType)` gives you the declaring entity type only; two `[Encrypted]` properties on the same entity always share the same key.
- **No built-in key rotation or ciphertext versioning.** Changing the key requires an explicit re-encryption migration; there's no support for decrypting old ciphertext with a previous key while encrypting new writes with a new one.
- **Ciphertext is longer than plaintext.** Stored value is Base64 of `12-byte IV + 16-byte tag + ciphertext`, so a `nvarchar`/`varchar` column must be sized with headroom (roughly plaintext-bytes + 28, then ×~1.33 for Base64) or migrations/writes can truncate.
- **`AddEfCoreEncryption` extends `ServiceCollection`, not `IServiceCollection`.** If your hosting code only exposes `IServiceCollection` (some builder abstractions do), the extension method won't be callable on it directly — register `IEncryptionKeyProvider` yourself with `services.AddSingleton<IEncryptionKeyProvider, TImpl>()` instead in that case.
- **Corrupted or wrong-key ciphertext throws, not silently returns garbage.** `Decrypt` throws `ArgumentException` for a malformed payload and `InvalidOperationException` when AES-GCM authentication fails (tampered data or wrong key) — handle these at the boundary if a bad key/rotation could reach production data.
- **Not the same package as `DKNet.Svc.Encryption` / `DKNet.Svc.BlobStorage.Encryption`.** Those are general-purpose cryptography utilities under `src/Services` with no EF Core dependency; use this package specifically for EF Core column-level encryption.

## 🔗 Related packages

- [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) – declares `[SensitiveData]`, which controls whether a
  value is *shown* in an audit trail. Reach for it to hide a value from logs; reach for this package to protect it at
  rest.
- [DKNet.EfCore.AuditLogs](./DKNet.EfCore.AuditLogs.md) – records field-level changes. Reach for it when you need a
  history of who changed a column, remembering that an audit entry captures the plaintext value unless the property is
  marked sensitive.
- [DKNet.EfCore.DataAuthorization](./DKNet.EfCore.DataAuthorization.md) – row-level access control. Reach for it to
  decide *which rows* a caller may read; this package decides whether a stored column is readable at all.
- [DKNet.EfCore.Extensions](./DKNet.EfCore.Extensions.md) – the model-building/wiring layer for the rest of the stack.
  Reach for it for automatic entity configuration; this package's `UseColumnEncryption` is an independent
  `OnModelCreating` call.
