# DKNet.Svc.BlobStorage.Local

Local-filesystem implementation of [`IBlobService`](./DKNet.Svc.BlobStorage.Abstractions.md) — `LocalBlobService`
stores blobs under a configured root folder with path-traversal protection. For the operations, models, and
validation rules every provider shares, read the Abstractions page first — this page covers only what's specific to
this provider.

## When to reach for it

Use this provider for local development, tests, and single-instance deployments that don't need cloud storage. It's
also the simplest provider to run in CI, since it needs no external service.

## Install and minimal wiring

```bash
dotnet add package DKNet.Svc.BlobStorage.Local
```

```csharp
// appsettings.json
// { "BlobStorage": { "LocalFolder": { "RootFolder": "/var/app/storage" } } }

builder.Services.AddLocalDirectoryBlobService(builder.Configuration);
```

```csharp
public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveReportAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)), ct);
}
```

`AddLocalDirectoryBlobService(IServiceCollection, IConfiguration)` binds `LocalDirectoryOptions` from
`"BlobStorage:LocalFolder"` and registers `IBlobService → LocalBlobService` as `Scoped`; the call is idempotent.

## Provider-specific behavior

- **Root folder defaults to `{CurrentDirectory}/LocalStore`** when `RootFolder` is left unset — fine for a quick
  local run, but set it explicitly for anything you deploy.
- **Path-traversal guard.** Every path is resolved to a full path and checked against the configured root; a path
  that would escape the root (e.g. `../../etc/passwd`) throws `UnauthorizedAccessException`. Comparison is
  case-insensitive on Windows, ordinal elsewhere.
- **`GetAsync` throws `FileNotFoundException` on a miss** — the only provider that does; S3 and Azure return `null`.
  Code that needs to run against any provider should handle both cases.
- **`GetPublicAccessUrl` always throws `NotSupportedException`.** There is no concept of a public URL for a local
  path — don't call it against this provider.
- **`SaveAsync` throws `InvalidOperationException("File already existed")`** when the target exists and `Overwrite`
  is `false`; missing directories are created automatically.
- **`ListItemsAsync` over a directory** yields files first (recursive), then bare directory entries (recursive, no
  `Details`) — an entry for a single file path yields just that one file.

## Configuration — `LocalDirectoryOptions` (extends `BlobServiceOptions`)

| Property | Default | Notes |
|---|---|---|
| `string? RootFolder` | `null` → falls back to `{CurrentDirectory}/LocalStore` at runtime | Set explicitly outside local dev. |
| `static string Name` | `"BlobStorage:LocalFolder"` | Configuration section key used by `AddLocalDirectoryBlobService`. |

Plus the shared `IncludedExtensions`/`MaxFileNameLength`/`MaxFileSizeInMb` from
[`BlobServiceOptions`](./DKNet.Svc.BlobStorage.Abstractions.md#configuration--blobserviceoptions).

## Composing with other DKNet packages

Depend on `IBlobService` from `DKNet.Svc.BlobStorage.Abstractions` in application code; only the composition root
needs `AddLocalDirectoryBlobService`. Useful as the "swap-in" provider for local/dev environments that otherwise run
S3 or Azure in production — the calling code never changes.

## Gotchas and limits

- No blob-name-length or upload-size cap beyond the shared, opt-in `BlobServiceOptions` checks.
- No public-URL support at all — plan for that if your application's blob-consuming code assumes every provider can
  hand back a shareable link.
- The `FileNotFoundException`-vs-`null` mismatch with S3/Azure on `GetAsync` is the single biggest behavioral trap
  when writing provider-agnostic code — see the Abstractions page's Gotchas section.
