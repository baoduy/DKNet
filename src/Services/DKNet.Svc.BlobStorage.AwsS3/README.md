# DKNet.Svc.BlobStorage.AwsS3

AWS S3 implementation of `IBlobService` for .NET developers, also compatible with S3-compatible services such as MinIO and Cloudflare R2.

## ✨ Why use it?

- Register AWS S3 with one call — binds credentials, endpoint, and bucket, and wires `IBlobService` for you.
- Point the same code at MinIO, R2, or AWS itself with `ForcePathStyle` and an explicit endpoint.
- Skip the bucket-provisioning step — the configured bucket is created on first use if missing.
- Hand out time-limited share links without SDK code via pre-signed URLs.

## 🚀 Quick Start

```bash
dotnet new console -f net10.0 -n S3Demo
cd S3Demo
dotnet add package DKNet.Svc.BlobStorage.AwsS3
dotnet add package Microsoft.Extensions.Logging
```

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AwsS3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BlobService:S3:ConnectionString"] = "https://s3.ap-southeast-1.amazonaws.com",
        ["BlobService:S3:BucketName"] = "app-documents"
    })
    .Build();

var services = new ServiceCollection().AddLogging().AddS3BlobService(config);
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
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.BlobStorage.AwsS3/index.md) | Start with architecture and the end-to-end flow. |
| [Configure and operate S3 storage](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.BlobStorage.AwsS3/configure-and-operate-s3-storage.md) | Set credentials and endpoint options, and work with save conflicts, folder delete, listing, and pre-signed URLs. |
