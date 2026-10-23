# RSA encryption and signing

Part of [DKNet.Svc.Encryption](index.md). `IRsaEncryption` wraps RSA encrypt/decrypt (OAEP-SHA256) and sign/verify
(SHA-256 with PKCS#1 v1.5) over small UTF-8 payloads, with a public-only mode for the side that must never hold the
private key.

```csharp
using DKNet.Svc.Encryption.Ciphers;

using var signer = new RsaEncryption(); // generates a new 2048-bit key pair
var signature = signer.Sign("payload");

using var verifier = RsaEncryption.FromPublicKey(signer.PublicKey);
var ok = verifier.Verify("payload", signature);
Console.WriteLine(ok); // True

var cipherText = verifier.Encrypt("payload");
var decrypted = signer.Decrypt(cipherText);
Console.WriteLine(decrypted); // payload
```

`new RsaEncryption(int keySize = 2048)` generates a fresh key pair; `new RsaEncryption(string privateKeyBase64)`
loads an existing PKCS#1 DER private key and derives the public key from it. `PublicKey` and `PrivateKey` are Base64
of the raw PKCS#1 structures — not PEM, so don't paste them into a `-----BEGIN` block without re-encoding.

![FromPublicKey(key) creates an instance with no private key; Encrypt and Verify work on it directly, while Decrypt and Sign throw InvalidOperationException because the private-key check fails.](diagrams/rsa-encryption.svg)

The diagram is the public-only instance's own call path — it cannot show the full-key-pair instance, where every
one of `Encrypt`, `Decrypt`, `Sign`, and `Verify` succeeds because `_hasPrivate` is `true`.

## ⚙️ Configuration reference

| Constructor / factory | Parameter | Default | Effect |
|---|---|---|---|
| `AddRsaEncryption(privateKeyBase64)` | Base64 PKCS#1 private key | — | Registers `IRsaEncryption` as a singleton; throws `ArgumentException` on a blank key. |
| `new RsaEncryption(int keySize = 2048)` | Key size in bits | `2048` | Generates a fresh key pair at that size. |
| `new RsaEncryption(string privateKeyBase64)` | PKCS#1 private key | *(required)* | Loads an existing pair; the public key is derived. |
| `RsaEncryption.FromPublicKey(string publicKeyBase64)` | PKCS#1 public key | *(required)* | Public-only instance: `Encrypt`/`Verify` work, `Decrypt`/`Sign` throw `InvalidOperationException`. |

Fixed, non-configurable choices: OAEP-SHA256 padding for encryption, SHA-256 with PKCS#1 v1.5 for signatures, and
raw PKCS#1 DER (not PEM) for both exported keys.

## 🧱 Where it fits

`AddRsaEncryption` is the other key-bearing cipher registration in the [package architecture](index.md) — like
`AddAesGcmEncryption`, it produces one singleton over the key you pass in, separate from the keyless hashing
services.

## ⚠️ Gotchas & limits

- **Not for bulk data.** This type is intended for small payloads (secrets, session keys); use a hybrid RSA +
  symmetric approach for large messages.
- **A public-only instance throws rather than silently degrading.** `Decrypt` and `Sign` both check `_hasPrivate`
  first and throw `InvalidOperationException` — there's no way to attempt a private-key operation and get a useful
  partial result.
- **Dispose what you construct.** `RsaEncryption` holds a native `RSA` handle; DI disposes a singleton registration,
  but a hand-built instance needs a `using`.
- **No key size enforcement beyond the constructor default.** `new RsaEncryption(keySize)` accepts whatever
  `RSA.Create(keySize)` accepts; there's no minimum-size guard in this package.
