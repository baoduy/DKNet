# DKNet.Svc.BlobStorage.Abstractions

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.Abstractions)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Abstractions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.Abstractions)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Abstractions/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../../LICENSE)

Provider-agnostic contract for blob storage: the `IBlobService` interface, shared request/result models, and file
validation options. Pair it with one provider package (AWS S3, Azure Storage, or Local) to get a working
implementation.

## Features

- `IBlobService` — save, get, list, delete, check existence, and generate public URLs, uniformly across providers
- `BlobData`/`BlobResult`/`BlobDataResult` — strongly-typed request/result records built on `BinaryData`
- `BlobServiceOptions` — opt-in file name length, file size, and extension validation shared by every provider
- Automatic content-type detection from file extension

## Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.Abstractions
```

## Quick Start

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

public sealed class DocumentService(IBlobService blobService)
{
    public Task<string> UploadAsync(string fileName, Stream content, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobData(fileName, BinaryData.FromStream(content)), ct);
}
```

Register a concrete provider (e.g. `services.AddLocalDirectoryBlobService(configuration)` from
`DKNet.Svc.BlobStorage.Local`) to supply the `IBlobService` implementation.

## Documentation

Full feature reference, configuration, and gotchas:
https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.BlobStorage.Abstractions.md
