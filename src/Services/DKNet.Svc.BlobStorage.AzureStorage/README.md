# DKNet.Svc.BlobStorage.AzureStorage

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.AzureStorage)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AzureStorage/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.AzureStorage)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AzureStorage/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

Azure Blob Storage implementation of `IBlobService` from
[DKNet.Svc.BlobStorage.Abstractions](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Abstractions/), backed
by `Azure.Storage.Blobs`.

## Features

- `AzureStorageBlobService` — full `IBlobService` implementation
- Connection-string or `BlobServiceClientFactory` client construction (the latter for managed-identity auth)
- Creates the configured container on first use if it does not exist
- SAS read URLs (`GetPublicAccessUrl`), default 1-day expiry

## Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.AzureStorage
```

## Quick Start

```jsonc
// appsettings.json
{
  "BlobService": {
    "AzureStorage": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...",
      "ContainerName": "documents"
    }
  }
}
```

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

builder.Services.AddAzureStorageAdapter(builder.Configuration); // binds "BlobService:AzureStorage"

public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)), ct);
}
```

There are two registration overloads, both extension members on `IServiceCollection`:

| Overload | What it does |
|---|---|
| `AddAzureStorageAdapter(IConfiguration configuration)` | Binds `AzureStorageOptions` from `AzureStorageOptions.Name` and registers `IBlobService → AzureStorageBlobService` as `Scoped`. **Use this one.** |
| `AddAzureStorageAdapter(Action<AzureStorageOptions> config)` | Registers the populated instance as a singleton `AzureStorageOptions` and the same scoped service — but the service reads `IOptions<AzureStorageOptions>`, so the values never reach it. Add `services.Configure<AzureStorageOptions>(...)` instead for anything configuration cannot bind, such as `BlobServiceClientFactory`. |

## Configuration — `AzureStorageOptions`

`AzureStorageOptions` extends `BlobServiceOptions`, so `IncludedExtensions`, `MaxFileNameLength`, and
`MaxFileSizeInMb` apply here unchanged (see the Abstractions package).

| Option | Type | Default | Effect |
|---|---|---|---|
| `ContainerName` | `string` | *(required)* | Target container. Created on first use when missing. |
| `ConnectionString` | `string?` | `null` | Storage account connection string. Used only when `BlobServiceClientFactory` is unset. |
| `BlobServiceClientFactory` | `Func<AzureStorageOptions, Task<BlobServiceClient>>?` | `null` | Builds the client yourself — the hook for managed identity or any custom client. Takes precedence over `ConnectionString`, and cannot be set by configuration binding. |
| `AzureStorageOptions.Name` (static) | `string` | `"BlobService:AzureStorage"` | The configuration section the `IConfiguration` overload binds from. |

Leaving both `ConnectionString` and `BlobServiceClientFactory` unset throws `ArgumentException` on the first
operation. Per-call settings live on the request: `BlobDetails.BlobData.Overwrite` (default `false`) and
`GetPublicAccessUrl(..., expiresFromNow)` (default `TimeSpan.FromDays(1)`).

## Documentation

Full feature reference, diagrams, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/Services/DKNet.Svc.BlobStorage.AzureStorage.md
