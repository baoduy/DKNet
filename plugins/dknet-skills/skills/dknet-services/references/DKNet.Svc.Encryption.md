# DKNet.Svc.Encryption reference

| Field | Value |
|---|---|
| Area | Services |
| NuGet | `dotnet add package DKNet.Svc.Encryption` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.Encryption.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/Services/DKNet.Svc.Encryption |
| Depends on (DKNet) | none — no `ProjectReference` in the `.csproj` |
| Depends on (3rd party) | `Microsoft.Extensions.DependencyInjection.Abstractions` |
| Target framework | `net10.0` |

## Purpose

`DKNet.Svc.Encryption` is a standalone, explicitly-invoked cryptography toolkit: AES-256-GCM authenticated
encryption, RSA encrypt/decrypt + sign/verify, HMAC-SHA256/512 and SHA-256/512 hashing, and Base64/Base64URL
helpers — all string-in/string-out over UTF-8. Every cipher call is made by application code on demand (no
interceptor, no attribute, no automatic trigger); keys are arguments the caller supplies and persists, never
derived, stored, or rotated by the package itself.

It is NOT the package for transparent EF Core column encryption — that is `DKNet.EfCore.Encryption`, which has
its own, unrelated AES-GCM key-provider abstraction (see `dknet-efcore-data-security`). Reach for this package
only when the call site itself should decide when encryption happens.

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `AddEncryptionServices` | `IServiceCollection AddEncryptionServices(this IServiceCollection services)` | `IServiceCollection` | Registers **only** `IShaHashing` and `IHmacHashing` (both `TryAddTransient`). Registers no cipher. Idempotent (uses `TryAdd*`). |
| `AddAesGcmEncryption` | `IServiceCollection AddAesGcmEncryption(this IServiceCollection services, string base64Key)` | `IServiceCollection` | Registers `IAesGcmEncryption` as a **singleton** built from `base64Key`. Skips registration if `IAesGcmEncryption` is already registered — first call's key wins. Throws `ArgumentNullException` if `base64Key` is `null`, `ArgumentException` if empty/whitespace. |
| `AddRsaEncryption` | `IServiceCollection AddRsaEncryption(this IServiceCollection services, string privateKeyBase64)` | `IServiceCollection` | Registers `IRsaEncryption` as a **singleton** built from `privateKeyBase64`. Skips registration if `IRsaEncryption` is already registered — first call's key wins. Throws `ArgumentNullException` if `privateKeyBase64` is `null`, `ArgumentException` if empty/whitespace. |

All three live on the static class `EncryptionSetup` (namespace `DKNet.Svc.Encryption`). There is no
`ModelBuilder`/`DbContextOptionsBuilder` surface, no attribute, and no MSBuild/analyzer surface in this package.

## Public surface

### `DKNet.Svc.Encryption`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `EncryptionSetup` | static class | DI registration entry point | `AddEncryptionServices(IServiceCollection)`, `AddAesGcmEncryption(IServiceCollection, string)`, `AddRsaEncryption(IServiceCollection, string)` — all return `IServiceCollection` |
| `Base64StringExtensions` | static class | Base64 / Base64URL encode/decode helpers, usable as extension methods or static calls | `string ToBase64String(this string plainText)`; `string FromBase64String(this string encryptedText)` (returns `""` for null/empty/whitespace); `string ToBase64UrlString(this string plainText)`; `string FromBase64UrlString(this string encryptedText)` (returns `""` for null/empty/whitespace); `bool IsBase64String(this string base64String)` |

### `DKNet.Svc.Encryption.Ciphers`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IAesGcmEncryption` | interface (`: IDisposable`) | AES-GCM authenticated encryption contract | `string Key { get; }`; `string EncryptString(string plainText, byte[]? associatedData = null)`; `string DecryptString(string cipherPackage, byte[]? associatedData = null)`; `string Encrypt(string plainText, string base64Key, byte[]? associatedData = null)`; `string Decrypt(string cipherPackage, string base64Key, byte[]? associatedData = null)` |
| `AesGcmEncryption` | sealed class | Implementation; caches one `AesGcm` handle per instance | `AesGcmEncryption(string? key = null)` — `null` generates a random 256-bit key; a supplied key must be Base64, contain no `:`, and decode to 16/24/32 bytes, else `ArgumentException`; a malformed Base64 string throws `FormatException` from `Convert.FromBase64String`. Implements all `IAesGcmEncryption` members plus `Dispose()`. |
| `IRsaEncryption` | interface | RSA encrypt/decrypt + sign/verify contract | `string PublicKey { get; }`; `string? PrivateKey { get; }`; `string Encrypt(string plainText)`; `string Decrypt(string base64CipherText)`; `string Sign(string data)`; `bool Verify(string data, string base64Signature)` |
| `RsaEncryption` | sealed class (`: IRsaEncryption, IDisposable`) | Implementation; caches exported key strings | `RsaEncryption(int keySize = 2048)` — generates a fresh pair; `RsaEncryption(string privateKeyBase64)` — imports PKCS#1 DER private key (public key derived); `static RsaEncryption FromPublicKey(string publicKeyBase64)` — public-only instance; `Decrypt`/`Sign` throw `InvalidOperationException` on a public-only instance; `Decrypt`/`Encrypt`/`Sign`/`Verify` throw `ObjectDisposedException` after `Dispose()` — but `PublicKey`/`PrivateKey` only throw it on a *first* read after disposal, since both cache their exported value (`??=`): a value already read once before `Dispose()` keeps returning successfully afterwards. |

### `DKNet.Svc.Encryption.Hashing`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IHmacHashing` | interface | Keyed HMAC-SHA256/512 compute + constant-time verify | `string ComputeSha256(string message, string secretKey, bool asBase64 = true)`; `string ComputeSha512(string message, string secretKey, bool asBase64 = true)`; `bool VerifySha256(string message, string secretKey, string expectedSignature, bool signatureIsBase64 = true)`; `bool VerifySha512(string message, string secretKey, string expectedSignature, bool signatureIsBase64 = true)` |
| `HmacHashing` | sealed class | Stateless implementation over `HMACSHA256`/`HMACSHA512` static APIs; zeroes the key-byte buffer after each computation | Implements `IHmacHashing`. No public constructor parameters, no disposable state. |
| `IShaHashing` | interface | Unkeyed SHA-256/512 compute + constant-time verify | `string ComputeSha256(string input, bool upperCase = false)`; `string ComputeSha512(string input, bool upperCase = false)`; `bool VerifySha256(string input, string expectedHex, bool ignoreCase = true)`; `bool VerifySha512(string input, string expectedHex, bool ignoreCase = true)` |
| `ShaHashing` | sealed class | Stateless implementation over `SHA256`/`SHA512` static `HashData` | Implements `IShaHashing`. |

No public types exist outside these three namespaces. `AesGcmEncryption` and `RsaEncryption` are the only
`IDisposable` public types in the package; `HmacHashing`/`ShaHashing` hold no unmanaged/cached state and need no
`using`.

## Options & defaults

There is no options object and no `IConfiguration` binding path anywhere in this package — every customization
point is a constructor or method argument.

| Option (constructor/argument) | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `AesGcmEncryption(string? key)` | `string?` | `null` | `null` → random 256-bit key generated via `RandomNumberGenerator`; supplied value must decode to 16/24/32 bytes and contain no `:` | `AesGcmEncryption` constructor |
| `RsaEncryption(int keySize)` | `int` | `2048` | Size of the freshly generated RSA key pair | `RsaEncryption` constructor |
| `EncryptString`/`DecryptString`/`Encrypt`/`Decrypt` — `associatedData` | `byte[]?` | `null` | Extra bytes authenticated (but not encrypted) into the GCM tag; a mismatch on decrypt throws `CryptographicException` | `IAesGcmEncryption` methods |
| `ComputeSha256`/`ComputeSha512` (HMAC) — `asBase64` | `bool` | `true` | `false` → upper-case hex instead of Base64 | `IHmacHashing` |
| `VerifySha256`/`VerifySha512` (HMAC) — `signatureIsBase64` | `bool` | `true` | Must match how the signature was produced; a mismatch returns `false`, never throws | `IHmacHashing` |
| `ComputeSha256`/`ComputeSha512` (SHA) — `upperCase` | `bool` | `false` | `true` → upper-case hex | `IShaHashing` |
| `VerifySha256`/`VerifySha512` (SHA) — `ignoreCase` | `bool` | `true` | **No effect** — comparison is done on decoded bytes, so the flag never changes the result | `IShaHashing` |

DI registrations (the equivalent of a configuration surface): see **Entry points** above.

## Usage patterns

### AES-GCM: DI-registered singleton cipher

**When**: a service needs to encrypt/decrypt values across requests using one durable, configured key.

```csharp
using DKNet.Svc.Encryption;
using DKNet.Svc.Encryption.Ciphers;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddEncryptionServices();                                  // IShaHashing, IHmacHashing
services.AddAesGcmEncryption("BASE64_256_BIT_KEY_FROM_CONFIG==");   // IAesGcmEncryption singleton

var provider = services.BuildServiceProvider();
var store = new SecretStore(provider.GetRequiredService<IAesGcmEncryption>());
var package = store.Protect("4111-1111-1111-1111");
var plain = store.Reveal(package);
```

`SecretStore` wraps the resolved cipher:

```csharp
using DKNet.Svc.Encryption.Ciphers;

public sealed class SecretStore(IAesGcmEncryption aesGcm)
{
    public string Protect(string plainText) => aesGcm.EncryptString(plainText);
    public string Reveal(string cipherPackage) => aesGcm.DecryptString(cipherPackage);
}
```

**Notes**: the key must be sourced from configuration or a key vault, never hardcoded. Calling
`AddAesGcmEncryption` a second time with a different key is a no-op — the first call's key is the one every
resolution uses.

### AES-GCM: hand-built instance with a persisted key

**When**: no DI container, or the key must be generated once and stored by the caller.

```csharp
using DKNet.Svc.Encryption.Ciphers;

using var aes = new AesGcmEncryption();   // generates a random 256-bit key
var keyToPersist = aes.Key;               // Base64 — save this or decryption is impossible later

var package = aes.EncryptString("secret value");
var roundTripped = aes.DecryptString(package);

// later, in a new process, reconstruct from the persisted key:
using var aes2 = new AesGcmEncryption(keyToPersist);  // same key, new instance
var stillWorks = aes2.DecryptString(package);
```

**Notes**: `new AesGcmEncryption()` with no key generates a throwaway key — fine only for data that never
outlives the process. `EncryptString`/`DecryptString` throw `ObjectDisposedException` once the instance is
disposed; dispose only after every consumer is done with it.

### RSA: split signer/verifier roles

**When**: one side must produce a signature and another side must verify it without ever holding the private key.

```csharp
using DKNet.Svc.Encryption.Ciphers;

// signing side — has the private key
using var signer = new RsaEncryption();          // generates a fresh 2048-bit pair
var privateKeyToPersist = signer.PrivateKey!;    // Base64 PKCS#1 DER — persist this
var publicKeyToShare = signer.PublicKey;         // Base64 PKCS#1 DER — share this

var payload = "order-42:confirmed";
var signature = signer.Sign(payload);

// verifying side — public key only
using var verifier = RsaEncryption.FromPublicKey(publicKeyToShare);  // public-only instance
var ok = verifier.Verify(payload, signature);          // true

// the verifier cannot decrypt or sign — it holds no private key
// verifier.Sign(payload) would throw InvalidOperationException
```

**Notes**: `PublicKey`/`PrivateKey` are raw PKCS#1 DER, Base64-encoded — not PEM. `Decrypt`/`Sign` on a
public-only instance (`FromPublicKey`) throw `InvalidOperationException`, not return `null` or `false`.

### HMAC: verifying an inbound webhook signature

**When**: an HTTP handler must confirm a request body was signed with a shared secret.

```csharp
using DKNet.Svc.Encryption.Hashing;

var verifier = new WebhookVerifier(new HmacHashing());
var trusted = verifier.IsTrusted(rawBody: "{...}", secret: "whsec_abc", signatureHeader: "BASE64_SIGNATURE");
```

`WebhookVerifier` is a thin wrapper around `IHmacHashing`:

```csharp
using DKNet.Svc.Encryption.Hashing;

public sealed class WebhookVerifier(IHmacHashing hmac)
{
    public bool IsTrusted(string rawBody, string secret, string signatureHeader) =>
        hmac.VerifySha256(rawBody, secret, signatureHeader);
}
```

**Notes**: a mismatched encoding between how the signature was produced (`asBase64` on `ComputeSha256`) and how
it is verified (`signatureIsBase64` on `VerifySha256`) returns `false`, not an exception. Blank `message`,
`secretKey`, or `expectedSignature` throws `ArgumentException`.

### SHA content hash + Base64URL token

**When**: fingerprinting a payload and embedding a value in a URL-safe token without a JWT library.

```csharp
using DKNet.Svc.Encryption;
using DKNet.Svc.Encryption.Hashing;

IShaHashing sha = new ShaHashing();
var contentHash = sha.ComputeSha256("file contents as utf8 text");   // lower-case hex by default

var payloadJson = """{"sub":"user-1","exp":1999999999}""";
var token = payloadJson.ToBase64UrlString();                 // URL-safe, no '=' padding
var recovered = token.FromBase64UrlString();                 // back to the original JSON
```

**Notes**: `sha.VerifySha256(input, expectedHex)` returns `false` (never throws) when `expectedHex` is not valid
hex. Empty-string input is allowed for both compute and verify; `null` input throws `ArgumentNullException`.

## Runtime behaviour

**`AesGcmEncryption.EncryptString(plainText, associatedData)`**: validates the instance is not disposed and
`plainText` is not null → generates a fresh random 12-byte nonce via `RandomNumberGenerator` → UTF-8-encodes
`plainText` → takes a `lock` on the shared `AesGcm` handle and calls `AesGcm.Encrypt`, producing ciphertext and a
16-byte tag → releases the lock → joins `base64(nonce):base64(tag):base64(cipher)` into one string → Base64-wraps
that whole string and returns it. Every call produces a different nonce, so ciphertext is never deterministic for
the same plaintext.

**`AesGcmEncryption.DecryptString(cipherPackage, associatedData)`**: validates not disposed, `cipherPackage` not
blank → Base64-decodes the outer wrapper → splits on `:` and requires exactly 3 parts (else `ArgumentException`)
→ Base64-decodes nonce/tag/cipher → takes the same lock → calls `AesGcm.Decrypt`, which throws
`CryptographicException` if the tag doesn't verify (wrong key, tampered ciphertext, or mismatched
`associatedData`) → UTF-8-decodes the plaintext bytes and returns.

**`AesGcmEncryption.Encrypt`/`Decrypt` (base64Key overloads)**: first call `KeyMatches(base64Key)`, which
Base64-decodes both the supplied key and the instance's own `Key` and compares the byte arrays with
`CryptographicOperations.FixedTimeEquals` (a malformed Base64 string is caught and treated as a non-match) → on a
mismatch, throw `InvalidOperationException` without touching the cipher at all → on a match, delegate to
`EncryptString`/`DecryptString`.

**`RsaEncryption.Encrypt`/`Decrypt`**: UTF-8-encode/decode the text, call `RSA.Encrypt`/`RSA.Decrypt` with
`RSAEncryptionPadding.OaepSHA256`; `Decrypt` checks disposal (`ObjectDisposedException`) **before** the
public-only check, then throws `InvalidOperationException` if the instance was built via `FromPublicKey` — a
disposed, public-only instance's `Decrypt` throws `ObjectDisposedException`, not `InvalidOperationException`.

**`RsaEncryption.Sign`/`Verify`**: UTF-8-encode the data, call `RSA.SignData`/`RSA.VerifyData` with
`HashAlgorithmName.SHA256` and `RSASignaturePadding.Pkcs1`; `Sign` throws `InvalidOperationException` on a
public-only instance.

**`HmacHashing.ComputeBytes`**: validates `message`/`secretKey` not blank → UTF-8-encodes both → calls
`HMACSHA256.HashData`/`HMACSHA512.HashData` (static, one-shot, no cached algorithm instance) → zeroes the key
byte array with `CryptographicOperations.ZeroMemory` in a `finally` block regardless of success or exception.

**`HmacHashing.Verify`**: recomputes the HMAC over `message`/`secretKey`, decodes `expectedSignature` per
`signatureIsBase64` (catching `FormatException` and returning `false` on bad input), then compares with
`CryptographicOperations.FixedTimeEquals`.

**`ShaHashing.VerifyHash`**: recomputes the hash, hex-decodes both the actual and expected hash (catching
`FormatException` → `false`), compares with `CryptographicOperations.FixedTimeEquals`. `ignoreCase` is read but
has no effect since comparison happens on decoded bytes, not on the hex strings.

## Diagnostics & exceptions

| Exception type | Severity | When | Fix |
|---|---|---|---|
| `ArgumentException` | Error | `AddAesGcmEncryption`/`AddRsaEncryption` called with an empty/whitespace (non-null) key; `new AesGcmEncryption(key)` with a key containing `:`, or one that doesn't decode to 16/24/32 bytes; `DecryptString` given an empty/whitespace (non-null) `cipherPackage`, or a non-blank one that doesn't split into exactly 3 `:`-separated parts; `HmacHashing`/`ShaHashing` compute/verify called with an empty/whitespace (non-null) `message`/`secretKey`/`expectedSignature`/`expectedHex`. A `null` in any of these same slots throws `ArgumentNullException` instead — see below | Supply a non-blank key/message/signature/cipher package; regenerate the AES key without `:`; don't hand-edit a cipher package |
| `FormatException` | Error | Any Base64 decode call on invalid Base64 — `AesGcmEncryption(key)` with a non-Base64 key; `RsaEncryption(privateKeyBase64)` with non-Base64 input | Pass a value that is actually Base64 |
| `InvalidOperationException` | Error | `AesGcmEncryption.Encrypt`/`Decrypt(base64Key)` overload called with a key that doesn't match the instance's own `Key`; `RsaEncryption.Decrypt`/`Sign` called on a `FromPublicKey` instance | Use `EncryptString`/`DecryptString` (no key argument) or construct with the matching key; keep the private key on the side that needs to decrypt/sign |
| `CryptographicException` | Error | `AesGcm.Decrypt` tag verification fails — wrong key, tampered ciphertext, or mismatched `associatedData` between encrypt and decrypt | Decrypt with the same key and the same `associatedData` used to encrypt |
| `ObjectDisposedException` | Error | Any method called on a disposed `AesGcmEncryption` or `RsaEncryption` | Don't reuse a disposed instance; scope the `using` correctly |
| `ArgumentNullException` | Error | `EncryptString(plainText: null)`; `AesGcmEncryption.DecryptString(cipherPackage: null)`; `ShaHashing.ComputeSha256`/`ComputeSha512`/`VerifySha256`/`VerifySha512` with a null `input`/`expectedHex`; `AddAesGcmEncryption`/`AddRsaEncryption`/`new RsaEncryption(string)`/`RsaEncryption.FromPublicKey` called with a `null` key; `HmacHashing` compute/verify called with a null `message`/`secretKey`/`expectedSignature`; `Base64StringExtensions.ToBase64String`/`ToBase64UrlString` with a null `plainText`. Every one of these guards is `ArgumentException.ThrowIfNullOrWhiteSpace` or `ArgumentNullException.ThrowIfNull`, both of which throw `ArgumentNullException` — not `ArgumentException` — specifically for `null` | Pass a non-null value (empty string is allowed only for `ShaHashing.ComputeSha256`/`ComputeSha512`'s `input`; every other parameter listed here still rejects empty/whitespace via `ArgumentException`) |

This package ships no Roslyn analyzer, so there are no `DiagnosticDescriptor`-based warning/error IDs.

## Gotchas (source-verified)

- **Cipher registrations are singletons over the key you passed in.** One `AddAesGcmEncryption`/`AddRsaEncryption`
  call fixes the key for the process lifetime; a second call with a different key is silently dropped because both
  methods guard with `services.Any(s => s.ServiceType == typeof(...))` before registering. Evidence:
  `EncryptionSetup.AddAesGcmEncryption` / `AddRsaEncryption`.
- **`AddEncryptionServices()` never registers a cipher, by design.** It registers only `IShaHashing`/`IHmacHashing`.
  An earlier revision handed every resolution a throwaway random key, making ciphertext permanently unreadable —
  the package's own test suite pins the current, safer behavior structurally so it can't regress silently.
  `GetService<IAesGcmEncryption>()`/`GetService<IRsaEncryption>()` are `null` after `AddEncryptionServices()` alone.
- **The `base64Key` overload accepts a byte-identical but textually different key.** `AesGcmEncryption.KeyMatches`
  compares *decoded bytes* with `CryptographicOperations.FixedTimeEquals`, so a key with inserted
  whitespace/newlines that still decodes to the same bytes as the instance's own key is accepted.
- **`ignoreCase` on `IShaHashing.Verify*` is read but never changes the outcome.** Comparison runs on decoded
  bytes, which are already case-normalized — the parameter is effectively vestigial. Evidence:
  `ShaHashing.VerifyHash`; the `IShaHashing` XML doc says as much.
- **AES-GCM instances serialize encrypt/decrypt behind one instance lock.** `AesGcmEncryption.EncryptString`/
  `DecryptString` both take the same lock around the shared `AesGcm` handle, so one instance is thread-safe but
  not concurrent — high-throughput code resolving a shared singleton will contend on that lock.
- **HMAC key bytes are zeroed after every call, even on the failure path.** `HmacHashing.ComputeBytes` wraps
  `CryptographicOperations.ZeroMemory(keyBytes)` in a `finally`, so the caller's own `secretKey` string is
  unaffected (strings are immutable) but the intermediate byte buffer never survives the call.
- **`Verify*` never throws on a malformed signature/hash — it returns `false`.** Both `HmacHashing.Verify` and
  `ShaHashing.VerifyHash` catch the decode exception from a malformed Base64/hex string and return `false` instead
  of propagating. Only a blank `expectedSignature`/`expectedHex` throws `ArgumentException`.
- **No stream/file/`byte[]` overloads anywhere.** Every public method on `IAesGcmEncryption`, `IRsaEncryption`,
  `IHmacHashing`, `IShaHashing` is `string`-in/`string`-out; a large payload is fully materialized twice (UTF-8
  bytes, then Base64).
- **`AesGcmEncryption`/`RsaEncryption` are `IDisposable`; `HmacHashing`/`ShaHashing` are not.** DI disposes
  transient/singleton registrations for you, but a hand-built `AesGcmEncryption`/`RsaEncryption` needs an explicit
  `using` — `HmacHashing`/`ShaHashing` hold no native handle and have no `Dispose()` to call.

## Anti-patterns & hallucination traps

- **`IPasswordAesEncryption` / `PasswordAesEncryption` do not exist.** An earlier revision of the docs also
  mentioned this type; it was never shipped. To encrypt with a user-supplied password, derive a key yourself (e.g.
  `Rfc2898DeriveBytes`) and construct `AesGcmEncryption` with the derived Base64 key.
- **`IAesEncryption` / `AesEncryption` / `EncryptionSetup.AddAesEncryption` (AES-CBC) are removed, not
  deprecated.** Code calling them will not build. The CBC implementation kept a fixed IV embedded in the key,
  making ciphertext deterministic — a real information leak, not a style nit. `IAesGcmEncryption` /
  `AddAesGcmEncryption` is the only symmetric cipher this package ships.
- **Don't guess `DKNet.Svc.Encryption` as the namespace for the ciphers or hashers.** `IAesGcmEncryption`,
  `AesGcmEncryption`, `IRsaEncryption`, `RsaEncryption` live in `DKNet.Svc.Encryption.Ciphers`; `IHmacHashing`,
  `HmacHashing`, `IShaHashing`, `ShaHashing` live in `DKNet.Svc.Encryption.Hashing`. Only `EncryptionSetup` and
  `Base64StringExtensions` are at the root `DKNet.Svc.Encryption` namespace.
- **`AesGcmEncryption.Key` has no setter.** It is `{ get; }`, assigned once in the constructor — there is no way
  to rotate an instance's key in place; construct a new instance instead.
- **Do not call this package for transparent column encryption.** It has no `ModelBuilder`/`DbContextOptionsBuilder`
  extension, no attribute, and no `SaveChanges` hook — that surface belongs to the unrelated
  `DKNet.EfCore.Encryption` package.
- **Don't add manual byte-array plumbing.** There are no `byte[]` overloads to bypass — every call is
  string-in/string-out; converting to/from `byte[]` yourself just duplicates what `EncryptString`/`ComputeSha256`
  already do internally.
- **Don't treat a decoded AES-GCM cipher package as anything but opaque.** Its internal `nonce:tag:cipher` layout
  is an implementation detail; pass the whole Base64 string around rather than parsing it yourself.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Encryption` | Reach for it instead of this package when a column should be encrypted transparently on save and decrypted on load; the two packages don't share implementation. See `dknet-efcore-data-security`. |
| `DKNet.Svc.BlobStorage.Abstractions` | Encrypt a payload with this package's ciphers before handing the bytes to `SaveAsync` when the blob store itself must never see plaintext. See `dknet-blob-storage`. |
| `DKNet.Svc.Transformation` | Resolve a template's tokens with that package before the result is encrypted or signed with this one. |

## Testing notes

Stack: xUnit + Shouldly, plus `NetArchTest.Rules` for two architecture-guard suites — no TestContainers, no
Docker: the package is pure in-memory cryptography with no external dependency to fixture.

- **Round-trip + tamper** is the dominant test shape: encrypt/sign, then mutate one byte/char of the ciphertext,
  key, or signature and assert the specific exception or `false` result.
- An **architecture-guard suite** asserts via `NetArchTest.Rules` that the package assembly never depends on
  `MD5`, `SHA1`, `DES`, `TripleDES`, `RC2`, or `System.Random` — copy this pattern if you add a new
  crypto-adjacent type to your own codebase and want the same guarantee.
- A **concurrency test** asserts via reflection that `ShaHashing`/`HmacHashing` declare no `Lock`,
  `Dictionary<,>`, `HashAlgorithm`, or `HMAC` instance fields (no shared mutable/cached state), then round-trips
  100 concurrent `ComputeSha256` calls with two different inputs to confirm no cross-talk.
- **DI tests** resolve twice from the same `ServiceProvider` (and across scope boundaries for the AES-GCM
  singleton) and assert `ReferenceEquals` plus a working encrypt-here/decrypt-there round trip.
