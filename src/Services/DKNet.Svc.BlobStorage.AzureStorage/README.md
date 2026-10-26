# DKNet.Svc.BlobStorage.AzureStorage

Azure Blob Storage implementation of `IBlobService` for .NET developers, backed by `Azure.Storage.Blobs`.

## ✨ Why use it?

- Authenticate with a connection string or your own managed-identity client — no separate credential plumbing.
- Skip the container-provisioning step — the configured container is created on first use if missing.
- Hand out read-only share links out of the box via SAS URIs scoped to a single blob.
- Get folder semantics over Azure's flat namespace — deleting a folder walks the prefix tree for you.

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

Run with `dotnet run`.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.BlobStorage.AzureStorage/index.md) | Start with architecture and the end-to-end flow. |
| [Configure the Azure blob client](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.BlobStorage.AzureStorage/configure-azure-blob-client.md) | Choose connection-string or managed-identity auth, and work with save, SAS URLs, and folder delete. |
