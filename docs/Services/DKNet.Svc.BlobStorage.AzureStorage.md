# DKNet.Svc.BlobStorage.AzureStorage

Azure Blob Storage implementation of [`IBlobService`](./DKNet.Svc.BlobStorage.Abstractions.md), backed by
`Azure.Storage.Blobs`. For the operations, models, and validation rules every provider shares, read the Abstractions
page first — this page covers only what's specific to Azure.

## When to reach for it

Use this provider when your blobs live in an Azure Storage account, including scenarios that need managed
identity/Azure AD auth instead of a connection string.

## Install and minimal wiring

```bash
dotnet add package DKNet.Svc.BlobStorage.AzureStorage
```

Configuration-bound registration:

```csharp
// appsettings.json
// { "BlobService": { "AzureStorage": { "ConnectionString": "...", "ContainerName": "documents" } } }

builder.Services.AddAzureStorageAdapter(builder.Configuration);
```

Or code-first (needed for managed identity — see below):

```csharp
builder.Services.AddAzureStorageAdapter(options =>
{
    options.ContainerName = "documents";
    options.BlobServiceClientFactory = _ =>
        Task.FromResult(new BlobServiceClient(new Uri("https://myaccount.blob.core.windows.net"), new DefaultAzureCredential()));
});
```

```csharp
public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveReportAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)), ct);
}
```

Both `AddAzureStorageAdapter` overloads register `IBlobService → AzureStorageBlobService` as `Scoped`; both are
idempotent.

## Provider-specific behavior

- **Managed identity / custom auth via `BlobServiceClientFactory`.** Set `BlobServiceClientFactory` to build the
  `BlobServiceClient` yourself (e.g. with `DefaultAzureCredential`) instead of a connection string — this is the real
  hook for Azure AD auth; there is no separate "UseAzureAD" flag. When set, it takes priority over `ConnectionString`.
  Setting neither throws `ArgumentException` when the client is first needed.
- **Container auto-creation.** The container is created on first use (`CreateIfNotExistsAsync`).
- **Folder delete traverses breadth-first**, deleting nested blobs before removing folder markers.
- **Public URLs are SAS tokens**, read-only, default lifetime `TimeSpan.FromDays(1)` — there is no "CDN endpoint"
  option; `GetPublicAccessUrl` throws `NotSupportedException` if the underlying client reports
  `CanGenerateSasUri == false` (i.e., the client wasn't created with credentials capable of signing a SAS).
- **Overwrite semantics come from the Azure SDK itself** — `SaveAsync` passes `Overwrite` straight through to
  `UploadAsync`; unlike S3/Local, this provider does not raise its own `InvalidOperationException` first.

## Configuration — `AzureStorageOptions` (extends `BlobServiceOptions`)

| Property | Default | Notes |
|---|---|---|
| `string ContainerName` | *(required)* | |
| `string? ConnectionString` | `null` | Ignored when `BlobServiceClientFactory` is set. |
| `Func<AzureStorageOptions, Task<BlobServiceClient>>? BlobServiceClientFactory` | `null` | Use for managed identity/Azure AD or any custom `BlobServiceClient` construction. |
| `static string Name` | `"BlobService:AzureStorage"` | Configuration section key used by the `IConfiguration` overload. |

Plus the shared `IncludedExtensions`/`MaxFileNameLength`/`MaxFileSizeInMb` from
[`BlobServiceOptions`](./DKNet.Svc.BlobStorage.Abstractions.md#configuration--blobserviceoptions).

## Composing with other DKNet packages

Depend on `IBlobService` from `DKNet.Svc.BlobStorage.Abstractions` in application code; only the composition root
needs `AddAzureStorageAdapter`. Pairs with `DKNet.EfCore.Events`/`DKNet.EfCore.Repos` the same way every provider
does — see the Abstractions page.

## Gotchas and limits

- No blob-name-length or upload-size cap beyond the shared, opt-in `BlobServiceOptions` checks.
- `GetPublicAccessUrl` throwing `NotSupportedException` almost always means the `BlobServiceClient` in use can't sign
  SAS tokens — check how it was constructed (account key vs. Azure AD token credential) before assuming a bug.
- Multiple `IBlobService` providers can coexist in the same `IServiceCollection`; this registration does not remove
  any other provider's.
