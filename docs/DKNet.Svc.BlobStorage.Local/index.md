# DKNet.Svc.BlobStorage.Local

Local-filesystem implementation of `IBlobService` for .NET developers, storing blobs under a configured root folder
with path-traversal protection.

## ✨ Why use it?

- Run a real `IBlobService` in local development, unit tests, and CI with nothing to provision and nothing to clean
  up but a folder.
- Keep production code identical — the same `IBlobService` consumers that run against S3 or Azure in production run
  against a directory here; only the registration line differs.
- Block escape attempts instead of trusting input — every request path is resolved and checked against the
  configured root, so a caller-supplied `../` cannot reach outside it.
- Ship a single-instance deployment on a mounted volume without taking a storage-SDK dependency.

## 🚀 Quick Start

```bash
dotnet new console -f net10.0 -n LocalBlobDemo
cd LocalBlobDemo
dotnet add package DKNet.Svc.BlobStorage.Local
dotnet add package Microsoft.Extensions.Logging
```

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BlobStorage:LocalFolder:RootFolder"] = Path.Combine(Path.GetTempPath(), "blob-demo")
    })
    .Build();

var services = new ServiceCollection().AddLogging().AddLocalDirectoryBlobService(config);
using var provider = services.BuildServiceProvider();
var blobService = provider.GetRequiredService<IBlobService>();

var location = await blobService.SaveAsync(
    new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromString("...")) { Overwrite = true });
Console.WriteLine(location); // reports/monthly.pdf
```

Run with `dotnet run`. `AddLocalDirectoryBlobService(IServiceCollection, IConfiguration)` binds
`LocalDirectoryOptions` from `"BlobStorage:LocalFolder"` and registers `IBlobService → LocalBlobService` as
`Scoped`; the call is idempotent.

## 🏗️ Architecture

![Application code registers AddLocalDirectoryBlobService, which registers LocalBlobService as scoped, deriving from Abstractions.BlobService; LocalBlobService reads LocalDirectoryOptions and resolves every call to a path under RootFolder on the local file system.](diagrams/components.svg)

There is no SDK client to build or cache here, unlike the cloud providers — every operation resolves a path under
`RootFolder` and touches the file system directly through `System.IO`.

## 🔄 How it works

![GetFinalPath drops a leading slash from the blob name, combines it with RootFolder and resolves it with Path.GetFullPath, then compares the result against the root prefix — ordinal on Linux, case-insensitive on Windows — throwing UnauthorizedAccessException when it escapes and otherwise reading or writing the file and returning the root-relative name.](diagrams/svc-blobstorage-local-path-resolution.svg)

Every operation resolves the requested name to an absolute path and re-checks it against the root before touching
the file system. The diagram does not show `ListItemsAsync`'s directory walk or `SaveAsync`'s overwrite/directory-
creation behavior — see the feature page below.

## 🧩 Features

- [Configure and operate local storage](configure-and-operate-local-storage.md) — Set the root folder, and work with save conflicts, directory listing, and recursive delete.

## 🔗 Related packages

- [DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions/index.md) – reach for it for the `IBlobService` contract, shared models, and validation that application code should depend on directly.
- [DKNet.Svc.BlobStorage.AwsS3](../DKNet.Svc.BlobStorage.AwsS3/index.md) – reach for it when the deployed environment stores blobs in S3 or an S3-compatible service.
- [DKNet.Svc.BlobStorage.AzureStorage](../DKNet.Svc.BlobStorage.AzureStorage/index.md) – reach for it when the deployed environment stores blobs in an Azure Storage account.
