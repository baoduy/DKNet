# DKNet.Svc.Encryption

Modern cryptography helpers that balance safety, performance, and developer ergonomics for application-level secrets. The
package bundles symmetric encryption, password-based encryption, public-key operations, hashing, and Base64 helpers behind
cohesive service interfaces with dependency injection support.

## ✨ Highlights

- **Authenticated encryption first** – `IAesGcmEncryption` delivers nonce management, authentication tags, and optional AAD.
- **Deterministic fallbacks** – `IAesEncryption` supplies a simple AES-CBC wrapper for scenarios that require deterministic
  output (key escrow, repeatable secrets).
- **Turn-key DI registration** – `services.AddEncryptionServices()` wires the entire stack with sensible lifetimes.
- **Optimised primitives** – SHA/HMAC implementations cache algorithm instances and keyed HMACs to avoid repeated allocations.
- **Secure defaults** – PBKDF2 password stretching, OAEP-SHA256 for RSA, Base64 validation helpers, and guard rails around
  key material.

## 🧱 Provided Services

| Interface | Implementation | Purpose | Authenticated |
|-----------|----------------|---------|---------------|
| `IAesEncryption` | `AesEncryption` | AES-CBC encryption/decryption with composite key serialization | ❌ |
| `IAesGcmEncryption` | `AesGcmEncryption` | AEAD wrapper with automatic nonce generation and string helpers | ✅ |
| `IPasswordAesEncryption` | `PasswordAesEncryption` | PBKDF2 + AES-CBC helper for password-protected payloads | ❌ |
| `IRsaEncryption` | `RsaEncryption` | RSA 2048/4096 encryption and PKCS#1 signing/verifying | N/A |
| `IHmacHashing` | `HmacHashing` | HMAC-SHA256/512 with caching, Base64/hex output helpers | N/A |
| `IShaHashing` | `ShaHashing` | SHA256/512 hashing utilities with verification helpers | N/A |
| Extensions | `Base65StringExtensions` | Base64/Base64Url encode, decode, and validation helpers | N/A |

> **Naming note**: `Base65StringExtensions` retains legacy naming while covering both Base64 and Base64Url utilities.

## 🚀 Quick Start

```csharp
var services = new ServiceCollection();
services.AddEncryptionServices();

await using var provider = services.BuildServiceProvider();
var aesGcm = provider.GetRequiredService<IAesGcmEncryption>();

var cipher = aesGcm.EncryptString("hello world");
var plain  = aesGcm.DecryptString(cipher); // "hello world"
```

## 📦 Usage Recipes

### AES-GCM (Recommended)

```csharp
var gcm = provider.GetRequiredService<IAesGcmEncryption>();
var aad = Encoding.UTF8.GetBytes("order:1234");

var cipher = gcm.EncryptString("sensitive payload", aad);
var plain  = gcm.DecryptString(cipher, aad);
```

### Password-Based Encryption

```csharp
var passwordCrypto = provider.GetRequiredService<IPasswordAesEncryption>();
var encrypted = passwordCrypto.Encrypt("config-json", "Sup3r$ecret");
var recovered = passwordCrypto.Decrypt(encrypted, "Sup3r$ecret");
```

### RSA Envelope + Signature

```csharp
var rsa = provider.GetRequiredService<IRsaEncryption>();

var cipher = rsa.Encrypt("api-key");
var signature = rsa.Sign("message");

var publicOnly = RsaEncryption.FromPublicKey(rsa.PublicKey);
var verified = publicOnly.Verify("message", signature);
```

### HMAC Hashing

```csharp
var hmac = provider.GetRequiredService<IHmacHashing>();
var mac = hmac.Compute("body", "shared-secret");
var ok  = hmac.Verify("body", "shared-secret", mac);
```

### Base64 Helpers

```csharp
var compact = "payload".ToBase64UrlString();
var original = compact.FromBase64UrlString();
var isValid = compact.IsBase64UrlString();
```

## 🧩 DI Integration

Add all encryption primitives with a single extension:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddEncryptionServices();
}
```

Each interface is registered as a transient implementation to keep cryptographic state isolated per consumer. If you need
long-lived instances (for example to reuse RSA key pairs), register the implementation yourself with the required lifetime.

## 🧠 Design Notes

- **AEAD first** – AES-GCM is provided as the default, authenticated option for general-purpose encryption.
- **Deterministic fallback** – AES-CBC exposes composite keys (`<key>:<iv>`) and deterministic encryption for legacy use cases.
- **Key material hygiene** – RSA keys are exposed as Base64 strings to simplify secure storage; helper factories rebuild
  instances from public or private material on demand.
- **Performance** – SHA and HMAC services reuse algorithm instances, reducing allocations during high-throughput hashing.
- **Validation** – Base64 helpers guard against malformed strings, reducing input sanitisation boilerplate.

## 🛡️ Security Guidance

- Prefer `IAesGcmEncryption` for new development; only use `IAesEncryption` where deterministic output is required.
- Always validate user-controlled inputs before passing them to crypto helpers.
- Store RSA private keys securely (KeyVault, AWS KMS, Azure Key Vault Secrets, etc.).
- Rotate HMAC secrets periodically and prefer per-tenant secrets for multi-tenant workloads.
- Respect cancellation tokens when performing IO-bound work inside your custom wrappers.

## ✅ Testing & Quality

`DKNet.Svc.Encryption` ships with >97% line coverage across deterministic and randomised test suites. Tests assert
round-trip behaviour, guard rails for invalid inputs, and key material validation, making the package suitable for audit-heavy
solutions.
