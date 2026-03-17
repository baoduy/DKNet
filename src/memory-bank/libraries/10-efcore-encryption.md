# DKNet.EfCore.Encryption — AI Skill File

> **Package**: `DKNet.EfCore.Encryption`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/EfCore/DKNet.EfCore.Encryption/`

---

## Purpose

Provides transparent AES-GCM column-level encryption for EF Core string properties: data is encrypted before writing to the database and decrypted on read, with no changes required to query code.

---

## When To Use

- ✅ Storing PII (email, phone, national ID, passport number)
- ✅ Storing financial secrets (account numbers, card tokens)
- ✅ Any column that must be encrypted at rest per compliance requirements (GDPR, PCI-DSS)

## When NOT To Use

- ❌ Encrypting the entire row — use Transparent Data Encryption (TDE) at the SQL Server level instead
- ❌ Columns you need to query with `LIKE`, range comparisons, or sorting — encrypted values are opaque blobs; equality-only lookups work
- ❌ Non-string properties — only `string?` columns are supported

---

## Installation

```bash
dotnet add package DKNet.EfCore.Encryption
```

---

## Setup / DI Registration

```csharp
// 1. Apply [EncryptedString] attribute to string properties on the entity
public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [EncryptedString]   // ← transparent encrypt/decrypt
    public string? Email { get; set; }

    [EncryptedString]
    public string? PhoneNumber { get; set; }
}

// 2. Register the encryption provider in DbContext OnModelCreating
public class AppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Key must come from IConfiguration — never hardcoded
        var key = Configuration["Encryption:Key"]!;
        modelBuilder.UseEncryption(key);
        base.OnModelCreating(modelBuilder);
    }
}
```

---

## Key API Surface

| Type / Method | Role |
|---|---|
| `[EncryptedString]` | Attribute — apply to `string?` properties to opt-in to encryption |
| `modelBuilder.UseEncryption(key)` | Registers the AES-GCM value converter on all `[EncryptedString]` properties |

---

## Usage Pattern

```csharp
// Entity with encrypted PII fields
public class Patient
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;   // not encrypted

    [EncryptedString]
    public string? NationalId { get; set; }                 // encrypted at rest

    [EncryptedString]
    public string? Email { get; set; }                      // encrypted at rest
}

// AppDbContext
public class AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration config)
    : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseEncryption(config["Encryption:Key"]!);
        base.OnModelCreating(modelBuilder);
    }
}

// appsettings.json (key stored in Key Vault in production)
{
  "Encryption": {
    "Key": "base64-encoded-32-byte-key-here"
  }
}

// Usage in handler — no encryption code needed; fully transparent
public async Task<PatientDto?> Handle(GetPatientQuery request, CancellationToken ct)
    => await _repo.Query(p => p.Id == request.Id)
                  .Select(p => new PatientDto(p.Id, p.FirstName, p.Email))  // ← Email auto-decrypted
                  .FirstOrDefaultAsync(ct);
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — hardcoding the encryption key
modelBuilder.UseEncryption("my-secret-key-12345");

// ✅ CORRECT — load from configuration / Key Vault
modelBuilder.UseEncryption(config["Encryption:Key"]!);

// ❌ WRONG — applying [EncryptedString] to a column used in LIKE / range queries
[EncryptedString]
public string? ProductCode { get; set; }
// Then in a spec:
WithFilter(p => EF.Functions.Like(p.ProductCode!, "%ABC%"));  // ← cannot LIKE an encrypted blob

// ✅ CORRECT — only encrypt columns queried only by exact equality
// Use a separate non-encrypted search token column if LIKE is needed

// ❌ WRONG — forgetting UseEncryption in OnModelCreating (attribute alone does nothing)
public class AppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // missing modelBuilder.UseEncryption(...)  ← [EncryptedString] is silently ignored!
    }
}
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.EfCore.Extensions` | `UseAutoConfigModel` applies entity configuration; run `UseEncryption` after |
| `DKNet.EfCore.AuditLogs` | Audit logs capture field-level changes; encrypted values appear as ciphertext in audit records — mask or exclude sensitive fields from audit |

---

## Security Notes

- **Key management**: The encryption key MUST be stored in Azure Key Vault, AWS Secrets Manager, or equivalent — never in `appsettings.json` in production.
- **Key rotation**: AES-GCM does not support in-place key rotation. Plan a migration strategy before rotating keys.
- **Null handling**: `null` and empty strings are stored as-is (unencrypted). Validate input before persisting.
- **FIPS compliance**: AES-GCM is FIPS 140-2 approved. Verify your .NET runtime is running in FIPS mode if required.

---

## Test Example

```csharp
// Uses TestContainers.MsSql-backed integration test environment.
[Fact]
public async Task EncryptedField_StoredAsCiphertext_DecryptedOnRead()
{
    // Arrange
    await using var db = CreateEncryptedDbContext(_container.GetConnectionString(), testKey: "base64key==");
    var patient = new Patient { NationalId = "123-45-6789" };
    db.Patients.Add(patient);
    await db.SaveChangesAsync();

    // Assert — raw SQL shows encrypted value
    var raw = await db.Database.SqlQueryRaw<string>(
        "SELECT NationalId FROM Patients WHERE Id = {0}", patient.Id)
        .FirstAsync();
    raw.ShouldNotBe("123-45-6789");   // ciphertext stored

    // Assert — EF Core decrypts transparently
    db.ChangeTracker.Clear();
    var loaded = await db.Patients.FindAsync(patient.Id);
    loaded!.NationalId.ShouldBe("123-45-6789");
}
```

---

## Quick Decision Guide

- Encrypt string fields that must be protected at rest.
- Keep searchable/sortable fields unencrypted or add companion search tokens.
- Load encryption keys from secret stores, never source code.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation — `[EncryptedString]` attribute, `modelBuilder.UseEncryption(key)` |

