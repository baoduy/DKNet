# DKNet.Svc.BlobStorage.AwsS3

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.AwsS3)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AwsS3/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.AwsS3)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AwsS3/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

AWS S3 (and S3-compatible services such as MinIO or Cloudflare R2) implementation of `IBlobService` from
[DKNet.Svc.BlobStorage.Abstractions](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Abstractions/).

## Features

- `S3BlobService` — full `IBlobService` implementation over the AWS SDK
- Creates the configured bucket on first use if it does not exist
- Pre-signed public URLs (`GetPublicAccessUrl`), default 1-hour expiry
- `ForcePathStyle`/`DisablePayloadSigning` options for MinIO and other S3-compatible endpoints

## Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.AwsS3
```

## Quick Start

```jsonc
// appsettings.json
{
  "BlobService": {
    "S3": {
      "ConnectionString": "https://s3.ap-southeast-1.amazonaws.com",
      "BucketName": "app-documents"
    }
  }
}
```

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AwsS3;

builder.Services.AddS3BlobService(builder.Configuration); // binds "BlobService:S3"

public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)), ct);
}
```

`AddS3BlobService(IServiceCollection, IConfiguration)` is the only registration entry point. It binds
`S3Options` from the section named by `S3Options.Name` and registers `IBlobService → S3BlobService` as
`Scoped`; the call is idempotent.

## Configuration — `S3Options`

`S3Options` extends `BlobServiceOptions`, so `IncludedExtensions`, `MaxFileNameLength`, and `MaxFileSizeInMb`
apply here unchanged (see the Abstractions package).

| Option | Type | Default | Effect |
|---|---|---|---|
| `ConnectionString` | `string` | *(required)* | S3 service endpoint URL, used as the SDK's `ServiceURL`. A value that does not start with `https` switches the client to plain HTTP. |
| `BucketName` | `string` | *(required)* | Target bucket. Created on first use when missing. |
| `AccessKey` | `string?` | `null` | With `Secret`, selects `BasicAWSCredentials`; leave both unset to use the ambient AWS credential chain. |
| `Secret` | `string?` | `null` | Paired with `AccessKey`. |
| `RegionEndpointName` | `string?` | `"us-east-1"` | Declared but never applied to the client — encode the region in `ConnectionString`. |
| `ForcePathStyle` | `bool` | `false` | `true` uses `endpoint/bucket/key` addressing; required by MinIO and most S3-compatible services. |
| `DisablePayloadSigning` | `bool` | `false` | Passed to `PutObject`; needed by some S3-compatible endpoints. |
| `S3Options.Name` (static) | `string` | `"BlobService:S3"` | The configuration section `AddS3BlobService` binds from. |

Per-call settings live on the request: `BlobDetails.BlobData.Overwrite` (default `false`) and
`GetPublicAccessUrl(..., expiresFromNow)` (default `TimeSpan.FromHours(1)`).

## Documentation

Full feature reference, diagrams, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/Services/DKNet.Svc.BlobStorage.AwsS3.md
