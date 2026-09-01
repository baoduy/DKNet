# DKNet.EfCore.Encryption

Transparent, column-level encryption for EF Core `string` properties. Mark a property `[Encrypted]`, register a key provider, and EF Core encrypts values with AES-GCM before they hit the database and decrypts them on read — application code always sees plaintext.

```bash
dotnet add package DKNet.EfCore.Encryption
```

## Features

- `[Encrypted]` attribute — opt-in marker for any `string` property.
- `AesGcmColumnEncryptionProvider` — default AES-128/192/256-GCM implementation (random IV per value, authenticated encryption).
- `IColumnEncryptionProvider` — swappable encryption algorithm abstraction.
- `IEncryptionKeyProvider` / `EncryptionKeyProvider` — bring your own key source (config, env var, Key Vault, etc.), one key per entity type.
- `ModelBuilder.UseColumnEncryption(...)` — one call in `OnModelCreating` wires up every `[Encrypted]` property automatically; rejects primary/foreign key columns.
- `services.AddEfCoreEncryption<TKeyProvider>()` — registers your key provider in DI.

## Quick start

```csharp
public sealed class AppEncryptionKeyProvider : EncryptionKeyProvider
{
    private readonly byte[] _key = Convert.FromBase64String(
        Environment.GetEnvironmentVariable("APP_ENCRYPTION_KEY")!); // 16, 24, or 32 bytes

    public override byte[] GetKey(Type entityType) => _key;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEfCoreEncryption<AppEncryptionKeyProvider>();

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

## Customisation reference

There is no options class and nothing is bound from `appsettings.json` — the customisation surface is the key
provider you write plus two wiring calls.

| Knob | Type | Default | Effect |
|---|---|---|---|
| `[Encrypted]` | property attribute | — | Opts a `string` property in. `AttributeTargets.Property`; nothing else is scanned. |
| `IEncryptionKeyProvider.GetKey(Type entityType)` | `byte[]` | none — you implement it | The AES key for every `[Encrypted]` property on that entity type. Evaluated once per property, at model-build time. |
| `AesGcmColumnEncryptionProvider(byte[] key)` | `byte[]` | required | Key material. Exactly 16, 24 or 32 bytes; anything else throws `ArgumentException`, `null` throws `ArgumentNullException`. |
| `IColumnEncryptionProvider` | interface | `AesGcmColumnEncryptionProvider` | Implement it to swap the algorithm; `ColumnEncryptionConverter` takes any implementation. |
| `AddEfCoreEncryption<TKeyProvider>()` | `IServiceCollection` extension | — | Registers `TKeyProvider` as the singleton `IEncryptionKeyProvider`, and only when one is not already registered. |
| `ModelBuilder.UseColumnEncryption(keyProvider)` | `ModelBuilder` extension | — | Must be called from `OnModelCreating`. Without it `[Encrypted]` has no effect at all. |

Ciphertext layout is fixed: Base64 of a 12-byte random IV, a 16-byte GCM tag, and the ciphertext, in that order.
`null` and empty strings pass through unchanged. Marking a primary or foreign key `[Encrypted]` throws
`InvalidOperationException` while the model is being built. There is no key rotation, versioned ciphertext, or
per-property key support.

Full documentation, configuration notes, and gotchas (query limitations on encrypted columns, key rotation, ciphertext sizing): https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Encryption.md
