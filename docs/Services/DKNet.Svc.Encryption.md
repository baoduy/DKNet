# DKNet.Svc.Encryption

Explicitly-invoked cryptography toolkit for application code — AES-GCM and RSA encryption, RSA signing, HMAC and SHA
hashing, and Base64/Base64URL helpers, all string-in/string-out.

> [!NOTE]
> Looking for **transparent EF Core column encryption** instead — where an attribute on an entity property does the work
> automatically on save/load? That's a different package:
> [`DKNet.EfCore.Encryption`](../EfCore/DKNet.EfCore.Encryption.md). Use `DKNet.Svc.Encryption` when you want to call
> `Encrypt`/`Decrypt` yourself; use `DKNet.EfCore.Encryption` when you want a column encrypted without touching the code
> that reads and writes the entity. The two don't share implementation — `DKNet.EfCore.Encryption`'s AES-GCM provider has
> its own key-provider abstraction.

## ✨ Why use it?

- **Authenticated encryption without the ceremony.** `IAesGcmEncryption` handles nonce generation, tag handling, and
  packaging into a single Base64 string, so call sites never assemble a cipher envelope by hand.
- **Correct-by-default primitives.** Fresh random nonce per call, OAEP-SHA256 for RSA, constant-time comparison on every
  `Verify*` — the choices that are easy to get wrong are already made.
- **UTF-8 strings in, Base64 strings out.** Values fit straight into JSON payloads, configuration, headers, or a
  `varchar` column with no byte-array plumbing.
- **Nothing else comes with it.** No EF Core, no hosting, no storage dependency — usable from a domain service, a console
  app, or a background worker.

Reach for it whenever application code needs to encrypt a value, hash a token, sign a webhook payload, or verify an
HMAC — anywhere the call site should control exactly when encryption happens.

## 🚀 Quick Start

```bash
dotnet add package DKNet.Svc.Encryption
```

```csharp
using DKNet.Svc.Encryption;

// IAesGcmEncryption, IHmacHashing, IShaHashing (and the obsolete IAesEncryption), all transient
builder.Services.AddEncryptionServices();

// IRsaEncryption is separate — a singleton built from the private key you supply
builder.Services.AddRsaEncryption(builder.Configuration["Crypto:RsaPrivateKey"]!);
```

```csharp
public sealed class SecretStore(IAesGcmEncryption aesGcm)
{
    public string Protect(string plainText) => aesGcm.EncryptString(plainText);
    public string Reveal(string cipherPackage) => aesGcm.DecryptString(cipherPackage);
}
```

`AddEncryptionServices` does **not** register `IRsaEncryption`; `AddRsaEncryption` throws `ArgumentException` for a
null, empty, or whitespace key. Both methods are idempotent — calling either twice does not double-register.

> [!IMPORTANT]
> `AddEncryptionServices` registers `IAesGcmEncryption` as **transient**, and a transient `AesGcmEncryption` generates a
> **new random key per resolution**. That is fine for encrypt-then-decrypt inside one scope, but a value encrypted in one
> request cannot be decrypted in the next. To persist ciphertext, construct the instance from a stored key
> (`new AesGcmEncryption(storedBase64Key)`) and register that yourself, e.g.
> `services.AddSingleton<IAesGcmEncryption>(_ => new AesGcmEncryption(storedBase64Key))`.

## 🧩 Features

### AES-GCM (`IAesGcmEncryption`) — the encryption to reach for

```csharp
string EncryptString(string plainText, byte[]? associatedData = null);
string DecryptString(string cipherPackage, byte[]? associatedData = null);
string Encrypt(string plainText, string base64Key, byte[]? associatedData = null);
string Decrypt(string cipherPackage, string base64Key, byte[]? associatedData = null);
string Key { get; } // Base64, persist this to decrypt later
```

`new AesGcmEncryption()` generates a random 256-bit key; `new AesGcmEncryption(existingBase64Key)` reconstructs an
instance from a previously persisted key (accepting 128/192/256-bit keys, and rejecting a key containing `:` — that shape
belongs to the obsolete AES-CBC type). Every call produces a fresh random 12-byte nonce and 16-byte tag, so ciphertext is
never deterministic even for the same plaintext:

```csharp
using var aes = new AesGcmEncryption();          // keep aes.Key to decrypt later
var package = aes.EncryptString("4111-1111-1111-1111");
var plain = aes.DecryptString(package);
```

The returned package is Base64 of `nonce:tag:cipher` (each part itself Base64) — treat it as opaque and pass it around
whole. Pass `associatedData` (AAD) when tamper detection should cover context outside the ciphertext; decrypting with
different AAD throws `CryptographicException`. The `Encrypt`/`Decrypt` overloads that take a `base64Key` throw
`InvalidOperationException` when it does not match the instance's own key — they never silently re-key.

### RSA (`IRsaEncryption`) — asymmetric encrypt and sign

```csharp
new RsaEncryption(int keySize = 2048);               // generates a new key pair
new RsaEncryption(string privateKeyBase64);          // loads an existing private key (PKCS#1 DER, Base64)
RsaEncryption.FromPublicKey(string publicKeyBase64); // public-only instance

string Encrypt(string plainText);   // OAEP-SHA256
string Decrypt(string base64CipherText);
string Sign(string data);           // PKCS#1 v1.5 + SHA256
bool Verify(string data, string base64Signature);
string PublicKey { get; }
string? PrivateKey { get; }         // null on a public-only instance
```

A public-only instance can `Encrypt` and `Verify` but throws `InvalidOperationException` on `Decrypt`/`Sign` — deploy it
on the side that only sends or verifies, and keep the private key off that side entirely:

```csharp
// signing side (has the private key)
using var signer = new RsaEncryption(privateKeyBase64);
var signature = signer.Sign(payload);

// verifying side (public key only)
using var verifier = RsaEncryption.FromPublicKey(publicKeyBase64);
var ok = verifier.Verify(payload, signature);
```

`PublicKey` and `PrivateKey` are Base64 of the raw PKCS#1 structures (`ExportRSAPublicKey` / `ExportRSAPrivateKey`) —
not PEM, so don't paste them into a `-----BEGIN` block without re-encoding.

### HMAC signatures (`IHmacHashing`)

```csharp
string ComputeSha256(string message, string secretKey, bool asBase64 = true);
string ComputeSha512(string message, string secretKey, bool asBase64 = true);
bool VerifySha256(string message, string secretKey, string expectedSignature, bool signatureIsBase64 = true, bool ignoreCase = true);
bool VerifySha512(string message, string secretKey, string expectedSignature, bool signatureIsBase64 = true, bool ignoreCase = true);
```

Set `asBase64 = false` to get upper-case hex instead, and match that choice with `signatureIsBase64` on the verify call —
a mismatched encoding returns `false` rather than throwing. The key bytes are zeroed after each computation, and
comparison runs through `CryptographicOperations.FixedTimeEquals`:

```csharp
// verifying an inbound webhook signature
var trusted = hmac.VerifySha256(rawBody, webhookSecret, header["X-Signature"]!);
```

Blank `message`, `secretKey`, or `expectedSignature` throws `ArgumentException`.

### Content hashes (`IShaHashing`)

```csharp
string ComputeSha256(string input, bool upperCase = false);
string ComputeSha512(string input, bool upperCase = false);
bool VerifySha256(string input, string expectedHex, bool ignoreCase = true);
bool VerifySha512(string input, string expectedHex, bool ignoreCase = true);
```

Output is hex, lower-case unless `upperCase` is set. Verification is hex-only (`expectedHex`), constant-time, and returns
`false` — instead of throwing — when the expected value isn't valid hex. Empty string input is allowed; `null` throws.

### Base64 / Base64URL helpers

`Base64StringExtensions` exposes plain static methods (not extension methods, despite the name): `ToBase64String`,
`FromBase64String`, `ToBase64UrlString`, `FromBase64UrlString`, and `IsBase64String`. The URL variants are useful for
JWT-style payloads without pulling in a JWT library:

```csharp
var token = Base64StringExtensions.ToBase64UrlString(payloadJson);
var back = Base64StringExtensions.FromBase64UrlString(token);
```

Whitespace or empty input returns `string.Empty` from both decode helpers, and `IsBase64String` returns `false` for it.
The older `Base65StringExtensions` (note the misspelling) is `[Obsolete]`, exposes the same operations as real extension
methods, and forwards to the same logic — migrate off it.

### AES-CBC (`IAesEncryption`) — obsolete, do not adopt

`[Obsolete]` on both the interface and the implementation: "Uses AES-CBC which is vulnerable to padding oracle attacks.
Use `IAesGcmEncryption` instead." It stays registered by `AddEncryptionServices` only for consumers still migrating off
it. Its IV is fixed per instance, so identical plaintext always produces identical ciphertext, and its `Key` packs
`key:iv` into one Base64 string — a shape `AesGcmEncryption` explicitly rejects. New code uses `IAesGcmEncryption`.

## 🧱 Where it fits

- **[DKNet.EfCore.Encryption](../EfCore/DKNet.EfCore.Encryption.md)** — the transparent counterpart: encrypt a column
  without the calling code knowing. Use this package when the call site should decide; use that one when the storage
  layer should.
- **[DKNet.Svc.BlobStorage.Abstractions](./DKNet.Svc.BlobStorage.Abstractions.md)** — encrypt a payload here before
  handing the bytes to `SaveAsync` when the store itself must never see plaintext.
- **The rest of DKNet** — this package depends on nothing beyond `Microsoft.Extensions.DependencyInjection.Abstractions`,
  so it can be called from a domain service, a handler, or a hosted worker without pulling in EF Core or messaging.

## ⚠️ Gotchas & limits

- **Transient AES-GCM registration means a per-resolution random key.** Ciphertext produced by one resolved instance
  cannot be decrypted by the next unless you register an instance built from a stored key — see the Quick Start note.
- **No password-based key derivation.** To encrypt with a user-supplied password, derive a key yourself (e.g.
  `Rfc2898DeriveBytes`) before constructing `AesGcmEncryption`. An earlier revision of this page documented a
  `PasswordAesEncryption` type; it does not exist in source.
- **`ignoreCase` on every HMAC/SHA `Verify*` overload is a no-op.** Comparison is done on decoded bytes, so case never
  matters — don't read the parameter as a behavior switch.
- **`Key`, `PrivateKey`, and `PublicKey` are yours to persist and rotate.** The package stores nothing and has no key
  rotation, versioning, or envelope-key support.
- **String-only surface.** No stream, file, or `byte[]` overloads — a large payload is fully materialized as a UTF-8
  string and again as Base64.
- **AES-GCM instances serialize their crypto calls.** `Encrypt`/`Decrypt` take a lock on the shared `AesGcm` handle, so
  one instance is thread-safe but not concurrent; resolve per unit of work if throughput matters.
- **Dispose what you construct.** `AesGcmEncryption` and `RsaEncryption` hold native handles; DI disposes the transient
  and singleton registrations, but a hand-built instance needs a `using`.

## 🔗 Related packages

- [DKNet.EfCore.Encryption](../EfCore/DKNet.EfCore.Encryption.md) – reach for it when a column should be encrypted
  transparently on save and decrypted on load.
- [DKNet.Svc.BlobStorage.Abstractions](./DKNet.Svc.BlobStorage.Abstractions.md) – reach for it when the values being
  protected are files rather than fields.
- [DKNet.Svc.Transformation](./DKNet.Svc.Transformation.md) – reach for it when a template's tokens must be resolved
  before the result is encrypted or signed.
