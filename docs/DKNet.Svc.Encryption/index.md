# DKNet.Svc.Encryption

Explicitly-invoked cryptography toolkit for .NET developers — AES-GCM and RSA encryption, RSA signing, HMAC and SHA hashing, and Base64/Base64URL helpers, all string-in/string-out.

> [!NOTE]
> Looking for **transparent EF Core column encryption** instead — where an attribute on an entity property does the
> work automatically on save/load? That's a different package: [DKNet.EfCore.Encryption](../DKNet.EfCore.Encryption/index.md).
> Use `DKNet.Svc.Encryption` when you want to call `Encrypt`/`Decrypt` yourself at the point you call it; use
> `DKNet.EfCore.Encryption` when you want a column encrypted transparently, without the code that reads and writes
> the entity knowing. The two packages don't share implementation.

## ✨ Why use it?

- **Authenticated encryption without the ceremony.** `IAesGcmEncryption` handles nonce generation, tag handling, and
  packaging into a single Base64 string, so call sites never assemble a cipher envelope by hand.
- **Correct-by-default primitives.** A fresh random nonce per AES-GCM call, OAEP-SHA256 for RSA, and
  `CryptographicOperations.FixedTimeEquals` on every `Verify*` call and key comparison — the choices that are easy to
  get wrong are already made.
- **UTF-8 strings in, Base64 or hex strings out.** Values fit straight into JSON payloads, configuration, headers, or
  a `varchar` column with no byte-array plumbing.
- **Nothing else comes with it.** No EF Core, no hosting, no storage dependency — usable from a domain service, a
  console app, or a background worker.

## 🚀 Quick Start

Use a .NET 10 console project. Install the package, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EncDemo
cd EncDemo
dotnet add package DKNet.Svc.Encryption
dotnet add package Microsoft.Extensions.DependencyInjection
```

```csharp
using DKNet.Svc.Encryption;
using DKNet.Svc.Encryption.Ciphers;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddEncryptionServices();                                   // IShaHashing, IHmacHashing
services.AddAesGcmEncryption(new AesGcmEncryption().Key);            // a throwaway demo key
using var provider = services.BuildServiceProvider();

var aesGcm = provider.GetRequiredService<IAesGcmEncryption>();
var package = aesGcm.EncryptString("4111-1111-1111-1111");
var plain = aesGcm.DecryptString(package);
Console.WriteLine(plain); // 4111-1111-1111-1111
```

Run with `dotnet run`. In a real application, source the key passed to `AddAesGcmEncryption` from configuration or a
key vault — never a freshly generated one.

## 🏗️ Architecture

![AddEncryptionServices registers the transient IShaHashing and IHmacHashing services with no key; AddAesGcmEncryption and AddRsaEncryption each register one key-bearing singleton cipher inside a boundary labelled "you supply and persist the key"; Base64StringExtensions is static and never registered.](diagrams/svc-encryption-registration-map.svg)

Each registration call owns exactly one outcome: `AddEncryptionServices` always registers the two keyless hashing
services; `AddAesGcmEncryption` and `AddRsaEncryption` each register one singleton cipher built from the key you
pass in. `Base64StringExtensions` is a static extension class, never registered with DI. All three registration
methods skip registration when the service type already exists, so calling any of them twice is a no-op — the
**first** call's key is the one the application keeps using.

## 🔄 How it works

![AddAesGcmEncryption registers IAesGcmEncryption as a singleton; every EncryptString call produces a fresh nonce and tag, and DecryptString with the same associated data returns the plaintext while a mismatched associated data throws CryptographicException.](diagrams/encrypt-decrypt-round-trip.svg)

`EncryptString` and `DecryptString` run against the instance's own key — no key argument needed once resolved. The
diagram cannot show the per-call locking (`AesGcmEncryption` serializes `Encrypt`/`Decrypt` on the shared `AesGcm`
handle) or that a hand-built `new AesGcmEncryption()` generates its own random key instead of using the one
registered with DI. Each feature page below covers its own call path.

## 🧩 Features

- [AES-GCM authenticated encryption](aes-gcm-encryption.md) — encrypt and decrypt a string with a fresh nonce every call.
- [RSA encryption and signing](rsa-encryption.md) — asymmetric encrypt/decrypt and sign/verify, including public-only instances.
- [SHA content hashing](sha-content-hashing.md) — compute and verify SHA-256/512 hex digests.
- [HMAC message signing](hmac-signing.md) — compute and verify keyed HMAC-SHA256/512 signatures.
- [Base64 and Base64URL encoding](base64-encoding.md) — encode/decode helpers for both alphabets.

## 🔗 Related packages

- [DKNet.EfCore.Encryption](../DKNet.EfCore.Encryption/index.md) — the transparent counterpart: encrypt a column
  without the calling code knowing. Use this package when the call site should decide; use that one when the
  storage layer should.
- [DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions/index.md) — encrypt a payload here
  before handing the bytes to `SaveAsync` when the store itself must never see plaintext.
- [DKNet.Svc.Transformation](../DKNet.Svc.Transformation/index.md) — reach for it when a template's tokens must be
  resolved before the result is encrypted or signed.
