# DKNet.Svc.Encryption

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.Encryption)](https://www.nuget.org/packages/DKNet.Svc.Encryption/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.Encryption)](https://www.nuget.org/packages/DKNet.Svc.Encryption/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

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

## Configuration — registration and constructor surface

There is no options type and no `IConfiguration` binding path. Everything a caller can vary is an argument.

| Registration | Argument | Registers | Lifetime |
|---|---|---|---|
| `AddEncryptionServices()` | — | `IShaHashing`, `IHmacHashing` | Transient |
| `AddAesGcmEncryption(base64Key)` | Base64 128/192/256-bit key | `IAesGcmEncryption` | Singleton |
| `AddRsaEncryption(privateKeyBase64)` | Base64 PKCS#1 private key | `IRsaEncryption` | Singleton |
| `AddAesEncryption(keyString)` `[Obsolete]` | The value `AesEncryption.Key` returned | `IAesEncryption` | Singleton |

All four are idempotent — a second call registers nothing, so the **first** call's key is the one the
application uses. The three cipher methods throw `ArgumentException` for a null, empty, or whitespace key.

| Constructor / factory | Parameter | Default | Effect |
|---|---|---|---|
| `new AesGcmEncryption(string? key = null)` | Base64 key, must not contain `:` | `null` | `null` generates a random 256-bit key; a supplied key must decode to 16, 24, or 32 bytes. |
| `new RsaEncryption(int keySize = 2048)` | Key size in bits | `2048` | Generates a fresh key pair. |
| `new RsaEncryption(string privateKeyBase64)` | PKCS#1 private key | *(required)* | Loads an existing pair; the public key is derived. |
| `RsaEncryption.FromPublicKey(string publicKeyBase64)` | PKCS#1 public key | *(required)* | Public-only: `Encrypt`/`Verify` work, `Decrypt`/`Sign` throw `InvalidOperationException`. |
| `new AesEncryption(string? keyString = null)` `[Obsolete]` | The value `AesEncryption.Key` returned | `null` | `null` generates a key and IV. `Key` is Base64 of `base64(key):base64(iv)` — one opaque token. |

| Per-call switch | Default | Effect |
|---|---|---|
| `IAesGcmEncryption.EncryptString`/`DecryptString` — `associatedData` | `null` | Extra bytes bound into the tag; a mismatch on decrypt throws `CryptographicException`. |
| `IHmacHashing.ComputeSha*` — `asBase64` | `true` | `false` returns upper-case hex. |
| `IHmacHashing.VerifySha*` — `signatureIsBase64` | `true` | Must match how the signature was produced; a mismatch returns `false`. |
| `IShaHashing.ComputeSha*` — `upperCase` | `false` | `true` returns upper-case hex. |
| `IHmacHashing.VerifySha*` / `IShaHashing.VerifySha*` — `ignoreCase` | `true` | No effect — comparison runs on decoded bytes. |

Fixed, non-configurable choices: AES-GCM uses a fresh 12-byte nonce and a 16-byte tag per call and packages
the result as Base64 of `base64(nonce):base64(tag):base64(cipher)`; RSA uses OAEP-SHA256 for encryption and
SHA-256 with PKCS#1 v1.5 for signatures; keys are exported as raw PKCS#1 DER in Base64, not PEM; every
`Verify*` compares with `CryptographicOperations.FixedTimeEquals`.

## Documentation

Full feature reference, configuration, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/Services/DKNet.Svc.Encryption.md
