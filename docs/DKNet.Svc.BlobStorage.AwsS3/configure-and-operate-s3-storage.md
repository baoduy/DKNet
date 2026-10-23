# Configure and operate S3 storage

Part of [DKNet.Svc.BlobStorage.AwsS3](index.md). Set connection and credential options, then work with the quirks
specific to S3's save, delete, and listing operations.

```csharp
// Container or EC2/ECS role — leave AccessKey and Secret unset to use the ambient AWS credential chain
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BlobService:S3:ConnectionString"] = "http://localhost:9000",
        ["BlobService:S3:BucketName"] = "dev",
        ["BlobService:S3:ForcePathStyle"] = "true",       // required by MinIO and most S3-compatible services
        ["BlobService:S3:DisablePayloadSigning"] = "true" // needed by some S3-compatible endpoints over plain HTTP
    })
    .Build();
```

When both `AccessKey` and `Secret` are non-blank the client uses `BasicAWSCredentials`; otherwise it falls back to
the AWS SDK's default credential chain (environment variables, shared profile, instance/task role). Which path was
taken is logged at `Information` level on first client construction.

![SaveAsync validates the stream, checks whether the key already exists, throws InvalidOperationException on an unflagged conflict, and otherwise uploads a single PutObject and returns the blob's name.](diagrams/configure-and-operate-s3-storage.svg)

The diagram cannot show the other operations: folder delete pages the prefix with `ListObjectsV2` and deletes each
page's keys in parallel with `Task.WhenAll` per page (not one key at a time — a plain per-key `DeleteObjectAsync`
loop, because this SDK version's batch-delete API needs a `Content-MD5` header that MinIO rejects); a folder delete
targeting the bucket root throws `ArgumentException` instead of deleting everything. `ListItemsAsync` follows
`NextContinuationToken` across every page, so a prefix with more than 1,000 objects is not truncated.

```csharp
var blob = new BlobDetails.BlobData("reports/2026/q1.pdf", BinaryData.FromString("...")) { Overwrite = true };
var key = await blobService.SaveAsync(blob); // "reports/2026/q1.pdf"

var url = await blobService.GetPublicAccessUrl(
    new BlobRequest("reports/monthly.pdf"), TimeSpan.FromMinutes(15));
```

`GetAsync` returns `null` and `CheckExistsAsync` returns `false` when S3 answers `404`; any other `AmazonS3Exception`
propagates. `GetPublicAccessUrl` returns a pre-signed URL valid for `expiresFromNow`, defaulting to one hour.

## ⚙️ Configuration reference

`S3Options` extends `BlobServiceOptions`, documented on the [Abstractions](../DKNet.Svc.BlobStorage.Abstractions/write-a-custom-blob-service-provider.md) feature page:

| Option | Type | Default | Effect |
|---|---|---|---|
| `ConnectionString` | `string` | *(required)* | S3 service endpoint URL, used as the SDK's `ServiceURL`; a value that does not start with `https` switches the client to plain HTTP. |
| `BucketName` | `string` | *(required)* | Target bucket; created on first use when missing. |
| `AccessKey` | `string?` | `null` | With `Secret`, selects `BasicAWSCredentials`; leave unset to use the ambient AWS credential chain. |
| `Secret` | `string?` | `null` | Paired with `AccessKey`. |
| `RegionEndpointName` | `string?` | `"us-east-1"` | Declared for configuration completeness; the client does not read this value — encode the region in `ConnectionString`. |
| `ForcePathStyle` | `bool` | `false` | Set `true` for MinIO and most S3-compatible services. |
| `DisablePayloadSigning` | `bool` | `false` | Passed to `PutObject`; needed by some S3-compatible endpoints over plain HTTP. |
| `S3Options.Name` (static) | `string` | `"BlobService:S3"` | Configuration section key `AddS3BlobService` binds from. |

## 🧱 Where it fits

`S3BlobService` derives from the `BlobService` base class in
[Abstractions](../DKNet.Svc.BlobStorage.Abstractions/index.md); application code depends on `IBlobService`, and only
the composition root knows this package exists. Pair it with
[DKNet.Svc.BlobStorage.Local](../DKNet.Svc.BlobStorage.Local/index.md) for tests and local runs when a MinIO
container is not wanted.

## ⚠️ Gotchas & limits

- **`RegionEndpointName` is inert.** It exists on `S3Options` but is never applied to the client configuration —
  encode the region into `ConnectionString`.
- **First use may create a bucket.** If the credentials can create buckets, a typo in `BucketName` silently produces
  a new empty bucket rather than failing.
- **`S3BlobService` is registered as a process-lifetime singleton**, and its `AmazonS3Client` is built and cached
  exactly once via a `Lazy<Task<AmazonS3Client>>` — the AWS SDK documents the client as thread-safe and meant to be
  long-lived, so this avoids a `ListBuckets`/`PutBucket` round trip per instance.
- **No multipart upload.** Every `SaveAsync` is a single `PutObject` — use the AWS SDK directly for multipart
  semantics on very large files.
- **Folder delete is per-key, not batched.** `DeleteObjectsAsync`'s batch API requires a `Content-MD5` header this
  SDK version won't compute and MinIO won't accept a substitute for; each page's keys are deleted with
  `Task.WhenAll` over individual `DeleteObjectAsync` calls instead.
- **No blob-name-length or upload-size cap** beyond the shared, opt-in `BlobServiceOptions` checks.
- **Registrations do not evict each other.** Multiple `IBlobService` providers can coexist in one
  `IServiceCollection` (e.g. S3 for archives, Local for scratch files); the last registration wins when a single
  `IBlobService` is resolved.
