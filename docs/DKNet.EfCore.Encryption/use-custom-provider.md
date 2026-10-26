# Use a custom encryption provider

Part of [DKNet.EfCore.Encryption](index.md). `IColumnEncryptionProvider` defines `Encrypt(string?)` and `Decrypt(string?)`. For an explicit converter, replace the Quick Start `UseColumnEncryption` call with:

```csharp
var key = keys.GetKey(typeof(Customer));
DKNet.EfCore.Encryption.Interfaces.IColumnEncryptionProvider algorithm =
    new AesGcmColumnEncryptionProvider(key);
modelBuilder.Entity<Customer>().Property(c => c.Secret).HasConversion(
    new DKNet.EfCore.Encryption.Converters.ColumnEncryptionConverter(algorithm));
```

![An explicitly configured ColumnEncryptionConverter delegates model-to-provider values to Encrypt and provider-to-model values to Decrypt on the selected IColumnEncryptionProvider.](diagrams/use-custom-provider.svg)

Implement that interface to substitute another algorithm in the explicit `HasConversion` path. The example uses the bundled AES-GCM provider to show the same wiring without inventing an algorithm.

## 🧱 Where it fits

![The host selects an IColumnEncryptionProvider and attaches its ColumnEncryptionConverter directly to an EF property.](diagrams/provider-components.svg)

Automatic `[Encrypted]` scanning hardcodes `AesGcmColumnEncryptionProvider`; registering another algorithm in DI does not replace it.

## ⚠️ Gotchas & limits

- Explicit conversion does not run UseColumnEncryption's primary/foreign-key guard. Preserve the supported string, non-key column constraints yourself.
- Use either automatic scanning or explicit conversion for a given property. A later converter assignment replaces the earlier one.
- A custom provider owns its own ciphertext format, null/empty handling and failure behavior; the bundled AES behavior is not guaranteed for another implementation.
