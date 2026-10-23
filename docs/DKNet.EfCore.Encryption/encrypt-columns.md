# Encrypt mapped columns

Part of [DKNet.EfCore.Encryption](index.md). In the [Quick Start](index.md#-quick-start), `[Encrypted]` marks `Customer.Secret` and `UseColumnEncryption` attaches its converter. With its open database, inspect the stored value:

```csharp
using var inspect = connection.CreateCommand();
inspect.CommandText = "SELECT Secret FROM Customers";
var stored = (string)inspect.ExecuteScalar()!;
Console.WriteLine(Convert.FromBase64String(stored).Length); // 38: 28 overhead + 10 UTF-8 bytes
```

![UseColumnEncryption skips unmarked and non-string properties, rejects marked primary or foreign keys, then attaches a ColumnEncryptionConverter to each accepted column.](diagrams/encrypt-columns.svg)

`EncryptedAttribute` is a property marker without options. `UseColumnEncryption` scans mapped strings with CLR property metadata, asks `IEncryptionKeyProvider.GetKey` for the property's declaring type, and installs the AES-GCM converter. It throws `ArgumentNullException` for null builder/provider arguments.

## ⚙️ Configuration reference

| Setting | Type | Default | Effect |
|---|---|---|---|
| `[Encrypted]` | Property attribute | Absent | Selects a string property for automatic conversion. |
| `UseColumnEncryption(keys)` | ModelBuilder call | Not called | Installs the selected converters. |

The AES-GCM format is Base64 of a 12-byte random IV, 16-byte authentication tag, then ciphertext. Every encryption gets a new IV; null/empty strings remain unchanged.

## 🧱 Where it fits

See the [column architecture](index.md#-architecture) for the model-to-database boundary. [AuditLogs](../DKNet.EfCore.AuditLogs/redact-sensitive-values.md) recognizes `EncryptedAttribute` by name and redacts its field values without a project dependency on this package.

## ⚠️ Gotchas & limits

- Marked primary/foreign keys throw `InvalidOperationException` during model building. Non-string and metadata-only shadow properties are skipped.
- Randomized ciphertext does not preserve plaintext equality, ordering, ranges or LIKE semantics in SQL. Query on another column and compare decrypted values in memory when appropriate.
- Size columns for UTF-8 bytes plus 28 bytes, Base64 encoded; plaintext character limits alone can truncate encrypted data.
- Invalid Base64 throws `FormatException`; a decoded payload shorter than 28 bytes throws `ArgumentException`; an authentication failure throws `InvalidOperationException`.
- A later HasConversion/SetValueConverter on the same property can replace this converter.
