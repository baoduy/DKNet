# Encrypt mapped columns

Part of [DKNet.EfCore.Encryption](index.md). In the [Quick Start](index.md#-quick-start), `[Encrypted]` marks `Customer.Secret` and `UseColumnEncryption` attaches its converter. With its open database, inspect the stored value:

```csharp
using var inspect = connection.CreateCommand();
inspect.CommandText = "SELECT Secret FROM Customers";
var stored = (string)inspect.ExecuteScalar()!;
Console.WriteLine(Convert.FromBase64String(stored).Length); // 38: 28 overhead + 10 UTF-8 bytes
```

![UseColumnEncryption refuses an [Encrypted] mark on a complex-type member, a non-string member, a primary key or a foreign key, then attaches a ColumnEncryptionConverter to each marked string on an entity or owned type.](diagrams/encrypt-columns.svg)

`EncryptedAttribute` is a property marker without options. `UseColumnEncryption` checks the mapped members of every entity and owned type for the attribute, asks `IEncryptionKeyProvider.GetKey` for each marked string's declaring type, and installs the AES-GCM converter. It throws `ArgumentNullException` for null builder/provider arguments, and `InvalidOperationException` for a mark it cannot honour (see [Gotchas & limits](#-gotchas--limits)).

## ⚙️ Configuration reference

| Setting | Type | Default | Effect |
|---|---|---|---|
| `[Encrypted]` | Property attribute | Absent | Selects a string property of an entity or owned type for automatic conversion. |
| `UseColumnEncryption(keys)` | ModelBuilder call | Not called | Installs the selected converters. |

The AES-GCM format is Base64 of a 12-byte random IV, 16-byte authentication tag, then ciphertext. Every encryption gets a new IV; null/empty strings remain unchanged.

## 🧱 Where it fits

See the [column architecture](index.md#-architecture) for the model-to-database boundary. [AuditLogs](../DKNet.EfCore.AuditLogs/redact-sensitive-values.md) recognizes `EncryptedAttribute` by name and redacts its field values without a project dependency on this package.

## ⚠️ Gotchas & limits

- `UseColumnEncryption` throws `InvalidOperationException` during model building for an `[Encrypted]` mark it cannot honour. Unmarked members and members EF Core does not map are skipped.
  - On a member of an EF Core complex type (table-split, nested, JSON-mapped or a complex collection), or on a complex property itself: `Property '<Type>.<Property>' is marked with [Encrypted] but is a complex type or a member of one, which is not supported. Move it to an entity or owned type as a string property, or remove the attribute.`
  - On a mapped member whose type is not `string` — for example `int`, `byte[]`, an enum, or a navigation, owned ones included: `Property '<Type>.<Property>' is marked with [Encrypted] but is of type '<ClrType>', which is not supported. Only string properties can be encrypted.` `<ClrType>` is the CLR short name, so an `int` member shows `Int32`.
  - On a string primary or foreign key: `Property '<Type>.<Property>' is marked with [Encrypted] but is a primary key or foreign key column, which is not supported.` The type check runs first, so a marked `int` key gets the non-string message.
- **Upgrading from 13.3.0 or earlier.** Those releases skipped a mark on a complex-type member, a complex property or a non-string member, and stored its value as plaintext. The same model now fails at the first model build with one of the messages above. Upgrading changes no stored data. Two ways out:
  - Remove the attribute. The value stays plaintext, as it already was.
  - Move the field to an entity or owned type as a `string` property; a non-string member changes its type, and its column, to `string` too. The values already stored for it are plaintext, and a read through the encrypted mapping does not accept plaintext: the whole query throws (see the decrypt errors below). Encrypt the existing values outside EF Core before the app reads them: read the raw column, pass each value to `new AesGcmColumnEncryptionProvider(key).Encrypt(value)`, where `key` is what `IEncryptionKeyProvider.GetKey` returns for the class that now declares the property, and write the result back. Null and empty values stay as they are. The same out-of-band pass is described for key changes in [Supply encryption keys](supply-encryption-keys.md#-gotchas--limits).
- Randomized ciphertext does not preserve plaintext equality, ordering, ranges or LIKE semantics in SQL. Query on another column and compare decrypted values in memory when appropriate.
- Size columns for UTF-8 bytes plus 28 bytes, Base64 encoded; plaintext character limits alone can truncate encrypted data.
- Invalid Base64 throws `FormatException`; a decoded payload shorter than 28 bytes throws `ArgumentException`; an authentication failure throws `InvalidOperationException`.
- AES-GCM runs without associated data, so a ciphertext is not bound to its row or column. A valid value copied to another row or column under the same key decrypts without error.
- A later HasConversion/SetValueConverter on the same property can replace this converter.
