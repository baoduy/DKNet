# DKNet.Svc.Encryption

Light‑weight .NET encryption & cryptographic utilities focused on pragmatic application scenarios:

- Symmetric encryption (AES-CBC wrapper and modern AES-GCM)
- Public key (RSA) encryption + signing
- HMAC (SHA-256 / SHA-512) with cached keyed instances
- Hashing (SHA-256 / SHA-512) with cached algorithm instances
- Password‑based encryption (PBKDF2 + AES-CBC)
- Base64 & Base64URL helpers (RFC 4648) with validation utilities
- Opinionated DI registration helper

> ✅ High test coverage (>97% lines) – designed to be easily auditable.

---
## Contents
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Services & Interfaces](#services--interfaces)
- [Examples](#examples)
  - [AES (CBC wrapper)](#aes-cbc-wrapper)
  - [AES-GCM (preferred)](#aes-gcm-preferred)
  - [Password-based Encryption](#password-based-encryption)
  - [RSA Encrypt / Sign](#rsa-encrypt--sign)
  - [HMAC](#hmac)
  - [Hashing](#hashing)
  - [Base64 / Base64URL](#base64--base64url)
- [Dependency Injection](#dependency-injection)
- [Security Notes](#security-notes)
- [Performance Notes](#performance-notes)
- [Migration / Naming Notes](#migration--naming-notes)
- [Testing / Coverage](#testing--coverage)
- [Roadmap](#roadmap)
- [License](#license)

---
## Installation
Add the NuGet package (placeholder ID – adjust to published package name):

```
dotnet add package DKNet.Svc.Encryption
```

Or reference the project directly in your solution.

---
## Quick Start
```csharp
var services = new ServiceCollection();
services.AddEncryptionServices();
var provider = services.BuildServiceProvider();

var aesGcm = provider.GetRequiredService<IAesGcmEncryption>();
var cipher = aesGcm.EncryptString("hello world");
var plain = aesGcm.DecryptString(cipher); // "hello world"
```

---
## Services & Interfaces
| Interface | Implementation | Purpose | Authenticated? | Notes |
|-----------|----------------|---------|----------------|-------|
| `IAesEncryption` | `AesEncryption` | AES-CBC static IV wrapper | ❌ | Deterministic; DO NOT use for multiple distinct plaintexts if confidentiality pattern matters. Prefer GCM. |
| `IAesGcmEncryption` | `AesGcmEncryption` | Modern AES-GCM (AEAD) | ✅ | Random nonce per message, key property, wrapper & direct methods. |
| `IPasswordAesEncryption` | `PasswordAesEncryption` | PBKDF2 + AES-CBC | ❌ | Salt + IV stored (salt:iv:cipher) → Base64. Good for secrets at rest. |
| `IRsaEncryption` | `RsaEncryption` | RSA 2048/4096 encrypt + sign | N/A | OAEP-SHA256 + PKCS#1 signatures. Public-only instance via `FromPublicKey`. |
| `IShaHashing` | `ShaHashing` | SHA-256 / SHA-512 hashing | n/a | Cached algorithm instances, hex (uppercase) output. |
| `IHmacHashing` | `HmacHashing` | HMAC (SHA-256 / SHA-512) | n/a | Caches keyed HMAC instances; Base64 or Hex. |
| (Extensions) | `Base65StringExtensions` | Base64 / Base64URL | n/a | Validation, encode/decode, URL-safe variant. |

> **Why “65”**: Backward naming kept; includes both Base64 & Base64URL helpers.

---
## Examples
### AES (CBC wrapper)
```csharp
using var aes = new AesEncryption(); // new key+iv generated
string key = aes.Key;                // Portable composite key (Base64(key):Base64(iv)) all Base64-wrapped
var cipher = aes.EncryptString("sensitive");
var back   = aes.DecryptString(cipher);

// Reconstruct with key
using var aes2 = new AesEncryption(key);
var again = aes2.DecryptString(cipher); // "sensitive"
```
**Warning**: IV is reused per instance → encryption is deterministic. Do not use this pattern for unique large scale messaging. Prefer `IAesGcmEncryption`.

### AES-GCM (preferred)
```csharp
using var gcm = new AesGcmEncryption();
string key = gcm.Key; // Base64 key only (no IV/nonce stored)
var cipher = gcm.EncryptString("hello");
var plain  = gcm.DecryptString(cipher);

// Associated Data (AAD)
var aad = Encoding.UTF8.GetBytes("meta");
var c2  = gcm.EncryptString("payload", aad);
var p2  = gcm.DecryptString(c2, aad);
```
Wrapper methods enforcing same key:
```csharp
gcm.Encrypt("msg", gcm.Key); // OK
gcm.Decrypt(cipher, gcm.Key); // OK
```

### Password-based Encryption
```csharp
IPasswordAesEncryption pbe = new PasswordAesEncryption();
var password = "Sup3r$ecret";
var cipher = pbe.Encrypt("secret-config-json", password);
var json   = pbe.Decrypt(cipher, password);
```
Format: `Base64( Base64(salt):Base64(iv):Base64(cipher) )`.

### RSA Encrypt / Sign
```csharp
using var rsa = new RsaEncryption();
string publicKey  = rsa.PublicKey;            // Base64 (PKCS#1)
string privateKey = rsa.PrivateKey!;          // Keep secure

var cipher = rsa.Encrypt("api-key");
var clear  = rsa.Decrypt(cipher);

var sig = rsa.Sign("message");
var publicOnly = RsaEncryption.FromPublicKey(publicKey);
bool ok = publicOnly.Verify("message", sig); // true
```

### HMAC
```csharp
using IHmacHashing hmac = new HmacHashing();
var macBase64 = hmac.Compute("body", "shared-secret");
var macHex = hmac.Compute("body", "shared-secret", HmacAlgorithm.Sha512, asBase64:false);
var valid = hmac.Verify("body", "shared-secret", macBase64); // true
```

### Hashing
```csharp
using IShaHashing hash = new ShaHashing();
var sha256 = hash.ComputeHash("payload");           // Uppercase hex by default
var sha512Lower = hash.ComputeHash("payload", HashAlgorithmKind.Sha512, upperCase:false);
var ok = hash.VerifyHash("payload", sha256);
```

### Base64 / Base64URL
```csharp
var std = "hello".ToBase64String();        // SGVsbG8=
var plain = std.FromBase64String();        // hello

var url = "hello".ToBase64UrlString();     // SGVsbG8 (no '=')
var plain2 = url.FromBase64UrlString();    // hello

bool valid = std.IsBase64String();         // true
```

---
## Dependency Injection
All cryptographic primitives are registered via the extension method:
```csharp
services.AddEncryptionServices();
```
Resolves:
- `IAesEncryption`
- `IAesGcmEncryption`
- `IShaHashing`
- `IHmacHashing`
- `IPasswordAesEncryption`
- `IRsaEncryption` (generated keypair per singleton instance – for multi-key scenarios register factories yourself)

> Registering `IRsaEncryption` as singleton generates one key pair at startup. Override if per-tenant / per-request keys are needed.

---
## Security Notes
| Concern | Guidance |
|---------|----------|
| AES-CBC (`AesEncryption`) | Reuses IV per instance. Deterministic ciphertext → pattern leakage. Use only for niche deterministic needs or replace with per-call IV logic if you modify. |
| AES-GCM | Preferred for confidentiality + integrity. Nonce generated automatically (12 bytes). Do not reuse nonce+key pair manually. |
| RSA | Use for small payloads (keys, tokens). Do not encrypt large blobs; instead encrypt a random AES key and use symmetric encryption for data. |
| Password-based | PBKDF2 iterations default (tunable) – ensure adequate iteration count for your threat model (100k baseline). |
| Hashing | Only strong SHA variants included (no MD5/SHA1). |
| HMAC | Safe for message authentication. Keep shared secret length ≥ 32 bytes for SHA-256. |
| Base64 Validation | `IsBase64String` is strict (length multiple of 4, proper padding, character set). Use before decoding untrusted input if you need boolean outcome instead of exceptions. |

### Key Handling
- Persist keys you want to reuse (e.g. `AesGcmEncryption.Key` or RSA private key).
- Protect RSA private keys with OS secrets vault, Azure Key Vault, AWS KMS, etc.
- Rotate keys periodically (design hook not included – compose externally).

### Recommended Defaults
| Scenario | Recommended API |
|----------|-----------------|
| General data at rest | `IAesGcmEncryption` (store key securely) |
| Deterministic encryption required* | Wrap Aes with custom per-call IV logic or use format-preserving scheme (not included) |
| Password derives encryption key | `IPasswordAesEncryption` |
| Message authentication | `IHmacHashing` |
| Short secret exchange | `IRsaEncryption` + `IAesGcmEncryption` hybrid |

\* Consider whether deterministic encryption is truly necessary.

---
## Performance Notes
- Hash & HMAC services cache algorithm instances / keyed HMAC objects → fewer allocations under load.
- AES-GCM instance reused (keyed) – per-call ephemeral nonce & buffers.
- Avoid sharing a single `AesEncryption` instance across many threads if you later add per-call IV logic; current deterministic approach is thread-safe for reads due to ephemeral encryptors.

---
## Migration / Naming Notes
Earlier internal drafts used names like `HashGenerator` / `HmacGenerator`; these evolved to `ShaHashing` / `HmacHashing` to signal narrow scope. If you had earlier package versions:
- Replace `IHashGenerator` → `IShaHashing`
- Replace `IHmacGenerator` → `IHmacHashing`
- AES-GCM wrapper methods (`Encrypt/Decrypt` with key parameter) now validate the key instead of silently using provided mismatched keys.

---
## Testing / Coverage
Extensive xUnit + Shouldly tests cover:
- All happy paths + error paths (invalid keys, tampering, disposal)
- Base64 / Base64URL edge cases & Unicode
- Associated data success & mismatch (AES-GCM)
- DI registration

To run tests with coverage (using XPlat collector):
```bash
cd path/to/src
 dotnet test Services/DKNet.Svc.Encryption.Tests/DKNet.Svc.Encryption.Tests.csproj \
  --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

Cobertura report (example): `TestResults/<guid>/coverage.cobertura.xml`.

---
## Roadmap
| Item | Status |
|------|--------|
| Add per-call IV CBC variant | Planned |
| Optional key rotation helpers | Planned |
| Streaming large-file encrypt/decrypt utilities | Planned |
| Support Argon2id for password KDF (when BCL available / via optional dep) | Under review |
| Add MD5 / SHA1 (explicit opt-in only) | Not planned |

---
## License
Specify your license (e.g. MIT) here.

---
## Disclaimer
This library streamlines common application crypto tasks but is **not** a substitute for a full security review. For high-risk / compliance contexts (PCI, HIPAA, FIPS) involve a qualified cryptographer and review threat models.

---
## Contributing
PRs welcome. Please include:
- Unit tests for new branches
- XML docs for public members
- Justification for any new algorithms / modes

---
## Support
Open an issue with reproduction steps. Security concerns: disclose privately first.

---
Enjoy building securely! 🔐

