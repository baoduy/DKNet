# DKNet.Svc.BlobStorage.Abstractions

Provider-agnostic blob storage contract for .NET developers — application code depends on `IBlobService` and the shared models, and a provider package supplies the implementation.

## ✨ Why use it?

- Swap storage backends with a single DI registration change — business logic only ever sees `IBlobService`.
- Move every payload through the same `BinaryData`-based API — no separate `byte[]` overload to choose.
- Get save-time guard rails (extension allow-list, name length, size limit) for free, shared by every provider.
- Stream a large upload or download without buffering the whole payload into memory.

## 🚀 Quick Start

Use a .NET 10 console project. Install this package together with a provider package (Local shown here).

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

var location = await blobService.SaveAsync(
    new BlobDetails.BlobData("reports/hello.txt", BinaryData.FromString("Hello blob storage")));
Console.WriteLine(location);
```

Run with `dotnet run`.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.BlobStorage.Abstractions/index.md) | Start with architecture and the end-to-end flow. |
| [Write a custom IBlobService provider](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.BlobStorage.Abstractions/write-a-custom-blob-service-provider.md) | Implement the abstract base class and reuse the shared validation and path rules. |
