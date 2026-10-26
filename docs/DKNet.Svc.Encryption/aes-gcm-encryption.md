# AES-GCM authenticated encryption

Part of [DKNet.Svc.Encryption](index.md). `IAesGcmEncryption` packages AES-256-GCM encryption into a single opaque
Base64 string, with a fresh random nonce on every call so ciphertext is never deterministic.

```csharp
using DKNet.Svc.Encryption.Ciphers;

using var aes = new AesGcmEncryption(); // keep aes.Key to decrypt later
var package = aes.EncryptString("secret-value");
var plain = aes.DecryptString(package);
Console.WriteLine(plain); // secret-value
```

`new AesGcmEncryption()` generates a random 256-bit key; `new AesGcmEncryption(existingBase64Key)` reconstructs an
instance from a previously persisted key (128/192/256-bit), rejecting a key containing `:` — that shape belonged to
the removed AES-CBC type's `key:iv` format. The returned package is Base64 of `nonce:tag:cipher` (each part itself
Base64) — treat it as opaque and pass it around whole.

Pass `associatedData` (AAD) when tamper detection should cover context outside the ciphertext:

```csharp
var aad = System.Text.Encoding.UTF8.GetBytes("order-42");
var withAad = aes.EncryptString("secret-value", aad);

try
{
    aes.DecryptString(withAad, System.Text.Encoding.UTF8.GetBytes("order-99"));
}
catch (System.Security.Cryptography.CryptographicException)
{
    Console.WriteLine("AAD mismatch rejected");
}
```

![Encrypt(text, key) checks the supplied key against the instance key with FixedTimeEquals before encrypting; a match runs EncryptString to produce a nonce:tag:cipher package, a mismatch throws InvalidOperationException with no re-keying.](diagrams/aes-gcm-encryption.svg)

The diagram follows the overloads that take an explicit `base64Key` (`Encrypt`/`Decrypt`); the single-argument
`EncryptString`/`DecryptString` shown above skip the key check entirely because they always use the instance's own
key. It cannot show the AAD mismatch path — see the example above for that branch.

## ⚙️ Configuration reference

| Member | Parameter | Default | Effect |
|---|---|---|---|
| `AddAesGcmEncryption(base64Key)` | Base64 128/192/256-bit key | — | Registers `IAesGcmEncryption` as a singleton; throws `ArgumentException` on a blank key. |
| `new AesGcmEncryption(string? key = null)` | Base64 key, no `:` | `null` | `null` generates a random 256-bit key; a supplied key must decode to 16, 24, or 32 bytes. |
| `EncryptString`/`DecryptString` | `associatedData` | `null` | Extra bytes bound into the authentication tag; a mismatch on decrypt throws `CryptographicException`. |
| `Encrypt`/`Decrypt` | `base64Key` | — | Compared against the instance key via `KeyMatches`; throws `InvalidOperationException` on a mismatch. |

Fixed, non-configurable choices: a 12-byte nonce and a 16-byte tag per call, a 256-bit generated key, and the
package layout `base64(nonce):base64(tag):base64(cipher)`.

## 🧱 Where it fits

`AddAesGcmEncryption` is one of two key-bearing cipher registrations shown in the [package architecture](index.md);
only the registration that receives a key produces a singleton cipher. A hand-built `new AesGcmEncryption()` with no
key sits outside DI entirely and generates its own throwaway key.

## ⚠️ Gotchas & limits

- **AES-CBC (`IAesEncryption`, `AesEncryption`, `EncryptionSetup.AddAesEncryption`) has been removed, not
  deprecated, and there is no compatibility shim.** The old type kept a fixed IV embedded in the key, so encrypting
  the same plaintext twice always produced the same ciphertext — a real leak of information about the data, not a
  theoretical concern. `IAesGcmEncryption` is authenticated and uses a fresh random nonce per call; it is the only
  symmetric cipher this package ships. Code still calling `AddAesEncryption` or constructing `AesEncryption` will
  not build. **Ciphertext produced by the removed AES-CBC type has no conversion path** — re-encrypt the underlying
  plaintext under `IAesGcmEncryption` and switch the call sites; there is no way to migrate existing AES-CBC
  ciphertext in place.
- **Cipher registrations are singletons over the key you passed in.** One `AddAesGcmEncryption` call means one key
  for the whole application; rotating it means new configuration and a restart.
- **AES-GCM instances serialize their crypto calls.** `Encrypt`/`Decrypt` take a lock on the shared `AesGcm` handle,
  so one instance is thread-safe but not concurrent; resolve per unit of work if throughput matters.
- **String-only surface.** No stream, file, or `byte[]` overloads — a large payload is fully materialized as a UTF-8
  string and again as Base64.
- **Dispose what you construct.** `AesGcmEncryption` holds a native handle; DI disposes a singleton registration,
  but a hand-built instance needs a `using`.
