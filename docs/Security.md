# Security

This page covers what DKNet supports with security fixes, how to report a vulnerability, and the
security-relevant behaviour of the packages themselves. The repository root
[`SECURITY.md`](https://github.com/baoduy/DKNet/blob/main/SECURITY.md) is the short version and links here.

## Supported versions

DKNet has **no long-term-support branches**. Packages are published from `main` by
`.github/workflows/dotnet-publish.yml`, which derives each version from the commit history with
[`paulhatch/semantic-version`](https://github.com/PaulHatch/semantic-version) in `major.minor.patch` form, so
there is no maintained matrix of older lines.

| Version | Supported |
|---|---|
| The latest version of a package on NuGet | ✅ Fixes land here |
| Any earlier version | ❌ Upgrade to the latest |

If upgrading is blocked by a breaking change, the [Migration Guide](Migration-Guide.md) documents the ones that
have shipped.

## Reporting a vulnerability

**Do not open a public issue for a security problem.**

Use GitHub's private vulnerability reporting for this repository:
<https://github.com/baoduy/DKNet/security/advisories/new>. That channel is private to the maintainers until an
advisory is published.

Please include the affected package and version, what an attacker can do, and a minimal reproduction. There is
no published response-time commitment — DKNet is a volunteer-maintained open-source project.

## Automated checks in CI

Two scanners run over the repository, and their findings are triaged like any other defect:

- **CodeQL** — `.github/workflows/codeql.yml`, C# analysis on push and pull request.
- **Qodana** — configured by `qodana.yaml`.

`src/Directory.Build.props` sets `TreatWarningsAsErrors` and `Nullable=enable` solution-wide, so a nullability
mistake in a security-relevant path fails the build rather than shipping.

## Security-relevant package behaviour

Each of these is a decision the packages make on your behalf. Read the ones you use.

### Secrets and randomness

- **Use `DKNet.RandomCreator` for anything secret.** It wraps
  `System.Security.Cryptography.RandomNumberGenerator`; `System.Random` is not cryptographically secure and its
  output is predictable. See [DKNet.RandomCreator](Core/DKNet.RandomCreator.md).

### Encryption keys

- **`AddEncryptionServices()` registers no cipher.** It registers `IShaHashing` and `IHmacHashing` only. AES and
  RSA are opt-in through `AddAesGcmEncryption(base64Key)` and `AddRsaEncryption(privateKeyBase64)`, so the key is
  something you supply and persist. An earlier release registered AES over a randomly generated key that was
  never persisted — ciphertext produced under that registration cannot be recovered. Details:
  [Migration Guide](Migration-Guide.md#dknetsvcencryption--aes-ciphers-are-now-opt-in).
- **`DKNet.EfCore.Encryption` fails closed.** It needs an `IEncryptionKeyProvider` registered through
  `AddEfCoreEncryption<TKeyProvider>()`; without usable key material the model build fails rather than storing
  plaintext. See [DKNet.EfCore.Encryption](EfCore/DKNet.EfCore.Encryption.md).
- **`IAesEncryption` (AES-CBC) is obsolete.** Prefer `IAesGcmEncryption`, which is authenticated.

### Row-level isolation

- **`AddDataOwnerProvider<TDbContext, TProvider>()` requires `IDataOwnerDbContext` on the context.** The
  constraint exists because the older, unconstrained signature allowed a context the ownership filter could not
  read — which silently disabled row isolation.
- **An empty `AccessibleKeys` denies access; it is never read as "see everything".** Unrestricted access is an
  explicit opt-in via `IsUnrestrictedAccess`, which defaults to `false`.
- **The filter is registered process-wide.** `AddDataOwnerProvider` adds it to a **static** model-builder list, so
  every `DbContext` that calls `UseAutoConfigModel()` applies it. A second context holding `IOwnedBy` entities
  must also implement `IDataOwnerDbContext`, or keep those entities out of its model.
- **Dropping `UseAutoConfigModel<TContext>()` removes the filter.** This matters most in tests: without it a
  query returns rows it never would in production. See [DKNet.EfCore.DataAuthorization](EfCore/DKNet.EfCore.DataAuthorization.md).

### Audit trails

- **Sensitive-looking values are redacted by default.** Under the default `AuditPropertyPolicy.RedactSensitive`,
  properties whose name matches a built-in deny-list (`password`, `secret`, `token`, `apikey`, `ssn`,
  `creditcard`, `connectionstring`, `privatekey`, …) and any `SecureString` property are captured as
  `"***REDACTED***"` — the field still shows that it changed, never its value.
- **`[AuditLog]` on a property forces plaintext capture.** Do not put it on a secret. `[SensitiveData]` always
  redacts and cannot be overridden by `[AuditLog]`; `[IgnoreAuditLog]` removes the property from the trail
  entirely. See [DKNet.EfCore.AuditLogs](EfCore/DKNet.EfCore.AuditLogs.md).

### File storage

- **`DKNet.Svc.BlobStorage.Local` rejects path traversal.** A resolved path that escapes the configured
  `RootFolder` raises `UnauthorizedAccessException` rather than reading or writing outside it.
- **All three adapters share the same validation.** `BlobService.ValidateFile` checks `MaxFileNameLength`,
  `IncludedExtensions`, and `MaxFileSizeInMb` and throws `FileLoadException` on a violation; Azure, S3, and local
  all call it at the top of their `SaveAsync`, so an extension allow-list applies uniformly. It is `protected
  virtual`, so a custom adapter of your own must call it — the base class does not force it. See
  [DKNet.Svc.BlobStorage.Abstractions](Services/DKNet.Svc.BlobStorage.Abstractions.md).

### Idempotency keys

- **A client-supplied key is validated before use.** `DKNet.AspCore.Idempotency` checks the header's presence,
  format, and length before it reaches a store.
- **Keys are scoped per caller, so one client cannot replay another's response.** The composite key includes a
  caller scope resolved from the authenticated user, then an HMAC-SHA256 digest of the `Authorization` header
  (only when `IdempotencyOptions.ScopeHmacSecret` is configured — otherwise that fallback is skipped), then the
  client IP if `IncludeClientIpInScope` is set. Neither the secret nor the raw header is ever logged. A
  `KeyScopeResolver` replaces the whole chain. See
  [DKNet.AspCore.Idempotency](AspNetCore/DKNet.AspCore.Idempotency.md).
- **Register the store, and register it once.** `.RequiredIdempotentKey()` always adds the filter, but the filter
  cannot be constructed unless `AddIdempotentKey`/`AddIdempotencyWith*Store` registered an
  `IIdempotencyKeyStore` — the route then fails on its first request. `AddIdempotentKey` is also
  first-registration-wins: a second call with different options is silently ignored, so a stricter `Expiration`
  or a `ScopeHmacSecret` added later may never take effect. Cover the registration with a start-up test.
- **The default distributed-cache store is not atomic.** The parameterless `AddIdempotentKey()` is `[Obsolete]`
  for that reason; use the SQL Server, PostgreSQL, or Redis store, which reserve the key atomically.

## Your responsibilities

DKNet does not authenticate or authorise callers. It gives you row-level ownership filtering, column encryption,
audit redaction, and idempotency; authentication, authorisation policies, transport security, and key rotation
remain yours. Keep the values listed in
[Environment and secrets](Configuration.md#environment-and-secrets) out of source control.
