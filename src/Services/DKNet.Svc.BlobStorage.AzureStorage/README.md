# DKNet.Svc.BlobStorage.AzureStorage

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.AzureStorage)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AzureStorage/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.AzureStorage)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AzureStorage/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../../LICENSE)

Azure Blob Storage implementation of `IBlobService` from
[DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions), backed by `Azure.Storage.Blobs`.

## Features

- `AzureStorageBlobService` — full `IBlobService` implementation
- Connection-string or `BlobServiceClientFactory` registration (the latter for managed identity/Azure AD auth)
- Auto-creates the configured container if it doesn't exist
- SAS-token public URLs (`GetPublicAccessUrl`), default 1-day expiry, read-only

## Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.AzureStorage
```

## Quick Start

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

builder.Services.AddAzureStorageAdapter(builder.Configuration); // binds "BlobService:AzureStorage"

public sealed class ReportStorage(IBlobService blobService)
{
    public Task<string> SaveAsync(Stream pdf, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)), ct);
}
```

## Documentation

Full feature reference, configuration, and gotchas:
https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.BlobStorage.AzureStorage.md
