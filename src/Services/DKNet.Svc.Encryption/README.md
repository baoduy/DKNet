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

```bash
dotnet add package DKNet.Svc.Encryption
```

```csharp
using DKNet.Svc.Encryption;
using DKNet.Svc.Encryption.Ciphers;

builder.Services.AddEncryptionServices();                                       // HMAC + SHA hashing
builder.Services.AddAesGcmEncryption(builder.Configuration["Crypto:AesKey"]!);  // singleton IAesGcmEncryption

public sealed class SecretStore(IAesGcmEncryption aesGcm)
{
    public string Protect(string plainText) => aesGcm.EncryptString(plainText);
    public string Reveal(string cipherPackage) => aesGcm.DecryptString(cipherPackage);
}
```

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/index.md) | Start with architecture and the end-to-end flow. |
| [AES-GCM authenticated encryption](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/aes-gcm-encryption.md) | Encrypt and decrypt a string with a fresh nonce every call. |
| [RSA encryption and signing](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/rsa-encryption.md) | Asymmetric encrypt/decrypt and sign/verify, including public-only instances. |
| [SHA content hashing](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/sha-content-hashing.md) | Compute and verify SHA-256/512 hex digests. |
| [HMAC message signing](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/hmac-signing.md) | Compute and verify keyed HMAC-SHA256/512 signatures. |
| [Base64 and Base64URL encoding](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Encryption/base64-encoding.md) | Encode/decode helpers for both alphabets. |
