# DKNet.Svc.BlobStorage.Abstractions

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.Abstractions)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Abstractions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.Abstractions)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Abstractions/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

Provider-agnostic contract for blob storage: the `IBlobService` interface, shared request/result models, and file
validation options. Pair it with one provider package (AWS S3, Azure Storage, or Local) to get a working
implementation.

## Features

- `IBlobService` — save, get, list, delete, check existence, and generate public URLs, uniformly across providers
- `BlobData`/`BlobResult`/`BlobDataResult` — strongly-typed request/result records built on `BinaryData`
- `BlobServiceOptions` — opt-in file name length, file size, and extension validation shared by every provider
- `BlobService` — the abstract base class every provider derives from, with the shared validation already wired
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

## Configuration — `BlobServiceOptions`

Every provider's own options type extends this one, so these three checks behave identically on every backend.
All three are **opt-in**: a value of `0` or an empty collection disables that check.

| Option | Type | Default | Effect |
|---|---|---|---|
| `IncludedExtensions` | `IEnumerable<string>` | `[]` | When non-empty, `SaveAsync` accepts only these extensions (compared case-insensitively, leading dot included). |
| `MaxFileNameLength` | `int` | `0` | `0` disables the check; otherwise a longer `Name` throws `FileLoadException`. |
| `MaxFileSizeInMb` | `int` | `0` | `0` disables the check; otherwise the limit is `MaxFileSizeInMb * 1_000_000` bytes (decimal megabytes). |

Failures throw `FileLoadException` with `"File name is invalid."`, `"File extension is invalid."`, or
`"File size is invalid."`.

## Public extension points

| Member | Accessibility | What you do with it |
|---|---|---|
| `IBlobService` | `public interface` | Implement directly for a provider that needs no shared behaviour. |
| `BlobService` | `public abstract class` | The usual base — implement its six abstract members. |
| `BlobService.ValidateFile(BlobData)` | `protected virtual` | Call it from your `SaveAsync`; override to add rules. |
| `BlobService.GetBlobLocation(BlobRequest)` | `protected virtual` | Override to change how a name becomes a provider path (the default prefixes `/`). |
| `BlobService.GetItemAsync(...)` | `public virtual` | Override when your store can fetch metadata without a full listing. |
| `BlobServiceOptions` | `public class` | Derive so your provider's options inherit the shared validation. |

`BlobDetails.BlobData` also carries two per-call settings: `Overwrite` (`bool`, default `false`) and
`ContentType` (`string`, defaults to `Name.GetContentTypeByExtension()`).

## Documentation

Full feature reference, diagrams, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/Services/DKNet.Svc.BlobStorage.Abstractions.md
