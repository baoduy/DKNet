# DKNet.Svc.BlobStorage.Local

Local-filesystem implementation of `IBlobService` for .NET developers, storing blobs under a configured root folder with path-traversal protection.

## ✨ Why use it?

- Run a real `IBlobService` in local development, unit tests, and CI with nothing to provision and nothing to clean up but a folder.
- Keep production code identical — only the registration line changes when swapping in S3 or Azure.
- Block escape attempts instead of trusting input — a caller-supplied `../` cannot reach outside the root folder.
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
Console.WriteLine(location);
```

Run with `dotnet run`.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.BlobStorage.Local/index.md) | Start with architecture and the end-to-end flow. |
| [Configure and operate local storage](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.BlobStorage.Local/configure-and-operate-local-storage.md) | Set the root folder, and work with save conflicts, directory listing, and recursive delete. |
