# DKNet.Svc.BlobStorage.Local

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.Local)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Local/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.Local)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Local/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../../LICENSE)

Local-filesystem implementation of `IBlobService` from
[DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions) — for local development, tests, and
single-instance deployments.

## Features

- `LocalBlobService` — full `IBlobService` implementation over a configured root folder
- Path-traversal protection (`UnauthorizedAccessException` on escape attempts)
- No external service required — runs anywhere, including CI

## Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.Local
```

## Quick Start

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

builder.Services.AddLocalDirectoryBlobService(builder.Configuration); // binds "BlobStorage:LocalFolder"

public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)), ct);
}
```

## Documentation

Full feature reference, configuration, and gotchas:
https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.BlobStorage.Local.md
