# DKNet.Svc.BlobStorage.Local

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.Local)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Local/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.Local)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Local/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

Local-filesystem implementation of `IBlobService` from
[DKNet.Svc.BlobStorage.Abstractions](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Abstractions/) — for
local development, tests, and single-instance deployments.

## Features

- `LocalBlobService` — full `IBlobService` implementation over a configured root folder
- Path-traversal protection (`UnauthorizedAccessException` on escape attempts)
- Creates missing parent directories on save
- No external service required — runs anywhere, including CI

## Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.Local
```

## Quick Start

```jsonc
// appsettings.json
{
  "BlobStorage": {
    "LocalFolder": {
      "RootFolder": "/var/app/storage"
    }
  }
}
```

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

builder.Services.AddLocalDirectoryBlobService(builder.Configuration); // binds "BlobStorage:LocalFolder"

public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)), ct);
}
```

`AddLocalDirectoryBlobService(IServiceCollection, IConfiguration)` is the only registration entry point. It
binds `LocalDirectoryOptions` from the section named by `LocalDirectoryOptions.Name` and registers
`IBlobService → LocalBlobService` as `Scoped`; the call is idempotent.

## Configuration — `LocalDirectoryOptions`

`LocalDirectoryOptions` extends `BlobServiceOptions`, so `IncludedExtensions`, `MaxFileNameLength`, and
`MaxFileSizeInMb` apply here unchanged (see the Abstractions package).

| Option | Type | Default | Effect |
|---|---|---|---|
| `RootFolder` | `string?` | `null` → `{CurrentDirectory}/LocalStore` | Base folder for every path this provider touches. Any request resolving outside it throws `UnauthorizedAccessException`. |
| `LocalDirectoryOptions.Name` (static) | `string` | `"BlobStorage:LocalFolder"` | The configuration section `AddLocalDirectoryBlobService` binds from — note the `BlobStorage:` prefix, unlike the cloud providers' `BlobService:`. |

Per-call settings live on the request: `BlobDetails.BlobData.Overwrite` (default `false`; saving over an
existing file without it throws `InvalidOperationException`).

`GetPublicAccessUrl` always throws `NotSupportedException` on this provider, and `GetAsync` throws
`FileNotFoundException` rather than returning `null` for a missing file.

The package also ships one public helper, `LocalDirectorySetup.IsDirectory(this string path)` — declared in the
`Microsoft.Extensions.DependencyInjection` namespace, so it is in scope wherever the registration is. It returns
`true` when the path is a directory, but it only catches `DirectoryNotFoundException` and `ArgumentException`:
a path whose parent exists but whose leaf does not throws `FileNotFoundException` rather than returning `false`.
`ListItemsAsync` routes through it, so listing a path that does not exist throws too.

## Documentation

Full feature reference, diagrams, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/Services/DKNet.Svc.BlobStorage.Local.md
