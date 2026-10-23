# Configure the Azure blob client

Part of [DKNet.Svc.BlobStorage.AzureStorage](index.md). Pick how `BlobServiceClient` gets built, then work with the
save, SAS, and delete behavior specific to Azure Blob Storage.

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

`BlobServiceClientFactory` is the hook for Azure AD / managed-identity auth — there is no separate "use Azure AD"
flag. When it is set it takes priority over `ConnectionString`; when neither is set, the first blob operation throws
`ArgumentException`. The factory receives the resolved `AzureStorageOptions`, so it can read your own configuration
values off it, and it runs once per service instance — the resulting container client is cached.

![SaveAsync validates the stream and calls UploadAsync with Overwrite passed straight through, surfacing RequestFailedException (409) on an unflagged conflict; GetPublicAccessUrl checks CanGenerateSasUri before building a read-only SAS, throwing NotSupportedException for a client that cannot sign one.](diagrams/configure-azure-blob-client.svg)

```csharp
var blob = new BlobDetails.BlobData("reports/2026/q1.pdf", BinaryData.FromString("...")) { Overwrite = true };
var location = await blobService.SaveAsync(blob); // "reports/2026/q1.pdf"

var url = await blobService.GetPublicAccessUrl(
    new BlobRequest("reports/monthly.pdf"), TimeSpan.FromMinutes(15));
```

Unlike S3 and Local, this provider raises no exception of its own on a save conflict — `SaveAsync` passes `Overwrite`
straight to the SDK's `UploadAsync`, so a duplicate upload with `Overwrite = false` surfaces the SDK's
`RequestFailedException` (`409 BlobAlreadyExists`) instead. `GetPublicAccessUrl` builds a read-only SAS valid for
`expiresFromNow`, defaulting to one day, and throws `NotSupportedException` when `CanGenerateSasUri` is `false` (a
client built from a token credential rather than an account key). The diagram does not show folder delete: it walks
the prefix breadth-first with a queue, deleting nested blobs as it finds them, then removes the folder markers from
the deepest level up; a delete targeting the container root throws `ArgumentException` instead of deleting
everything.

## ⚙️ Configuration reference

`AzureStorageOptions` extends `BlobServiceOptions`, documented on the
[Abstractions](../DKNet.Svc.BlobStorage.Abstractions/write-a-custom-blob-service-provider.md) feature page:

| Option | Type | Default | Effect |
|---|---|---|---|
| `ContainerName` | `string` | *(required)* | Target container; created on first use when missing. |
| `ConnectionString` | `string?` | `null` | Storage account connection string. Used only when `BlobServiceClientFactory` is unset. |
| `BlobServiceClientFactory` | `Func<AzureStorageOptions, Task<BlobServiceClient>>?` | `null` | Builds the `BlobServiceClient` yourself — the hook for managed identity or any custom client. Takes priority over `ConnectionString`; cannot be set from configuration binding. |
| `AzureStorageOptions.Name` (static) | `string` | `"BlobService:AzureStorage"` | Configuration section key the `IConfiguration` overload binds from. |

## 🧱 Where it fits

`AzureStorageBlobService` derives from the `BlobService` base class in
[Abstractions](../DKNet.Svc.BlobStorage.Abstractions/index.md); application code depends on `IBlobService`, and only
the composition root references this package. Pair it with
[DKNet.Svc.BlobStorage.Local](../DKNet.Svc.BlobStorage.Local/index.md) for local runs and CI where no storage
account or emulator should be required.

## ⚠️ Gotchas & limits

- **`GetAsync` returns `null` for a missing blob — it does not throw.** `DownloadContentAsync`'s
  `RequestFailedException` (`404`) is caught internally and converted to `null`, the same shape as the
  [AwsS3](../DKNet.Svc.BlobStorage.AwsS3/index.md) provider. Only the
  [Local provider](../DKNet.Svc.BlobStorage.Local/index.md) throws (`FileNotFoundException`) instead.
- **The `Action<AzureStorageOptions>` overload does not configure the service.** It registers the instance you
  populate as a plain singleton `AzureStorageOptions`, while `AzureStorageBlobService`'s constructor takes
  `IOptions<AzureStorageOptions>` — a different service type, so the values never reach it and the first operation
  throws `ArgumentException`. Use the `IConfiguration` overload, plus `services.Configure<AzureStorageOptions>(...)`
  for anything configuration cannot bind, such as `BlobServiceClientFactory`.
- **`NotSupportedException` from `GetPublicAccessUrl` is usually an auth-shape problem, not a bug.** A client built
  from a token credential (managed identity) cannot sign a SAS; an account-key client can. Check how the client was
  constructed first.
- **Folder detection is heuristic.** A blob is treated as a directory marker when its `ContentLength` is `0` and its
  `ContentType` is empty — a real zero-length blob with no content type is indistinguishable from a folder marker.
- **No blob-name-length or upload-size cap** beyond the shared, opt-in `BlobServiceOptions` checks.
- **Registrations do not evict each other.** Multiple `IBlobService` providers can coexist in one
  `IServiceCollection`; the last registration wins when a single `IBlobService` is resolved.
