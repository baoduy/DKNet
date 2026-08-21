# Services

Services are DKNet's pluggable, provider-agnostic adapters: application code depends on a small interface
(`IBlobService`, `IPdfGenerator`, `ITransformerService`, the various encryption/hashing interfaces), and a concrete
package supplies the implementation. Swapping an implementation — a different blob provider, a different
encryption algorithm — is a DI change, not a rewrite of business logic. See
[Onion Architecture](../Architecture.md#onion-architecture) for how this keeps infrastructure out of the domain and
application layers.

## Blob storage

One abstraction, three providers — depend on `IBlobService` from Abstractions and register whichever provider
package matches your storage backend.

- [DKNet.Svc.BlobStorage.Abstractions](./DKNet.Svc.BlobStorage.Abstractions.md) — the `IBlobService` contract, shared
  models, and validation options every provider implements.
- [DKNet.Svc.BlobStorage.AwsS3](./DKNet.Svc.BlobStorage.AwsS3.md) — AWS S3 and S3-compatible services (MinIO,
  Cloudflare R2).
- [DKNet.Svc.BlobStorage.AzureStorage](./DKNet.Svc.BlobStorage.AzureStorage.md) — Azure Blob Storage, including
  managed-identity auth.
- [DKNet.Svc.BlobStorage.Local](./DKNet.Svc.BlobStorage.Local.md) — local filesystem, for dev/test/single-instance
  deployments.

## Cryptography

- [DKNet.Svc.Encryption](./DKNet.Svc.Encryption.md) — AES-GCM/RSA encryption, HMAC/SHA hashing, Base64 helpers,
  called explicitly from your own code. (Not the same package as `DKNet.EfCore.Encryption`, which encrypts EF Core
  columns transparently — the page explains which one to reach for.)

## Documents and data

- [DKNet.Svc.PdfGenerators](./DKNet.Svc.PdfGenerators.md) — Markdown/HTML → PDF via headless Chromium, with table of
  contents, syntax highlighting, and themes.
- [DKNet.Svc.Transformation](./DKNet.Svc.Transformation.md) — template-token substitution against a plain object or
  dictionary, independent of any templating engine.

## Picking a package

| Need | Package |
|---|---|
| Store/retrieve files without coupling to a specific cloud | `BlobStorage.Abstractions` + one provider |
| Encrypt/hash a value at the point you call it | `Svc.Encryption` |
| Encrypt an EF Core column transparently | `EfCore.Encryption` (not covered here) |
| Turn Markdown/HTML into a PDF | `Svc.PdfGenerators` |
| Fill a text template from an object | `Svc.Transformation` |
