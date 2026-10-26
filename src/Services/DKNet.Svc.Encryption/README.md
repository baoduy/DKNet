# DKNet.Svc.Encryption

Explicitly-invoked cryptography toolkit for .NET developers — AES-GCM and RSA encryption, RSA signing, HMAC and SHA
hashing, and Base64/Base64URL helpers, all string-in/string-out.

## ✨ Why use it?

- Authenticated AES-256-GCM encryption with a fresh nonce per call, packaged into one opaque Base64 string.
- RSA encrypt/decrypt (OAEP-SHA256) and sign/verify (SHA-256 with PKCS#1 v1.5), including public-key-only instances.
- HMAC-SHA256/512 and SHA-256/512 compute plus constant-time verify.
- Base64 and Base64URL encode/decode helpers.
- No EF Core, hosting, or storage dependency — usable from any domain service, console app, or worker.

> AES-CBC (`IAesEncryption`, `AesEncryption`, `EncryptionSetup.AddAesEncryption`) was removed, not deprecated, with
> no compatibility shim, and ciphertext it produced has no conversion path. See the
> [AES-GCM feature page](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/aes-gcm-encryption.md)
> for details.

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

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/index.md) | Start with architecture and the end-to-end flow. |
| [AES-GCM authenticated encryption](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/aes-gcm-encryption.md) | Encrypt and decrypt a string with a fresh nonce every call. |
| [RSA encryption and signing](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/rsa-encryption.md) | Asymmetric encrypt/decrypt and sign/verify, including public-only instances. |
| [SHA content hashing](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/sha-content-hashing.md) | Compute and verify SHA-256/512 hex digests. |
| [HMAC message signing](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/hmac-signing.md) | Compute and verify keyed HMAC-SHA256/512 signatures. |
| [Base64 and Base64URL encoding](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/base64-encoding.md) | Encode/decode helpers for both alphabets. |
