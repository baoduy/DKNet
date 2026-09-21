---
name: dknet-blob-storage
description: "Covers DKNet.Svc.BlobStorage.Abstractions, the .NET blob storage abstraction (IBlobService, BlobService, BlobRequest, BlobDetails.BlobData/BlobStreamData/BlobResult/BlobDataResult, BlobServiceOptions), plus its three providers: DKNet.Svc.BlobStorage.Local (AddLocalDirectoryBlobService, path-traversal-safe filesystem storage), DKNet.Svc.BlobStorage.AwsS3 (AddS3BlobService, AWS S3 and S3-compatible endpoints like MinIO and Cloudflare R2), and DKNet.Svc.BlobStorage.AzureStorage (AddAzureStorageAdapter, managed identity, SAS URLs). Use to upload, download, list, or delete files; wire DI/IConfiguration binding; generate a public or SAS URL; stream a large upload without buffering; test with Azurite or MinIO containers; or debug FileLoadException, InvalidOperationException, UnauthorizedAccessException path-traversal, or a GetAsync miss that throws on Local but returns null on S3/Azure."
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.Svc.BlobStorage.Abstractions,DKNet.Svc.BlobStorage.AwsS3,DKNet.Svc.BlobStorage.AzureStorage,DKNet.Svc.BlobStorage.Local"
---

# DKNet blob storage abstraction and adapters

This skill teaches the `IBlobService` contract and its three provider packages: how to register one, save/read/
list/delete blobs, generate public URLs, and avoid the traps that differ across Local, S3, and Azure. Open
`references/<PackageId>.md` for full member tables, runtime behaviour, and testing notes per package.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.Svc.BlobStorage.Abstractions` | `dotnet add package DKNet.Svc.BlobStorage.Abstractions` | `IBlobService`, `BlobService` base class, `BlobRequest`/`BlobDetails.*` records, `BlobServiceOptions` | none | [reference](references/DKNet.Svc.BlobStorage.Abstractions.md) |
| `DKNet.Svc.BlobStorage.Local` | `dotnet add package DKNet.Svc.BlobStorage.Local` | `AddLocalDirectoryBlobService` — filesystem-backed `IBlobService` with path-traversal protection | Abstractions | [reference](references/DKNet.Svc.BlobStorage.Local.md) |
| `DKNet.Svc.BlobStorage.AwsS3` | `dotnet add package DKNet.Svc.BlobStorage.AwsS3` | `AddS3BlobService` — `IBlobService` over AWS S3, MinIO, or Cloudflare R2 | Abstractions | [reference](references/DKNet.Svc.BlobStorage.AwsS3.md) |
| `DKNet.Svc.BlobStorage.AzureStorage` | `dotnet add package DKNet.Svc.BlobStorage.AzureStorage` | `AddAzureStorageAdapter` — `IBlobService` over Azure Blob Storage, managed identity + SAS URLs | Abstractions | [reference](references/DKNet.Svc.BlobStorage.AzureStorage.md) |

## Quick start

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json: { "BlobStorage": { "LocalFolder": { "RootFolder": "/var/app/storage" } } }
builder.Services.AddLocalDirectoryBlobService(builder.Configuration);

var app = builder.Build();

app.MapPost("/files/{name}", async (string name, IBlobService blobService, HttpRequest request, CancellationToken ct) =>
{
    var data = await BinaryData.FromStreamAsync(request.Body, ct);
    var location = await blobService.SaveAsync(new BlobDetails.BlobData(name, data) { Overwrite = true }, ct);
    return Results.Ok(location);
});

app.Run();
```

Swap `AddLocalDirectoryBlobService` for `AddS3BlobService` or `AddAzureStorageAdapter` to target S3-compatible
storage or Azure Blob Storage — application code above the DI registration line never changes.

## Rules

1. Depend on `IBlobService` (from `DKNet.Svc.BlobStorage.Abstractions`) everywhere in application code. Never
   reference `LocalBlobService`, `S3BlobService`, or `AzureStorageBlobService` directly — each provider registers
   only the interface, so `GetRequiredService<S3BlobService>()`-style resolution throws.
2. Register exactly one provider's DI extension at the composition root: `AddLocalDirectoryBlobService`,
   `AddS3BlobService`, or `AddAzureStorageAdapter`. Registering more than one does not error — the last
   registration wins whenever `IBlobService` is resolved as a single instance.
3. `using DKNet.Svc.BlobStorage.AwsS3;` is required to call `AddS3BlobService` — its `S3Setup` class sits in a
   normal namespace, unlike `AddLocalDirectoryBlobService` and `AddAzureStorageAdapter`, which are declared ambient
   under `Microsoft.Extensions.DependencyInjection` and need no extra `using` to *call* (their option types still
   do: `LocalDirectoryOptions`, `AzureStorageOptions`).
4. Registration lifetimes differ: Local is **Scoped**; S3 and Azure are both **Singleton**. Don't assume a fresh
   provider instance per request for S3/Azure, and don't assume a shared one for Local.
5. `GetAsync`'s "missing blob" signal differs per provider: Local throws `FileNotFoundException`; S3 and Azure
   both return `null`. `CheckExistsAsync` returns `false` on every provider — prefer it when only presence
   matters.
6. `Overwrite` on `BlobData`/`BlobStreamData` defaults to `false`. Local and S3 throw `InvalidOperationException`
   on a duplicate save without it; Azure instead lets the SDK's `RequestFailedException` (409, "BlobAlreadyExists")
   surface unwrapped.
7. `MaxFileSizeInMb`, `MaxFileNameLength`, and `IncludedExtensions` all default to disabled (`0`/`0`/`[]`) — there
   is no built-in cap unless you set one, and the size math is decimal MB (`* 1_000_000`), not binary MiB.
8. A blob name with no file extension makes `BlobRequest.Type` default to `BlobTypes.Directory` — a bare name
   like `"reports"` needs no extra flag to be treated as a folder, but a genuinely extension-less file needs
   `Type = BlobTypes.File` set explicitly, or a delete against it becomes a recursive folder delete.
9. Never hand-roll a `..`-string path-traversal check around the Local provider — its `GetFinalPath` guard already
   resolves and normalizes every request and throws `UnauthorizedAccessException` outside `RootFolder`.
10. On Azure, set `BlobServiceClientFactory` via `services.Configure<AzureStorageOptions>(...)` in code, never
    through the `Action<AzureStorageOptions>` overload of `AddAzureStorageAdapter` — that overload's values never
    reach the resolved `IOptions<AzureStorageOptions>` (see Gotchas).

## How to ...

### Save a blob and allow overwrite

**When**: writing or replacing a file by name.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.Threading;

var services = new ServiceCollection();
services.AddLocalDirectoryBlobService(new ConfigurationBuilder().Build());
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();

var draft = new BlobDetails.BlobData("reports/2026/q1.pdf", BinaryData.FromString("first draft"));
await blobService.SaveAsync(draft, CancellationToken.None);

var final = new BlobDetails.BlobData("reports/2026/q1.pdf", BinaryData.FromString("final")) { Overwrite = true };
await blobService.SaveAsync(final, CancellationToken.None);
```

Notes: a second save without `Overwrite = true` throws on Local/S3 (`InvalidOperationException`); Azure surfaces
the SDK's 409 instead.

### Read a blob back and handle a missing one

**When**: code must run against any of the three providers without crashing on a miss.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Threading;

var services = new ServiceCollection();
services.AddLocalDirectoryBlobService(new ConfigurationBuilder().Build());
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();

async Task<BinaryData?> TryReadAsync(IBlobService service, string name, CancellationToken ct)
{
    try
    {
        var result = await service.GetAsync(new BlobRequest(name), ct);
        return result?.Data; // S3 and Azure both return null here instead of throwing
    }
    catch (FileNotFoundException)
    {
        return null; // Local-only signal for a missing blob
    }
}

var content = await TryReadAsync(blobService, "reports/2026/q1.pdf", CancellationToken.None);
```

Notes: `CheckExistsAsync` returns `false` on every provider for a miss — prefer it when you only need presence,
not content.

### List everything under a prefix

**When**: enumerating a "folder" recursively.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.Threading;

var services = new ServiceCollection();
services.AddLocalDirectoryBlobService(new ConfigurationBuilder().Build());
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();

var fileNames = new List<string>();
await foreach (var item in blobService.ListItemsAsync(new BlobRequest("reports") { Type = BlobTypes.Directory }, CancellationToken.None))
    if (item.Type == BlobTypes.File)
        fileNames.Add(item.Name);
```

Notes: S3 pages internally via `ContinuationToken` until every object is returned; Local performs one interleaved
filesystem walk. Do not assume "files, then directories" ordering on any provider.

### Stream a large upload without buffering it into memory

**When**: the payload is big enough that materializing it as `BinaryData` first is wasteful.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Threading;

var services = new ServiceCollection();
services.AddLocalDirectoryBlobService(new ConfigurationBuilder().Build());
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();

await using var fileStream = File.OpenRead("large-export.csv");
var savedName = await blobService.SaveAsync(
    new BlobDetails.BlobStreamData("exports/large-export.csv", fileStream) { Overwrite = true },
    CancellationToken.None);
```

Notes: all three shipped providers override `SaveAsync(BlobStreamData, ...)` to write straight from the stream;
only a hand-rolled `IBlobService`/`BlobService` subclass that skips that override falls back to the buffering
default.

### Get a time-limited SAS or pre-signed URL

**When**: sharing a private blob without handing out credentials. Not supported on Local.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AzureStorage;
using System.Threading;

var services = new ServiceCollection();
services.AddAzureStorageAdapter(new ConfigurationBuilder().Build());
services.Configure<AzureStorageOptions>(o =>
{
    o.ConnectionString = "UseDevelopmentStorage=true";
    o.ContainerName = "documents";
});
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();

var url = await blobService.GetPublicAccessUrl(
    new BlobRequest("reports/monthly.pdf"),
    TimeSpan.FromMinutes(15),
    CancellationToken.None);
```

Notes: S3's `GetPublicAccessUrl` returns a pre-signed URL the same way. Azure throws `NotSupportedException` for
a managed-identity (token-credential) client — SAS needs an account-key/connection-string client. Local always
throws `NotSupportedException`.

### Point the S3 provider at MinIO or Cloudflare R2

**When**: an S3-compatible endpoint instead of real AWS.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AwsS3;

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BlobService:S3:ConnectionString"] = "http://localhost:9000",
        ["BlobService:S3:BucketName"] = "app-documents",
        ["BlobService:S3:AccessKey"] = "minioadmin",
        ["BlobService:S3:Secret"] = "minioadmin",
        ["BlobService:S3:ForcePathStyle"] = "true"
    })
    .Build();

var services = new ServiceCollection();
services.AddS3BlobService(configuration);
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();
```

Notes: `ForcePathStyle = true` is required for MinIO and most S3-compatible services. For Cloudflare R2 also set
`DisablePayloadSigning = true`. `RegionEndpointName` has no effect either way — encode a region, if any, into
`ConnectionString`.

### Use Azure managed identity instead of a connection string

**When**: no account key/connection string is allowed.

```csharp
using Azure.Core;
using Azure.Storage.Blobs;
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AzureStorage;

// credential: pass in `new Azure.Identity.DefaultAzureCredential()` from the composition root.
public sealed class ManagedIdentityBlobSetup(TokenCredential credential)
{
    public IBlobService Build()
    {
        var services = new ServiceCollection();
        services.AddAzureStorageAdapter(new ConfigurationBuilder().Build()); // binds ContainerName, etc.
        services.Configure<AzureStorageOptions>(o =>
        {
            o.ContainerName = "documents";
            o.BlobServiceClientFactory = _ => Task.FromResult(
                new BlobServiceClient(new Uri("https://myaccount.blob.core.windows.net"), credential));
        });
        return services.BuildServiceProvider().GetRequiredService<IBlobService>();
    }
}
```

Notes: use the `IConfiguration` overload of `AddAzureStorageAdapter` plus `Configure<AzureStorageOptions>`, not
the `Action<AzureStorageOptions>` overload — that overload's values never reach the resolved options. A
managed-identity client generally cannot sign SAS URLs. `DKNet.Svc.BlobStorage.AzureStorage`'s own `.csproj` does
not reference `Azure.Identity` — but don't add it yourself either: `Azure.Storage.Blobs` already pulls in an
`Azure.Core` version that re-exports the whole `Azure.Identity` credential surface (`DefaultAzureCredential`
included) under that same namespace, so `using Azure.Identity;` plus `new DefaultAzureCredential()` at the call
site (the `TokenCredential credential` passed into `ManagedIdentityBlobSetup` above) already resolves it, no
extra package needed. Explicitly adding the standalone `Azure.Identity` package on top gives you two same-named
`DefaultAzureCredential` types and fails the build with `error CS0433: The type 'DefaultAzureCredential' exists
in both 'Azure.Core...' and 'Azure.Identity...'`.

### Test code that depends on IBlobService without Docker

**When**: unit/integration-testing application code that only needs *some* `IBlobService`, not S3/Azure-specific
behavior.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;

public sealed class BlobFixture : IDisposable
{
    public BlobFixture()
    {
        TestRoot = Path.Combine(Path.GetTempPath(), "test-blobs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TestRoot);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobStorage:LocalFolder:RootFolder"] = TestRoot
            })
            .Build();

        Service = new ServiceCollection()
            .AddLogging()
            .AddLocalDirectoryBlobService(config)
            .BuildServiceProvider()
            .GetRequiredService<IBlobService>();
    }

    public IBlobService Service { get; }
    public string TestRoot { get; }

    public void Dispose()
    {
        if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, true);
    }
}
```

Notes: reach for Azurite/MinIO containers (see the provider reference pages' Testing notes) only when the test
needs to verify S3- or Azure-specific behavior; Local's filesystem backing is faster and needs no Docker for
everything else.

## Runtime behaviour

Every save funnels through the same shape regardless of provider: `ValidateFile` (name/extension/size, shared
from Abstractions) → `GetBlobLocation` (path/key normalization) → the provider's own SDK write. The native client
each cloud provider needs (`AmazonS3Client`, `BlobContainerClient`) is built lazily on the *first* call after
registration and cached for the life of the resolved `IBlobService` instance — so the first call after startup
pays a real network/bucket-or-container-ensure round trip on S3 and Azure; Local pays nothing extra since it has
no client to build.

## Gotchas

- **`S3Options.RegionEndpointName` is inert** — building the S3 client never reads it. Encode the region into
  `ConnectionString` instead.
- **S3 bucket auto-creation on first use.** A `BucketName` typo silently creates a new empty bucket rather than
  failing — double-check `BucketName` before deploying.
- **S3 folder delete is one `DeleteObjectAsync` per key, not a batch call** (this SDK version's batch API needs a
  Content-MD5 header that MinIO rejects). Expect O(n) round trips deleting a very large prefix against real AWS
  S3.
- **Local's `RootFolder` defaults to `{CurrentDirectory}/LocalStore` when unset** — that directory differs
  between `dotnet run`, a published binary, and a container. Always set `RootFolder` explicitly outside a quick
  local run.
- **`ListItemsAsync`'s `Details.ContentType` on S3 is always `""`.** Call `GetAsync` for the real content type;
  don't trust the listing for it.
- **`SizeLimitedStream` (the non-seekable-stream size guard used internally) is read-only and forward-only** —
  `Seek`/`SetLength`/`Write`/`Length`/`Position` all throw `NotSupportedException`. Only sequential
  `Read`/`ReadAsync` works on a stream handed back from `ValidateFile(BlobStreamData)`.
- **`IBlobService`'s own default interface members for `OpenReadAsync`/`SaveAsync(BlobStreamData, ...)` still
  fully buffer the payload.** Only a provider that overrides them (all three shipped providers do) avoids that —
  a hand-rolled provider that skips the override gets no memory benefit from calling them.

## Do not

- `IBlobService.SaveAsync(byte[] data, ...)` — no such overload exists. Wrap arrays with `BinaryData.FromBytes(...)`.
- `IBlobServiceFactory`, or any provider registry/selector type — does not exist. DI registration order ("the
  last registration wins") is the only selection mechanism.
- `S3Options.Region` / `S3Options.RegionEndpoint` — the real (inert) property is `RegionEndpointName`.
- `AddLocalBlobService`, `AddLocalBlobStorage`, `UseLocalBlobStorage` — the real method is
  `AddLocalDirectoryBlobService`.
- `LocalBlobServiceOptions` / `LocalStorageOptions` — the real type is `LocalDirectoryOptions`.
- `AzureStorageOptions.UseManagedIdentity` — no such flag. Managed identity is expressed only by setting
  `BlobServiceClientFactory` and leaving `ConnectionString` unset.
- `GetRequiredService<S3BlobService>()` / `<AzureStorageBlobService>()` / `<LocalBlobService>()` — only
  `IBlobService` is registered; resolving a concrete provider type throws `InvalidOperationException` unless you
  registered it yourself.
- `new AmazonS3Client(...)` handed into `S3BlobService` — there is no constructor overload that accepts a
  pre-built client.
- `S3Options.MaxKeys` or any page-size option — pagination is automatic and internal, not caller-tunable.

## Related skills

- `dknet-packages` — start there to confirm blob storage is the right package family before wiring DI.
- `dknet-efcore-domain-model` — for the aggregate/entity that stores a blob's returned location string as a plain
  property.
- `dknet-efcore-specifications` — for querying/persisting entities that reference a blob location; `IBlobService`
  itself never appears in a specification.
- `dknet-efcore-save-pipeline` — to raise a domain event after `SaveAsync` returns a location, rather than
  calling `IBlobService` from inside a `SaveChanges` interceptor.
- `dknet-efcore-data-security` — for encrypting/row-securing the entity that references a blob, not the blob
  bytes themselves (that's `dknet-services`).
- `dknet-codegen` — unrelated to blob storage; route here only for `[CrudAction]`/`[GenerateDto]` questions.
- `dknet-slimbus-cqrs` — for the command/handler that calls `IBlobService` as part of a CQRS vertical slice.
- `dknet-aspcore-api` — for minimal-API endpoint conventions around the upload/download endpoint itself, not the
  storage call.
- `dknet-idempotency` — for making a repeated upload request safe to retry; blob storage's own `Overwrite` flag
  is not idempotency.
- `dknet-services` — for encrypting blob bytes before `SaveAsync`, or generating a PDF to then store via
  `IBlobService`.
- `dknet-core-utilities` — for a random file-name/key generator to hand to `SaveAsync`; not blob-storage-specific
  itself.
- `dknet-testing` — for TestContainers fixture conventions (Azurite/MinIO) beyond what this skill's reference
  pages already cover, and for testing inside the DKNet repo itself.

## References

- [references/DKNet.Svc.BlobStorage.Abstractions.md](references/DKNet.Svc.BlobStorage.Abstractions.md) — the
  `IBlobService` contract, `BlobService` base class, request/result records, shared validation options, and how
  to write your own provider.
- [references/DKNet.Svc.BlobStorage.AwsS3.md](references/DKNet.Svc.BlobStorage.AwsS3.md) — `S3BlobService`/
  `S3Options`, MinIO/R2 endpoint configuration, pagination, and folder-delete mechanics.
- [references/DKNet.Svc.BlobStorage.AzureStorage.md](references/DKNet.Svc.BlobStorage.AzureStorage.md) —
  `AzureStorageBlobService`/`AzureStorageOptions`, managed identity, SAS URLs, and the folder-marker heuristic.
- [references/DKNet.Svc.BlobStorage.Local.md](references/DKNet.Svc.BlobStorage.Local.md) — `LocalBlobService`/
  `LocalDirectoryOptions`, the path-traversal guard, and the no-Docker testing fixture pattern.
- Docs site: https://baoduy.github.io/DKNet/
- NuGet: https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Abstractions (swap the id suffix for `AwsS3`,
  `AzureStorage`, or `Local`)
