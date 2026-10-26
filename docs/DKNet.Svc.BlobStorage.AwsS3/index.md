# DKNet.Svc.BlobStorage.AwsS3

AWS S3 implementation of `IBlobService` for .NET developers, also compatible with S3-compatible services such as
MinIO and Cloudflare R2.

## ✨ Why use it?

- Register AWS S3 with one call — `AddS3BlobService(configuration)` binds credentials, endpoint, and bucket, and
  wires `IBlobService` without you managing an `AmazonS3Client` lifetime.
- Point the same code at MinIO, R2, or AWS itself — `ForcePathStyle` and an explicit endpoint cover self-hosted and
  compatible services.
- Skip the bucket-provisioning step in dev and CI — the configured bucket is created on first use if it is missing.
- Hand out time-limited share links without SDK code — `GetPublicAccessUrl` returns a pre-signed URL.

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

`AddS3BlobService(IServiceCollection, IConfiguration)` binds `S3Options` from the `"BlobService:S3"` section and
registers `IBlobService → S3BlobService` as a process-lifetime singleton; the call is idempotent (safe to call more
than once).

## 🏗️ Architecture

![Application code registers AddS3BlobService once, which registers S3BlobService as a singleton deriving from Abstractions.BlobService; S3BlobService reads S3Options and lazily builds a cached AmazonS3Client that talks to the configured S3 bucket.](diagrams/components.svg)

`S3BlobService` is `IDisposable` and caches its `AmazonS3Client`; the diagram cannot show that the client is built
lazily — on the first operation, not at registration time — which is covered in the next section.

## 🔄 How it works

![S3BlobService: how the AmazonS3Client is built on the first operation — resolving credentials, creating the bucket if missing, and caching the client for the rest of the process.](diagrams/svc-blobstorage-awss3-client-bootstrap.svg)

The client is not built at registration — it is built lazily on the first operation, and that first operation is
also what creates the bucket. A `Lazy<Task<AmazonS3Client>>` makes this race-safe: concurrent first callers all await
the same build instead of each paying their own `ListBuckets`/`PutBucket` round trip. The diagram does not show the
per-call `SaveAsync` path (validate, conflict check, upload) — see the feature page below.

## 🧩 Features

- [Configure and operate S3 storage](configure-and-operate-s3-storage.md) — Set credentials and endpoint options, and work with save conflicts, folder delete, listing, and pre-signed URLs.

## 🔗 Related packages

- [DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions/index.md) – reach for it for the `IBlobService` contract, shared models, and validation that application code should depend on directly.
- [DKNet.Svc.BlobStorage.AzureStorage](../DKNet.Svc.BlobStorage.AzureStorage/index.md) – reach for it when blobs live in an Azure Storage account instead.
- [DKNet.Svc.BlobStorage.Local](../DKNet.Svc.BlobStorage.Local/index.md) – reach for it for local development, tests, and CI, where no cloud account should be needed.
