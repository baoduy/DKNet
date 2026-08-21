# DKNet.Svc.BlobStorage.AwsS3

AWS S3 (and S3-compatible services such as Cloudflare R2 or MinIO) implementation of
[`IBlobService`](./DKNet.Svc.BlobStorage.Abstractions.md). For the operations, models, and validation rules every
provider shares, read the Abstractions page first — this page covers only what's specific to S3.

## When to reach for it

Use this provider when your blobs live in AWS S3 or an S3-compatible object store. It supports both real AWS S3 and
`ForcePathStyle` endpoints (MinIO, R2, and similar).

## Install and minimal wiring

```bash
dotnet add package DKNet.Svc.BlobStorage.AwsS3
```

```csharp
// appsettings.json
// { "BlobService": { "S3": { "BucketName": "my-bucket", "ConnectionString": "https://s3.amazonaws.com", "AccessKey": "...", "Secret": "..." } } }

builder.Services.AddS3BlobService(builder.Configuration);
```

```csharp
public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveReportAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)), ct);
}
```

`AddS3BlobService(IServiceCollection, IConfiguration)` binds `S3Options` from the `"BlobService:S3"` section and
registers `IBlobService → S3BlobService` as `Scoped`; the call is idempotent (safe to call more than once).

## Provider-specific behavior

- **Bucket auto-creation.** The client lazily lists buckets and creates the configured bucket if it doesn't exist yet
  — no manual provisioning step required for local/dev setups.
- **Leading-slash handling.** `CheckExistsAsync` (and friends) trim a leading `/` before calling S3 — a leading slash
  would double up under `ForcePathStyle`/MinIO and break SigV4 request signing.
- **Folder delete is one key at a time.** `DeleteFolderAsync` deletes keys individually rather than batching through
  `DeleteObjectsAsync`.
  > ponytail: this SDK version's batch delete needs a Content-MD5 header that MinIO rejects; per-key delete is the
  > simple thing that works everywhere. Revisit batching if folder deletes with many keys become a throughput problem
  > against real AWS S3.
- **Overwrite conflict.** `SaveAsync` throws `InvalidOperationException` if the key already exists and `Overwrite` is
  `false` (the shared default).
- **Public URLs are pre-signed.** `GetPublicAccessUrl` returns a pre-signed URL, default expiry `TimeSpan.FromHours(1)`
  when `expiresFromNow` is omitted; throws `NotSupportedException` if the SDK returns no URL.
- **No multipart upload.** `S3BlobService` does not implement S3 multipart upload — every `SaveAsync` is a single
  `PutObject`-style call. Reach for the AWS SDK directly (outside this package) if you need multipart semantics for
  very large files.

## Configuration — `S3Options` (extends `BlobServiceOptions`)

| Property | Default | Notes |
|---|---|---|
| `string ConnectionString` | *(required)* | S3 service endpoint / region URL. |
| `string BucketName` | *(required)* | |
| `string? AccessKey` | `null` | |
| `string? Secret` | `null` | |
| `string? RegionEndpointName` | `"us-east-1"` | Declared for configuration completeness; the client does not currently read this value when building the connection — set `ConnectionString` to point at the right region/endpoint. |
| `bool ForcePathStyle` | `false` | Set `true` for MinIO and most S3-compatible services. |
| `bool DisablePayloadSigning` | `false` | |
| `static string Name` | `"BlobService:S3"` | Configuration section key used by `AddS3BlobService`. |

Plus the shared `IncludedExtensions`/`MaxFileNameLength`/`MaxFileSizeInMb` from
[`BlobServiceOptions`](./DKNet.Svc.BlobStorage.Abstractions.md#configuration--blobserviceoptions).

## Composing with other DKNet packages

Depend on `IBlobService` from `DKNet.Svc.BlobStorage.Abstractions` in application code; only the composition root
needs to know `AddS3BlobService` exists. Pairs with `DKNet.EfCore.Events`/`DKNet.EfCore.Repos` the same way every
provider does — see the Abstractions page.

## Gotchas and limits

- `RegionEndpointName` is present on `S3Options` but not wired into the actual AWS client configuration — don't rely
  on it; encode region into `ConnectionString`.
- No blob-name-length or upload-size cap beyond the shared, opt-in `BlobServiceOptions` checks.
- Multiple `IBlobService` providers can be registered side by side (e.g. S3 for archives, Local for scratch files) —
  each `Add*` call only adds its own registration and does not evict another provider's.
