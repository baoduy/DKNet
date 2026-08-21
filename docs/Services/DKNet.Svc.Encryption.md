# DKNet.Svc.Encryption

Standalone, explicitly-invoked cryptography toolkit: symmetric encryption (AES-GCM, plus an obsolete AES-CBC path),
RSA encryption/signing, HMAC and SHA hashing, and Base64/Base64URL helpers. Every operation works on UTF-8 strings in
and Base64 strings out — there is no stream, field, or attribute-driven encryption here.

> Looking for **transparent EF Core column encryption** instead — where a `[Encrypted]` attribute on an entity
> property does the work automatically on save/load? That's a different package: **`DKNet.EfCore.Encryption`**. Use
> `DKNet.Svc.Encryption` when you want to call `Encrypt`/`Decrypt` yourself; use `DKNet.EfCore.Encryption` when you
> want a column encrypted without touching the code that reads/writes the entity. The two don't share
> implementation — `DKNet.EfCore.Encryption`'s AES-GCM provider has its own key-provider abstraction.

## When to reach for it

Use this package whenever application code needs to encrypt a value, hash a password reset token, sign a webhook
payload, or verify an HMAC — anywhere the call site controls exactly when encryption happens.

## Install and minimal wiring

```bash
dotnet add package DKNet.Svc.Encryption
```

```csharp
using DKNet.Svc.Encryption;

builder.Services.AddEncryptionServices(); // registers AES-GCM, the obsolete AES-CBC, HMAC, and SHA services
```

```csharp
public sealed class SecretStore(IAesGcmEncryption aesGcm)
{
    public string Protect(string plainText) => aesGcm.EncryptString(plainText);
    public string Reveal(string cipherPackage) => aesGcm.DecryptString(cipherPackage);
}
```

## Features

### AES-GCM (`IAesGcmEncryption`) — the encryption you want

```csharp
string EncryptString(string plainText, byte[]? associatedData = null);
string DecryptString(string cipherPackage, byte[]? associatedData = null);
string Encrypt(string plainText, string base64Key, byte[]? associatedData = null);
string Decrypt(string cipherPackage, string base64Key, byte[]? associatedData = null);
string Key { get; } // Base64, persist this to decrypt later
```

`new AesGcmEncryption()` generates a random 256-bit key; `new AesGcmEncryption(existingBase64Key)` reconstructs an
instance from a previously persisted key. Every call produces a fresh random 12-byte nonce + 16-byte tag, so
ciphertext is never deterministic even for the same plaintext. Pass `associatedData` (AAD) when you want tamper
detection tied to context outside the ciphertext itself — decrypting with different AAD throws
`CryptographicException`. The `Encrypt`/`Decrypt` wrapper overloads throw `InvalidOperationException` if the
`base64Key` you pass doesn't match the instance's own key — they never silently re-key.

### RSA (`IRsaEncryption`) — asymmetric encrypt/sign

```csharp
new RsaEncryption(int keySize = 2048);           // generates a new key pair
new RsaEncryption(string privateKeyBase64);       // loads an existing private key
RsaEncryption.FromPublicKey(string publicKeyBase64); // public-only instance

string Encrypt(string plainText);   // OAEP-SHA256
string Decrypt(string base64CipherText);
string Sign(string data);           // PKCS1 + SHA256
bool Verify(string data, string base64Signature);
string PublicKey { get; }
string? PrivateKey { get; } // null on a public-only instance
```

A public-only instance (from `FromPublicKey`) can `Encrypt`/`Verify` but throws `InvalidOperationException` on
`Decrypt`/`Sign` — use it on the side that only needs to send or verify, never the private key.

### HMAC (`IHmacHashing`) and SHA (`IShaHashing`) hashing

```csharp
string ComputeSha256(string message, string secretKey, bool asBase64 = true);
string ComputeSha512(string message, string secretKey, bool asBase64 = true);
bool VerifySha256(string message, string secretKey, string expectedSignature, bool signatureIsBase64 = true, bool ignoreCase = true);
bool VerifySha512(string message, string secretKey, string expectedSignature, bool signatureIsBase64 = true, bool ignoreCase = true);
```

```csharp
string ComputeSha256(string input, bool upperCase = false);
string ComputeSha512(string input, bool upperCase = false);
bool VerifySha256(string input, string expectedHex, bool ignoreCase = true);
bool VerifySha512(string input, string expectedHex, bool ignoreCase = true);
```

Both verify methods compare in constant time (`CryptographicOperations.FixedTimeEquals`). Note the `ignoreCase`
parameter on every `Verify*` overload is accepted but has **no effect** — comparison is always exact; don't rely on
it for case-insensitive hex/Base64 comparison.

### Base64 / Base64URL helpers

`Base64StringExtensions` exposes plain static methods (not extension methods, despite the name):
`ToBase64String`, `FromBase64String`, `ToBase64UrlString`, `FromBase64UrlString`, `IsBase64String`. Useful for
JWT-style Base64URL payloads without pulling in a JWT library. The older `Base65StringExtensions` (note the
misspelling) is `[Obsolete]` and forwards to the same logic — migrate off it.

### AES-CBC (`IAesEncryption`) — obsolete, do not adopt

`[Obsolete]` on both the interface and implementation: "Uses AES-CBC which is vulnerable to padding oracle attacks.
Use `IAesGcmEncryption` instead." It's registered by `AddEncryptionServices` only for existing consumers migrating
off it — its IV is fixed per instance, so identical plaintext always produces identical ciphertext. New code should
use `IAesGcmEncryption`.

## Registration

```csharp
services.AddEncryptionServices();                 // IAesEncryption, IAesGcmEncryption, IHmacHashing, IShaHashing — all transient
services.AddRsaEncryption(privateKeyBase64);       // IRsaEncryption — singleton, built from the key you pass
```

`AddEncryptionServices` does **not** register `IRsaEncryption` — call `AddRsaEncryption` separately with the key you
want that singleton built from (throws `ArgumentException` for a null/empty/whitespace key). Both methods are
idempotent — calling either twice does not double-register.

## Composing with other DKNet packages

Independent of EF Core, blob storage, and messaging — call it directly from any layer that needs it. Pair with
[`DKNet.EfCore.Encryption`](https://github.com/baoduy/DKNet) when you additionally need transparent column
encryption; the two are complementary, not overlapping.

## Gotchas and limits

- No password-based key derivation anywhere in this package — if you need to encrypt with a user-supplied password
  rather than a generated/stored key, derive a key yourself (e.g. via `Rfc2898DeriveBytes`) before calling
  `AesGcmEncryption`. An earlier revision of this page documented a `PasswordAesEncryption` type; it does not exist
  in source.
- `ignoreCase` on every HMAC/SHA `Verify*` overload is a no-op — don't depend on it.
- AES-GCM's `Encrypt`/`Decrypt` wrapper methods require the passed key to match the instance's own key exactly; they
  raise `InvalidOperationException` rather than re-keying.
- Keys and key pairs are not persisted or rotated by this package — persist `Key`/`PrivateKey`/`PublicKey` yourself.
