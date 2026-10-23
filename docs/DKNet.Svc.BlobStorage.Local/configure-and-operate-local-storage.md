# Configure and operate local storage

Part of [DKNet.Svc.BlobStorage.Local](index.md). Set the root folder, then work with the save, listing, and delete
behavior specific to this provider.

```csharp
// appsettings.json
// { "BlobStorage": { "LocalFolder": { "RootFolder": "/var/app/storage" } } }

builder.Services.AddLocalDirectoryBlobService(builder.Configuration);
```

`RootFolder` is the base of every path this provider touches. Left unset, it falls back to
`{CurrentDirectory}/LocalStore` — fine for a quick local run, but set it explicitly for anything you deploy, because
the current directory is whatever the process happened to start in.

```csharp
var blob = new BlobDetails.BlobData("reports/2026/q1.pdf", BinaryData.FromString("..."))
{
    Overwrite = true // replace an existing file instead of throwing
};
var location = await blobService.SaveAsync(blob); // "reports/2026/q1.pdf"
```

`SaveAsync` validates against `BlobServiceOptions`, then throws `InvalidOperationException("File already existed")`
when the target exists and `Overwrite` is `false` (the shared default). Missing parent directories are created
automatically, and the write goes through a `FileStream` opened with `FileShare.None`, so two writers never
interleave on the same path. `GetAsync` throws `FileNotFoundException` when the file does not exist — every other
built-in provider returns `null` instead (see [Abstractions](../DKNet.Svc.BlobStorage.Abstractions/write-a-custom-blob-service-provider.md#-gotchas--limits)).

![ListItemsAsync resolves the path with GetFinalPath, checks whether it is a directory with Directory.Exists, and either walks every entry under it in one recursive EnumerateFileSystemInfos pass or yields the single matching file, interleaving files and folders in whatever order the file system returns them.](diagrams/configure-and-operate-local-storage.svg)

The diagram does not show that a non-existent, non-directory path yields an empty sequence rather than throwing — the
same "not found ⇒ nothing" shape S3 and Azure Storage use for listing. Names come back relative to `RootFolder`,
computed with `Path.GetRelativePath` — correct even when a subfolder shares the root folder's own name.

## ⚙️ Configuration reference

`LocalDirectoryOptions` extends `BlobServiceOptions`, documented on the
[Abstractions](../DKNet.Svc.BlobStorage.Abstractions/write-a-custom-blob-service-provider.md) feature page:

| Option | Type | Default | Effect |
|---|---|---|---|
| `RootFolder` | `string?` | `null` → `{CurrentDirectory}/LocalStore` at runtime | Base directory for every blob; also the boundary the traversal guard enforces. |
| `LocalDirectoryOptions.Name` (static) | `string` | `"BlobStorage:LocalFolder"` | Configuration section key `AddLocalDirectoryBlobService` binds from — note the `BlobStorage:` prefix, unlike the cloud providers' `BlobService:`. |

The package also ships one public helper outside this options type: `LocalDirectorySetup.IsDirectory(this string
path)`, declared in the `Microsoft.Extensions.DependencyInjection` namespace so it is in scope wherever the
registration call is. It is a thin wrapper over `Directory.Exists(path)` — it returns `false` for a missing or
non-directory path and never throws.

## 🧱 Where it fits

`LocalBlobService` derives from the `BlobService` base class in
[Abstractions](../DKNet.Svc.BlobStorage.Abstractions/index.md), so validation and path normalization behave the same
as on the cloud providers. Application code depends on `IBlobService` only; swap this registration for
[S3](../DKNet.Svc.BlobStorage.AwsS3/index.md) or [Azure](../DKNet.Svc.BlobStorage.AzureStorage/index.md) per
environment without touching a consumer. Test projects register this provider against a temporary directory to
exercise blob-writing code paths without a storage emulator.

## ⚠️ Gotchas & limits

- **`GetAsync`'s `FileNotFoundException` is the biggest provider-agnostic trap.** S3 and Azure Storage both return
  `null` for the same miss — see the
  [Abstractions feature page](../DKNet.Svc.BlobStorage.Abstractions/write-a-custom-blob-service-provider.md#-gotchas--limits).
- **No public-URL support at all.** `GetPublicAccessUrl` always throws `NotSupportedException` — plan for it if
  blob-consuming code assumes every provider hands back a shareable link.
- **`RootFolder`'s default depends on the process working directory**, which differs between `dotnet run`, a
  published binary, and a container — always set it explicitly outside local dev.
- **No blob-name-length or upload-size cap** beyond the shared, opt-in `BlobServiceOptions` checks.
- **Deleting a directory is recursive and unconditional** (`Directory.Delete(path, true)`) — a `BlobRequest` whose
  name has no file extension is treated as a directory, so a missing extension on a delete can remove a subtree.
- **Listing and directory-walk order changed from file-then-directory to interleaved.** A single
  `EnumerateFileSystemInfos` pass replaced two separate passes; no consumer should depend on files being listed
  before folders.
- **Not safe as shared storage for multiple instances.** There is no locking or coordination beyond
  `FileShare.None` on a single write; two processes writing the same path race on the filesystem.
