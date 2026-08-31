# DKNet.Svc.BlobStorage.AzureStorage

Azure Blob Storage implementation of [`IBlobService`](./DKNet.Svc.BlobStorage.Abstractions.md), backed by
`Azure.Storage.Blobs`.

For the operations, models, and validation rules every provider shares, read the
[Abstractions](./DKNet.Svc.BlobStorage.Abstractions.md) page first — this page covers only what is specific to Azure.

## ✨ Why use it?

- **Connection-string or managed-identity auth.** Bind a connection string from configuration, or hand the adapter a
  `BlobServiceClient` you built yourself with `DefaultAzureCredential` — no credential plumbing inside your own code.
- **The container provisions itself.** `CreateIfNotExistsAsync` runs on first use, so a fresh environment needs no
  manual container step.
- **Read-only share links out of the box.** `GetPublicAccessUrl` returns a SAS URI scoped to a single blob, valid for a
  day unless you ask for less.
- **Folder semantics over a flat namespace.** Azure has no real directories; `DeleteAsync` on a directory request walks
  the prefix tree and removes the nested blobs and folder markers for you.

## 🚀 Quick Start

```bash
dotnet add package DKNet.Svc.BlobStorage.AzureStorage
```

```csharp
// appsettings.json
// { "BlobService": { "AzureStorage": { "ConnectionString": "...", "ContainerName": "documents" } } }

builder.Services.AddAzureStorageAdapter(builder.Configuration);
```

```csharp
public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveReportAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)), ct);
}
```

`AddAzureStorageAdapter(IConfiguration)` binds `AzureStorageOptions` from the `"BlobService:AzureStorage"` section and
registers `IBlobService → AzureStorageBlobService` as `Scoped`; the call is idempotent. Set code-only options such as
`BlobServiceClientFactory` with a follow-up `Configure` call — see the next section, and read the
[Gotchas](./DKNet.Svc.BlobStorage.AzureStorage.md) before reaching for the `Action<AzureStorageOptions>` overload.

## 🧩 Features

### Managed identity via `BlobServiceClientFactory`

`BlobServiceClientFactory` is the hook for Azure AD / managed-identity auth — there is no separate "use Azure AD" flag.
When it is set it takes priority over `ConnectionString`; when neither is set, the first blob operation throws
`ArgumentException`. Because the factory is a delegate, configuration cannot bind it — layer it on with the standard
options API:

```csharp
using Azure.Identity;
using Azure.Storage.Blobs;
using DKNet.Svc.BlobStorage.AzureStorage;

builder.Services.AddAzureStorageAdapter(builder.Configuration);
builder.Services.Configure<AzureStorageOptions>(options =>
{
    options.ContainerName = "documents";
    options.BlobServiceClientFactory = _ => Task.FromResult(
        new BlobServiceClient(
            new Uri("https://myaccount.blob.core.windows.net"),
            new DefaultAzureCredential()));
});
```

The factory receives the resolved `AzureStorageOptions`, so it can read your own configuration values off it, and it is
invoked once per scoped service instance — the resulting container client is cached.

### Container auto-creation

The first operation resolves the container client for `ContainerName` and calls `CreateIfNotExistsAsync`. The identity in
use therefore needs container-create rights unless the container already exists.

### Save and overwrite semantics

`SaveAsync` validates against `BlobServiceOptions` and then passes `Overwrite` straight through to the SDK's
`UploadAsync`. Unlike S3 and Local, this provider raises no `InvalidOperationException` of its own — a duplicate upload
with `Overwrite = false` surfaces the SDK's `RequestFailedException` (`409 BlobAlreadyExists`) instead:

```csharp
var blob = new BlobDetails.BlobData("reports/2026/q1.pdf", BinaryData.FromString("..."))
{
    Overwrite = true
};
var location = await blobService.SaveAsync(blob, ct); // "reports/2026/q1.pdf"
```

### SAS-based public URLs

`GetPublicAccessUrl` builds a read-only `BlobSasBuilder` for the single blob, starting now and expiring after
`expiresFromNow` — default `TimeSpan.FromDays(1)`. If the underlying client cannot sign a SAS
(`CanGenerateSasUri == false`, i.e. it was built from a token credential rather than an account key), it throws
`NotSupportedException`:

```csharp
var url = await blobService.GetPublicAccessUrl(
    new BlobRequest("reports/monthly.pdf"),
    TimeSpan.FromMinutes(15),
    ct);
```

### Recursive folder delete

A `BlobRequest` whose name has no file extension is a directory request. `DeleteAsync` then walks the prefix
breadth-first, deleting nested blobs as it finds them and queuing nested prefixes, and finally removes the folder
markers from the deepest level up. Directory detection is by convention — an entry with no content type and no content
length is treated as a folder marker.

### Listing a prefix

`ListItemsAsync` streams `GetBlobsAsync` for the prefix (leading slash removed) and populates `Details` from each blob's
properties; folder markers come back with `Details` as `null`.

## ⚙️ Configuration reference

`AzureStorageOptions` extends [`BlobServiceOptions`](./DKNet.Svc.BlobStorage.Abstractions.md):

| Option | Type | Default | Effect |
|---|---|---|---|
| `ContainerName` | `string` | *(required)* | Target container; created on first use when missing. |
| `ConnectionString` | `string?` | `null` | Storage account connection string. Ignored when `BlobServiceClientFactory` is set. |
| `BlobServiceClientFactory` | `Func<AzureStorageOptions, Task<BlobServiceClient>>?` | `null` | Builds the `BlobServiceClient` yourself — the hook for managed identity or any custom client. Cannot be set from configuration binding. |
| `Name` (static) | `string` | `"BlobService:AzureStorage"` | Configuration section key `AddAzureStorageAdapter(IConfiguration)` binds from. |

The shared `IncludedExtensions`, `MaxFileNameLength`, and `MaxFileSizeInMb` checks apply unchanged.

## 🧱 Where it fits

- **[DKNet.Svc.BlobStorage.Abstractions](./DKNet.Svc.BlobStorage.Abstractions.md)** — `AzureStorageBlobService` derives
  from its `BlobService` base class; application code depends on `IBlobService`, and only the composition root
  references this package.
- **[DKNet.Svc.BlobStorage.Local](./DKNet.Svc.BlobStorage.Local.md)** — the usual stand-in for local runs and CI, where
  no storage account or emulator should be required.
- **`DKNet.EfCore.Events` / `DKNet.EfCore.Repos`** — pair with them exactly as any provider does; see the Abstractions
  page.

## ⚠️ Gotchas & limits

- **`GetAsync` throws on a missing blob; it does not return `null`.** Properties are fetched before the existence check,
  so the SDK's `RequestFailedException` (`404`) surfaces first and the method's `null` path is unreachable. Treat a
  `404` as "not found" here, the same way you treat `FileNotFoundException` on the
  [Local provider](./DKNet.Svc.BlobStorage.Local.md).
- **The `Action<AzureStorageOptions>` overload does not configure the service.** It registers the instance you populate
  as a singleton `AzureStorageOptions`, while `AzureStorageBlobService` consumes `IOptions<AzureStorageOptions>` — the
  values never reach it and the first operation throws `ArgumentException`. Use the `IConfiguration` overload, plus
  `services.Configure<AzureStorageOptions>(...)` for anything configuration cannot bind.
- **`NotSupportedException` from `GetPublicAccessUrl` is usually an auth-shape problem, not a bug.** A client built from
  a token credential (managed identity) cannot sign a SAS; an account-key client can. Check how the client was
  constructed first.
- **Listing entries reuse the requested name.** `ListItemsAsync` sets each result's `Name` to the request's name rather
  than the blob's own name — use `Details` for per-blob metadata and don't rely on the listed name to address a blob.
- **Folder detection is heuristic.** A real zero-length blob with no content type is indistinguishable from a folder
  marker, so it will be treated as a directory.
- **No blob-name-length or upload-size cap** beyond the shared, opt-in `BlobServiceOptions` checks.
- **Registrations do not evict each other.** Multiple `IBlobService` providers can coexist in one `IServiceCollection`;
  the last registration wins when a single `IBlobService` is resolved.

## 🔗 Related packages

- [DKNet.Svc.BlobStorage.Abstractions](./DKNet.Svc.BlobStorage.Abstractions.md) – the contract, models, and shared
  validation; the package application code should reference.
- [DKNet.Svc.BlobStorage.AwsS3](./DKNet.Svc.BlobStorage.AwsS3.md) – reach for it when blobs live in AWS S3 or an
  S3-compatible store instead.
- [DKNet.Svc.BlobStorage.Local](./DKNet.Svc.BlobStorage.Local.md) – reach for it for local development, tests, and CI.
