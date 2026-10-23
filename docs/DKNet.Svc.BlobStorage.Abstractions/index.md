# DKNet.Svc.BlobStorage.Abstractions

Provider-agnostic blob storage contract for .NET developers — application code depends on `IBlobService` and the
shared models, and a provider package (AWS S3, Azure Storage, or the local filesystem) supplies the implementation.

## ✨ Why use it?

- Swap storage backends with a single DI registration change — business logic only ever sees `IBlobService`.
- Move every payload, from a 2 KB text file to a multi-gigabyte stream, through the same `BinaryData`-based API —
  no separate `byte[]` overload to choose.
- Get save-time guard rails (extension allow-list, name length, size limit) for free — they run once, in the shared
  base class every provider derives from, instead of being re-implemented per backend.
- Stream a large upload or download without buffering the whole payload into memory, through the `BlobStreamData`
  overloads and `OpenReadAsync`.

## 🚀 Quick Start

Use a .NET 10 console project. This package has no `IBlobService` implementation of its own, so install it together
with the [Local](../DKNet.Svc.BlobStorage.Local/index.md) provider to run the example.

```bash
dotnet new console -f net10.0 -n BlobDemo
cd BlobDemo
dotnet add package DKNet.Svc.BlobStorage.Abstractions
dotnet add package DKNet.Svc.BlobStorage.Local
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

var blob = new BlobDetails.BlobData("reports/hello.txt", BinaryData.FromString("Hello blob storage"));
var location = await blobService.SaveAsync(blob);
Console.WriteLine(location); // reports/hello.txt
```

Run with `dotnet run`.

## 🏗️ Architecture

![Application code calls IBlobService, implemented by the BlobService abstract base class, which reads BlobServiceOptions and accepts or returns BlobRequest and BlobDetails models; a provider package (S3, Azure, or Local) derives from BlobService to supply the actual backend.](diagrams/components.svg)

This package ships no `IBlobService` implementation — only the contract, the shared `BlobService` base class, the
request/result models, and `BlobServiceOptions`. The diagram cannot show that `BlobService.ValidateFile` and
`GetBlobLocation` are `protected virtual`: a provider can call them as-is or override them to change the rules.

## 🔄 How it works

![SaveAsync runs ValidateFile and GetBlobLocation in the shared BlobService base class, throws FileLoadException when a BlobServiceOptions rule is violated, and otherwise hands the normalized name to the provider override, which writes to its backing store and returns the location.](diagrams/svc-blobstorage-abstractions-save-path.svg)

Every `SaveAsync` call runs the same two shared steps before a provider ever touches its backing store, and only the
last step differs per backend. The diagram does not show the streaming path: `SaveAsync(BlobData)` is itself
implemented by converting to a `Stream` and delegating to `SaveAsync(BlobStreamData)`, so the same validation and
location logic applies to both.

## 🧩 Features

- [Write a custom IBlobService provider](write-a-custom-blob-service-provider.md) — Implement the abstract base class and reuse the shared validation and path rules.

## 🔗 Related packages

- [DKNet.Svc.BlobStorage.AwsS3](../DKNet.Svc.BlobStorage.AwsS3/index.md) — reach for it when blobs live in AWS S3 or an S3-compatible store (MinIO, Cloudflare R2).
- [DKNet.Svc.BlobStorage.AzureStorage](../DKNet.Svc.BlobStorage.AzureStorage/index.md) — reach for it when blobs live in an Azure Storage account, especially with managed-identity auth.
- [DKNet.Svc.BlobStorage.Local](../DKNet.Svc.BlobStorage.Local/index.md) — reach for it in local development, tests, and CI where no cloud account should be needed.
- [DKNet.Svc.Encryption](../DKNet.Svc.Encryption/index.md) — reach for it when a blob's bytes must be encrypted before they reach the store.
