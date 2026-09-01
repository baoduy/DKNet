# DKNet.Svc.Encryption

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.Encryption)](https://www.nuget.org/packages/DKNet.Svc.Encryption/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.Encryption)](https://www.nuget.org/packages/DKNet.Svc.Encryption/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Standalone, explicitly-invoked cryptography toolkit: AES-GCM encryption, RSA encrypt/sign, HMAC and SHA hashing, and
Base64/Base64URL helpers, all operating on UTF-8 strings. For transparent EF Core column encryption instead, see
`DKNet.EfCore.Encryption` — a different package.

## Features

- `IAesGcmEncryption` — authenticated AES-256-GCM encryption with optional associated data
- `IRsaEncryption` — RSA encrypt/decrypt (OAEP-SHA256) and sign/verify (PKCS1-SHA256), including public-key-only instances
- `IHmacHashing` / `IShaHashing` — HMAC-SHA256/512 and SHA-256/512 compute + constant-time verify
- `Base64StringExtensions` — Base64 and Base64URL encode/decode helpers
- `IAesEncryption` (AES-CBC) kept for migration only — `[Obsolete]`, prefer AES-GCM

## Installation

```bash
dotnet add package DKNet.Svc.Encryption
```

## Quick Start

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

## Migration — namespace changes in this release

Root types were grouped into concern folders; the namespace of each moved type now ends
with its folder name. This is an import-only source break: no type was renamed, removed,
resignatured, or had its behaviour changed — update the `using` line and you're done.

| Type | Old namespace | New namespace |
|---|---|---|
| `IAesEncryption`/`AesEncryption`, `IAesGcmEncryption`/`AesGcmEncryption`, `IRsaEncryption`/`RsaEncryption` | `DKNet.Svc.Encryption` | `DKNet.Svc.Encryption.Ciphers` |
| `IHmacHashing`/`HmacHashing`, `IShaHashing`/`ShaHashing` | `DKNet.Svc.Encryption` | `DKNet.Svc.Encryption.Hashing` |

`EncryptionSetup` (registration point) and `Base64StringExtensions`/`Base65StringExtensions`
(the Base64/Base64URL encoding helpers) stay at `DKNet.Svc.Encryption`.

## Documentation

Full feature reference, configuration, and gotchas:
https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.Encryption.md
