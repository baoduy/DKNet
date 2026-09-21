# DKNet.Svc.BlobStorage.AwsS3

| Field | Value |
|---|---|
| Area | Services |
| NuGet | `dotnet add package DKNet.Svc.BlobStorage.AwsS3` |
| Docs | [DKNet.Svc.BlobStorage.AwsS3.md](https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.BlobStorage.AwsS3.md) |
| Source | [src/Services/DKNet.Svc.BlobStorage.AwsS3](https://github.com/baoduy/DKNet/tree/dev/src/Services/DKNet.Svc.BlobStorage.AwsS3) |
| Depends on (DKNet) | `DKNet.Svc.BlobStorage.Abstractions` (`IBlobService`, `BlobService`, `BlobServiceOptions`, `BlobDetails`, `BlobRequest`) |
| Depends on (3rd party) | `AWSSDK.Core`, `AWSSDK.S3`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options` |
| Target framework | net10.0 |

## Purpose

The AWS S3 (and S3-compatible: MinIO, Cloudflare R2) implementation of `IBlobService`. `S3BlobService` derives from
the shared abstract `BlobService` base and implements the CRUD/listing/URL-signing operations as calls against an
`AmazonS3Client`, built lazily on first use and cached for the life of the (singleton-registered) service.

It is **not** a general-purpose AWS SDK wrapper — it exposes only the `IBlobService` surface (no multipart upload,
no bucket policy/ACL management, no object versioning); reach for the AWS SDK directly for anything outside that
surface, and use `DKNet.Svc.BlobStorage.Local` instead for tests/local dev that shouldn't depend on a real or
containerized S3-compatible endpoint.

## Entry points

| Call | Signature | Notes |
|---|---|---|
| `AddS3BlobService` | `public static IServiceCollection AddS3BlobService(this IServiceCollection services, IConfiguration configuration)` — static class `S3Setup`, namespace `DKNet.Svc.BlobStorage.AwsS3` (**not** ambient — add `using DKNet.Svc.BlobStorage.AwsS3;` to call it) | Binds `S3Options` from the config section named by `S3Options.Name` (`"BlobService:S3"`). Registers `IBlobService → S3BlobService` as **Singleton** (guarded — a second call is a no-op if that exact registration already exists). Must run once at composition root before `IBlobService` is resolved. |

## Public surface

### `DKNet.Svc.BlobStorage.AwsS3`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `S3Setup` | static class | DI registration extension | `AddS3BlobService(this IServiceCollection, IConfiguration)` — see Entry points |
| `S3Options` | class (`: BlobServiceOptions`) | Provider configuration bound from `"BlobService:S3"` | `string? AccessKey { get; set; }` (default `null`) · `string BucketName { get; set; }` (required, `= null!`) · `string ConnectionString { get; set; }` (required, `= null!`) · `bool DisablePayloadSigning { get; set; }` (default `false`) · `bool ForcePathStyle { get; set; }` (default `false`) · `static string Name => "BlobService:S3"` · `string? RegionEndpointName { get; set; } = "us-east-1"` (declared, **never read** by the client) · `string? Secret { get; set; }` (default `null`) |
| `S3BlobService` | sealed class (`: BlobService, IDisposable`) | Concrete `IBlobService` implementation over `AmazonS3Client` | `S3BlobService(IOptions<S3Options> options, ILogger<S3BlobService> logger)` · `override Task<bool> CheckExistsAsync(BlobRequest, CancellationToken = default)` · `override Task<bool> DeleteAsync(BlobRequest, CancellationToken = default)` · `override Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest, CancellationToken = default)` · `override Task<Stream?> OpenReadAsync(BlobRequest, CancellationToken = default)` · `override Task<Uri> GetPublicAccessUrl(BlobRequest, TimeSpan? expiresFromNow = null, CancellationToken = default)` · `override IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(BlobRequest, CancellationToken = default)` · `override Task<string> SaveAsync(BlobDetails.BlobData, CancellationToken = default)` · `override Task<string> SaveAsync(BlobDetails.BlobStreamData, CancellationToken = default)` · `void Dispose()` |

A consumer touches only `AddS3BlobService` and `S3Options` directly; `S3BlobService` is resolved through
`IBlobService` (from `DKNet.Svc.BlobStorage.Abstractions`), never referenced by its concrete type — DI registers
only the interface, so `GetRequiredService<S3BlobService>()` throws (see Anti-patterns).

## Options & defaults

| Option | Type | Default | Effect |
|---|---|---|---|
| `ConnectionString` | `string` | *(required)* | Passed as `AmazonS3Config.ServiceURL`; a value not starting with `"https"` (case-insensitive) sets `UseHttp = true`. |
| `BucketName` | `string` | *(required)* | Target bucket; the first client build lists buckets and issues `PutBucketAsync` if missing. |
| `AccessKey` / `Secret` | `string?` / `string?` | `null` / `null` | When **both** non-blank, the client uses `BasicAWSCredentials`; otherwise it falls back to the AWS SDK's ambient credential chain (env vars, shared profile, instance/task role). |
| `RegionEndpointName` | `string?` | `"us-east-1"` | **Inert** — never read when building the client. Encode the region into `ConnectionString` instead. |
| `ForcePathStyle` | `bool` | `false` | Maps 1:1 to `AmazonS3Config.ForcePathStyle`; set `true` for MinIO/most S3-compatible services. |
| `DisablePayloadSigning` | `bool` | `false` | Passed to `PutObjectRequest.DisablePayloadSigning` on every `SaveAsync`. |
| `S3Options.Name` (static) | `string` | `"BlobService:S3"` | Configuration section `AddS3BlobService` binds. |
| `IncludedExtensions` / `MaxFileNameLength` / `MaxFileSizeInMb` | inherited | `[]` / `0` / `0` | `0` or empty means the corresponding check is skipped (opt-in). |

Two more `AmazonS3Config` fields are set unconditionally when the client is built and are not exposed as options:
`RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED` and
`ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED`.

## Usage patterns

### Register against real AWS S3

**When**: composition root, explicit keys or an ambient credential chain.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AwsS3;

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BlobService:S3:ConnectionString"] = "https://s3.ap-southeast-1.amazonaws.com",
        ["BlobService:S3:BucketName"] = "app-documents"
        // AccessKey/Secret omitted on purpose -> ambient AWS credential chain (env/instance role)
    })
    .Build();

var services = new ServiceCollection();
services.AddS3BlobService(configuration);
var provider = services.BuildServiceProvider();

var blobService = provider.GetRequiredService<IBlobService>();
```

**Notes**: `AddS3BlobService` is idempotent — calling it twice does not register a second `IBlobService →
S3BlobService` entry. Remember `using DKNet.Svc.BlobStorage.AwsS3;` — unlike the Local and Azure setup classes,
`S3Setup` is not declared in the ambient `Microsoft.Extensions.DependencyInjection` namespace.

### Save a blob, allow overwrite

**When**: uploading/replacing a document by key.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Threading;

public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveReportAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(
            new BlobDetails.BlobData("reports/2026/q1.pdf", BinaryData.FromString("placeholder"))
            {
                Overwrite = true
            },
            ct);
}
```

**Notes**: `Overwrite` defaults to `false`; a second `SaveAsync` for the same key without `Overwrite = true` throws
`InvalidOperationException`. `SaveAsync(BlobDetails.BlobStreamData, ...)` is also overridden directly (not via the
interface's buffering default), so streaming a large upload does not buffer the whole payload into memory first.

### Check existence, then read

**When**: avoid a `GetAsync` round-trip when only presence matters.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AwsS3;
using System.Threading;

var services = new ServiceCollection();
services.AddS3BlobService(new ConfigurationBuilder().Build());
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();

var request = new BlobRequest("reports/2026/q1.pdf");
if (await blobService.CheckExistsAsync(request, CancellationToken.None))
{
    var result = await blobService.GetAsync(request, CancellationToken.None);
    var contentType = result?.Details?.ContentType;
}
```

**Notes**: `CheckExistsAsync` returns `false` on an S3 `404`; any other `AmazonS3Exception` propagates. `GetAsync`
returns `null` on `404` for the same reason.

### Delete a "folder" (prefix)

**When**: `BlobRequest.Name` has no file extension, so its default `Type` is `BlobTypes.Directory`.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AwsS3;
using System.Threading;

var services = new ServiceCollection();
services.AddS3BlobService(new ConfigurationBuilder().Build());
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();

var deleted = await blobService.DeleteAsync(new BlobRequest("reports/2026"), CancellationToken.None);
```

**Notes**: deletes every key under the prefix one `DeleteObjectAsync` call at a time (not a batch
`DeleteObjectsAsync` — see Gotchas), then deletes the prefix marker itself. Deleting an already-empty/non-existent
prefix still returns `true` and does not throw.

### Pre-signed, time-limited download link

**When**: sharing a private object without exposing AWS credentials.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AwsS3;
using System.Threading;

var services = new ServiceCollection();
services.AddS3BlobService(new ConfigurationBuilder().Build());
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();

var url = await blobService.GetPublicAccessUrl(
    new BlobRequest("reports/monthly.pdf"),
    TimeSpan.FromMinutes(15),
    CancellationToken.None);
```

**Notes**: throws `NotSupportedException` if the underlying SDK call (`GetPreSignedURLAsync`) returns no URL.
`expiresFromNow` defaults to `TimeSpan.FromHours(1)` when omitted.

### List a prefix

**When**: enumerating objects under a directory-like key.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AwsS3;
using System.Threading;

var services = new ServiceCollection();
services.AddS3BlobService(new ConfigurationBuilder().Build());
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();

await foreach (var item in blobService.ListItemsAsync(new BlobRequest("reports/2026"), CancellationToken.None))
    Console.WriteLine($"{item.Name}: {item.Type}");
```

**Notes**: pages internally via `ListObjectsV2Request.ContinuationToken` until `IsTruncated` is false, so this
returns every object under the prefix regardless of count — it is not limited to a single page. `Details` is
populated for `File` entries (`ContentType` is always `""`), `null` for `Directory` entries (S3 keys ending in
`'/'`).

## Runtime behaviour

1. `AddS3BlobService` binds `S3Options` and registers `IBlobService → S3BlobService` as **Singleton**. No
   `AmazonS3Client` is built yet.
2. On the **first** call to any `S3BlobService` operation, a `Lazy<Task<AmazonS3Client>>`
   (`LazyThreadSafetyMode.ExecutionAndPublication`) is awaited so concurrent first callers share one build:
   - The client is built from `ConnectionString` (→ `ServiceURL`, and `UseHttp` inferred from the scheme),
     `ForcePathStyle`, plus the fixed checksum settings.
   - `BasicAWSCredentials(AccessKey, Secret)` is used when both are non-blank, else no explicit credentials
     (ambient AWS chain); an `Information`-level log line records which path was taken.
   - `ListBucketsAsync` runs; if `BucketName` is not present, `PutBucketAsync` creates it.
   - The resulting `AmazonS3Client` is cached for the process lifetime (the service is a singleton) and reused by
     every subsequent operation and every consumer that resolves `IBlobService`.
3. Every operation first calls `GetBlobLocation(blob)` (from the `BlobService` base, prefixes `/` if absent) then
   `.TrimStart('/')` before using the result as the S3 key — the leading slash exists only for the base class's own
   convention and is stripped before it reaches the SDK.
4. `SaveAsync(BlobData/BlobStreamData)`: `ValidateFile` (name length, extension allow-list, size ceiling) →
   `CheckExistsAsync` → throws `InvalidOperationException` if it exists and `Overwrite` is `false` → single
   `PutObjectAsync` with `ContentType` and `DisablePayloadSigning`.
5. `DeleteAsync` for a directory: loops `ListObjectsV2Async` on the prefix, deletes every key in the page one
   `DeleteObjectAsync` call at a time via `Task.WhenAll`, repeats until a page comes back empty, then deletes the
   prefix marker itself.
6. `ListItemsAsync`: loops `ListObjectsV2Async` with `ContinuationToken` until `IsTruncated` is false, classifying
   each object as `Directory` only when its key ends with `/`, otherwise `File`.
7. `Dispose()` disposes the cached `AmazonS3Client` field on the instance that built it and nulls the field, making
   a second `Dispose()` call a no-op.

## Diagnostics & exceptions

| Exception | When | Fix |
|---|---|---|
| `InvalidOperationException` | `SaveAsync` called for a key that already exists and `Overwrite` is `false`. | Pass `Overwrite = true`, or check `CheckExistsAsync` first and branch. |
| `FileLoadException` (from base `BlobService.ValidateFile`) | Name exceeds `MaxFileNameLength`, extension missing/not in `IncludedExtensions`, or size exceeds `MaxFileSizeInMb` — only when the corresponding option is configured. | Configure `BlobServiceOptions` limits to match real constraints, or fix the offending upload. |
| `NotSupportedException` | `GetPublicAccessUrl`'s underlying `GetPreSignedURLAsync` call returns no URL. | Verify bucket/credentials support pre-signed URL generation. |
| `AmazonS3Exception` (re-thrown) | Any S3 error other than `404 NotFound` on `CheckExistsAsync`/`GetAsync`/`OpenReadAsync`. | Inspect `StatusCode`/message; typically a credentials or permissions problem. |
| `ArgumentNullException` (from base `BlobService` constructor) | `options` is `null` when constructing `S3BlobService` directly (bypassing DI). | Always resolve via `IOptions<S3Options>` through DI. |

No analyzer/`DiagnosticDescriptor` surface — this is a runtime provider, not a source generator or analyzer.

## Gotchas

- **Registration is Singleton, not Scoped.** `S3Setup.AddS3BlobService` registers `IBlobService → S3BlobService`
  as a singleton; the `AmazonS3Client` is built once for the process and the bucket-ensure round trip runs exactly
  once, even under concurrent first callers. Do not assume a fresh client per request scope.
- **`RegionEndpointName` is inert.** It exists on `S3Options` with a default of `"us-east-1"`, but building the
  client never reads it — encode the region into `ConnectionString` instead.
- **Bucket auto-creation on first use.** The client build calls `PutBucketAsync` when `BucketName` is missing from
  `ListBucketsAsync` — a typo in `BucketName` silently creates a new empty bucket rather than failing.
- **No multipart upload.** Every `SaveAsync` path ends in one `PutObjectAsync` call — there is no chunked/multipart
  path for very large files.
- **Per-key delete, not batch delete.** Folder delete issues one `DeleteObjectAsync` per key instead of a single
  `DeleteObjectsAsync`, because this SDK version's batch API needs a Content-MD5 header that MinIO rejects (marked
  with a `ponytail:` comment in source). Throughput on very large folder deletes against real AWS S3 is the named
  upgrade path, not yet built.
- **Directory classification is by trailing `/` in the key, not by size.** `ListItemsAsync` classifies an object as
  a directory only when its key ends with `/` — a 0-byte or 1-byte real file is still classified as `File`.
- **`ListItemsAsync` pages through all results.** It loops on `ListObjectsV2Request.ContinuationToken` until
  `IsTruncated` is false — it is not limited to a single page.
- **`Details.ContentType` on listed items is always `string.Empty`.** `ListItemsAsync` never populates it — call
  `GetAsync` for the real content type.
- **Credentials selection is all-or-nothing.** The client is built with `BasicAWSCredentials` only when *both*
  `AccessKey` and `Secret` are non-blank; setting only one of them silently falls through to the ambient AWS
  credential chain rather than erroring.
- **Leading-slash trimming is a genuine footgun for `ForcePathStyle`.** Every operation calls
  `GetBlobLocation(blob).TrimStart('/')` — the base class's `GetBlobLocation` always prepends `/`, and this
  provider must strip it again or the resulting key breaks SigV4 signing under path-style addressing.

## Anti-patterns & hallucination traps

- Do **not** call `new AmazonS3Client(...)` yourself and hand it to `S3BlobService` — there is no constructor
  overload accepting a client; the only public constructor is `S3BlobService(IOptions<S3Options>,
  ILogger<S3BlobService>)`, and the client is built internally.
- There is no `S3Options.Region` or `S3Options.RegionEndpoint` property — the actual (inert) property name is
  `RegionEndpointName`, and it has no effect regardless of what you set it to.
- There is no `MaxKeys`/page-size option on `S3Options` or `ListItemsAsync` — pagination is automatic and internal
  (`ContinuationToken`), not a caller-tunable parameter.
- `AddS3BlobService` takes only `(IServiceCollection, IConfiguration)` — there is no overload taking an
  `Action<S3Options>` delegate or a bucket name string directly.
- Do not try to resolve `S3BlobService` (the concrete type) directly from the DI container — `AddS3BlobService`
  registers **only** `IBlobService`, so `provider.GetService<S3BlobService>()` returns `null` and
  `GetRequiredService<S3BlobService>()` throws `InvalidOperationException`. Code that genuinely needs the concrete
  type (e.g. tests exercising `Dispose()`) constructs it directly with `new S3BlobService(Options.Create(...),
  logger)` instead of resolving it from a container.
- Multiple `IBlobService` registrations (e.g. one `AddS3BlobService` and one `AddLocalDirectoryBlobService`/
  `AddAzureStorageAdapter`) do not throw and do not evict each other — the last registration in the collection wins
  when a single `IBlobService` is resolved; use a keyed/named registration pattern yourself if more than one must
  be resolvable simultaneously (this package provides no built-in multiplexing).
- Never hand-roll batch object deletion via `DeleteObjectsAsync` expecting it to be used here — the source
  deliberately avoids it (see Gotchas); don't "fix" the folder-delete path to use the batch API without re-testing
  against MinIO.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.Svc.BlobStorage.Abstractions` | Always the dependency application code references for `IBlobService`, `BlobRequest`, `BlobDetails.*`, `BlobServiceOptions`; this package is composition-root-only. |
| `DKNet.Svc.BlobStorage.Local` | Swap in for local dev/tests instead of a real or containerized S3-compatible endpoint when you don't want Docker/MinIO in the loop. |
| `DKNet.Svc.BlobStorage.AzureStorage` | Reach for it instead of this package when blobs live in an Azure Storage account. |
| `DKNet.EfCore.Events` / `DKNet.EfCore.Specifications` | Pair with `IBlobService` the same way any provider does (e.g. persist a blob key on an aggregate, fetch/save via specifications) — no S3-specific coupling. |

## Testing notes

- S3-specific tests live in the shared blob-storage test project alongside the other providers; there is no
  dedicated `*.AwsS3.Tests` project.
- The fixture spins up a **Testcontainers.Minio** container (image pinned by digest, not tag) and builds a real
  `IBlobService` through `AddS3BlobService` against it — no mocking of the AWS SDK.
- Canonical assertions: round-trip via `SaveAsync` → `GetAsync`/`CheckExistsAsync`/`ListItemsAsync`/`DeleteAsync`,
  using Shouldly (`ShouldBeTrue`, `ShouldBeNull`, `ShouldContain`, etc.).
- Regression-style tests worth copying the pattern of: a zero-byte/one-byte-file classification test (pins the
  trailing-slash directory heuristic), a 1001-object upload test forcing a real `ContinuationToken` round trip, a
  concurrent-first-callers test asserting the client-build/bucket-probe path runs exactly once under concurrency,
  and a direct-construction test exercising `Dispose()` twice for idempotency.
- DI-idempotency and chaining (`AddS3BlobService` called twice registers the implementation once; the call returns
  the same `IServiceCollection`) are covered by the shared setup-tests file alongside the other two providers'
  equivalents.
