# DKNet.Svc.BlobStorage.AzureStorage

Azure Blob Storage implementation of `IBlobService` for .NET developers, backed by `Azure.Storage.Blobs`.

## ✨ Why use it?

- Authenticate with a connection string or your own managed-identity client — `BlobServiceClientFactory` is the hook
  for `DefaultAzureCredential`, with no separate credential plumbing in your own code.
- Skip the container-provisioning step — `CreateIfNotExistsAsync` runs on first use, so a fresh environment needs no
  manual container creation.
- Hand out read-only share links out of the box — `GetPublicAccessUrl` returns a SAS URI scoped to a single blob.
- Get folder semantics over Azure's flat namespace — `DeleteAsync` on a directory request walks the prefix tree and
  removes nested blobs and folder markers for you.

## 🚀 Quick Start

```bash
dotnet new console -f net10.0 -n AzureBlobDemo
cd AzureBlobDemo
dotnet add package DKNet.Svc.BlobStorage.AzureStorage
dotnet add package Microsoft.Extensions.Logging
```

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BlobService:AzureStorage:ConnectionString"] = "UseDevelopmentStorage=true",
        ["BlobService:AzureStorage:ContainerName"] = "documents"
    })
    .Build();

var services = new ServiceCollection().AddLogging().AddAzureStorageAdapter(config);
using var provider = services.BuildServiceProvider();
var blobService = provider.GetRequiredService<IBlobService>();

var location = await blobService.SaveAsync(
    new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromString("...")) { Overwrite = true });
Console.WriteLine(location);
```

`AddAzureStorageAdapter(IConfiguration)` binds `AzureStorageOptions` from the `"BlobService:AzureStorage"` section
and registers `IBlobService → AzureStorageBlobService` as a process-lifetime singleton; the call is idempotent. A
second `AddAzureStorageAdapter(Action<AzureStorageOptions>)` overload exists — read its trap on the
[configure page](configure-azure-blob-client.md#-gotchas--limits) before reaching for it.

## 🏗️ Architecture

![Application code registers AddAzureStorageAdapter once, which registers AzureStorageBlobService as a singleton deriving from Abstractions.BlobService; AzureStorageBlobService reads AzureStorageOptions and lazily builds a cached BlobContainerClient that talks to the configured Azure Blob container.](diagrams/components.svg)

The diagram cannot show that `BlobServiceClientFactory`, when set, takes priority over `ConnectionString` for
building the underlying client — covered on the configure page.

## 🔄 How it works

![AzureStorageBlobService: client resolution picks BlobServiceClientFactory when set, otherwise ConnectionString, throws ArgumentException when neither is set, creates the container when missing, then runs uploads, listings and deletes, with SAS URL signing as a separate branch.](diagrams/svc-blobstorage-azure-client-resolution.svg)

Which of the two option properties you set decides how the `BlobServiceClient` is built, and that in turn decides
whether `GetPublicAccessUrl` can sign anything. The diagram does not show the per-call `SaveAsync` path or SAS
signing in detail — see the feature page below.

## 🧩 Features

- [Configure the Azure blob client](configure-azure-blob-client.md) — Choose connection-string or managed-identity auth, and work with save, SAS URLs, and folder delete.

## 🔗 Related packages

- [DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions/index.md) – reach for it for the `IBlobService` contract, shared models, and validation that application code should depend on directly.
- [DKNet.Svc.BlobStorage.AwsS3](../DKNet.Svc.BlobStorage.AwsS3/index.md) – reach for it when blobs live in AWS S3 or an S3-compatible store instead.
- [DKNet.Svc.BlobStorage.Local](../DKNet.Svc.BlobStorage.Local/index.md) – reach for it for local development, tests, and CI.
