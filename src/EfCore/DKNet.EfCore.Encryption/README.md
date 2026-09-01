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

Full documentation, configuration notes, and gotchas (query limitations on encrypted columns, key rotation, ciphertext sizing): https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Encryption.md
