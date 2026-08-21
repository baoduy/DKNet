# DKNet.Svc.BlobStorage.AwsS3

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.AwsS3)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AwsS3/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.AwsS3)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AwsS3/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../../LICENSE)

AWS S3 (and S3-compatible services such as MinIO or Cloudflare R2) implementation of `IBlobService` from
[DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions).

## Features

- `S3BlobService` — full `IBlobService` implementation over the AWS SDK
- Auto-creates the configured bucket if it doesn't exist
- Pre-signed public URLs (`GetPublicAccessUrl`), default 1-hour expiry
- `ForcePathStyle`/`DisablePayloadSigning` options for MinIO and other S3-compatible endpoints

## Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.AwsS3
```

## Quick Start

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

builder.Services.AddS3BlobService(builder.Configuration); // binds "BlobService:S3"

public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)), ct);
}
```

## Documentation

Full feature reference, configuration, and gotchas:
https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.BlobStorage.AwsS3.md
