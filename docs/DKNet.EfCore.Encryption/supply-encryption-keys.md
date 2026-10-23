# Supply encryption keys

Part of [DKNet.EfCore.Encryption](index.md). Replace the demo-only key class in the Quick Start with a stable external value. Set `APP_ENCRYPTION_KEY` to Base64-encoded key bytes before running:

```csharp
public sealed class DemoKey : IEncryptionKeyProvider
{
    private readonly byte[] _key = Convert.FromBase64String(
        Environment.GetEnvironmentVariable("APP_ENCRYPTION_KEY")
        ?? throw new InvalidOperationException("APP_ENCRYPTION_KEY is required."));
    public byte[] GetKey(Type entityType) => _key;
}
```

![AddEfCoreEncryption registers the first singleton key provider, and model construction captures its GetKey result in an AES-GCM provider for each marked property.](diagrams/supply-encryption-keys.svg)

`IEncryptionKeyProvider` has one method, `byte[] GetKey(Type entityType)`. There is no supplied implementation. `AddEfCoreEncryption<TKeyServiceImplementation>()` uses `TryAddSingleton`, so a prior registration is retained.

## ⚙️ Configuration reference

| Input | Type | Default | Effect |
|---|---|---|---|
| `GetKey(entityType)` result | `byte[]` | Required | Must contain 16, 24 or 32 bytes. |
| `AesGcmColumnEncryptionProvider(key)` | `byte[]` constructor input | Required | Null throws `ArgumentNullException`; other lengths throw `ArgumentException`. |

The environment variable in the example is application code, not a configuration convention supplied by this package. Key lookup receives the CLR property's declaring type, not a property name.

## 🧱 Where it fits

The [package architecture](index.md#-architecture) shows key sourcing in the host. EF model caching means a changed provider value does not update an already-built converter.

## ⚠️ Gotchas & limits

- Keep key bytes stable and treat returned arrays as immutable: the AES provider retains the supplied array.
- Different keys may be selected by declaring type. Properties declared on the same type have no separate key identifier input.
- No key rotation, ciphertext versioning or dual-key decrypt support is implemented. Existing data needs explicit decryption with its old key and re-encryption with the new key, as well as rebuilt model converters.
- Losing the key prevents this provider from reading persisted ciphertext. A restart with the Quick Start's random demo key cannot recover a persisted database.
