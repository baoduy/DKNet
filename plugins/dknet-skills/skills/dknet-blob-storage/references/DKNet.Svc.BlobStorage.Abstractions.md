# DKNet.Svc.BlobStorage.Abstractions

| Field | Value |
|---|---|
| Area | Services |
| NuGet | `dotnet add package DKNet.Svc.BlobStorage.Abstractions` |
| Docs | [DKNet.Svc.BlobStorage.Abstractions.md](https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.BlobStorage.Abstractions.md) |
| Source | [src/Services/DKNet.Svc.BlobStorage.Abstractions](https://github.com/baoduy/DKNet/tree/dev/src/Services/DKNet.Svc.BlobStorage.Abstractions) |
| Depends on (DKNet) | none |
| Depends on (3rd party) | `System.Memory.Data` (for `BinaryData`); `Meziantou.Analyzer` (build-time analyzer only) |
| Target framework | net10.0 |

## Purpose

The provider-agnostic contract for blob storage: the `IBlobService` interface, the `BlobService` abstract base
class with shared save-time validation, and the request/result record family (`BlobRequest`, `BlobDetails`,
`BlobDetails.BlobData`/`BlobStreamData`/`BlobResult`/`BlobDataResult`) built on `BinaryData`/`Stream`. Application
code depends only on this package's types; a sibling provider package (`DKNet.Svc.BlobStorage.Local`, `.AwsS3`, or
`.AzureStorage`) supplies the concrete `IBlobService` implementation via DI.

This package is **not** an implementation — it ships zero working storage backends. Without a provider package
registered there is no `IBlobService` to resolve.

## Entry points

This package has no DI extension methods, attributes, or analyzers of its own — it is a pure contract + base-class
library. The "entry points" a consumer touches are the base class a provider derives from and the default interface
members every `IBlobService` implementer gets for free.

| Call | Signature | Notes |
|---|---|---|
| Derive a provider | `public abstract class BlobService(BlobServiceOptions options) : IBlobService` | Ctor throws `ArgumentNullException` if `options` is null. Application code never instantiates `BlobService` directly (abstract) — provider packages do. |
| Shared save validation | `protected virtual void ValidateFile(BlobDetails.BlobData item)` | Call first in every `SaveAsync` override; throws `FileLoadException`. |
| Shared stream validation | `protected virtual Stream ValidateFile(BlobDetails.BlobStreamData item)` | Returns the stream to actually read from (may wrap in `SizeLimitedStream`) — read from the *returned* stream, not `item.Data`, or the size ceiling is not enforced for non-seekable sources. |
| Normalize a path | `protected virtual string GetBlobLocation(BlobRequest item)` | Default: prefixes `/` if not already present. Override to change how a name maps to a provider-native path. |
| Get item without full listing | `public virtual async Task<BlobDetails.BlobResult?> GetItemAsync(BlobRequest blob, CancellationToken cancellationToken = default)` | Default implementation is "first item from `ListItemsAsync`" — override for a cheaper metadata-only fetch. |

## Public surface

Namespace `DKNet.Svc.BlobStorage.Abstractions`:

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IBlobService` | interface | The unified storage contract every provider implements. | `Task<bool> CheckExistsAsync(BlobRequest, CancellationToken = default)`; `Task<bool> DeleteAsync(BlobRequest, CancellationToken = default)`; `Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest, CancellationToken = default)`; `Task<BlobDetails.BlobResult?> GetItemAsync(BlobRequest, CancellationToken = default)`; `Task<Uri> GetPublicAccessUrl(BlobRequest, TimeSpan? expiresFromNow = null, CancellationToken = default)`; `IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(BlobRequest, CancellationToken = default)`; `Task<string> SaveAsync(BlobDetails.BlobData, CancellationToken = default)`; default-interface member `Task<Stream?> OpenReadAsync(BlobRequest, CancellationToken = default)`; default-interface member `Task<string> SaveAsync(BlobDetails.BlobStreamData, CancellationToken = default)` |
| `BlobService` | abstract class | Shared base every provider derives from. | Ctor `BlobService(BlobServiceOptions options)`; abstract `CheckExistsAsync`, `DeleteAsync`, `GetAsync`, `GetPublicAccessUrl`, `ListItemsAsync`, `SaveAsync(BlobData, ...)` (six members every provider must implement); virtual `GetItemAsync`, `OpenReadAsync(BlobRequest, ...)`, `SaveAsync(BlobStreamData, ...)`; `protected virtual GetBlobLocation(BlobRequest)`; `protected virtual ValidateFile(BlobDetails.BlobData)`; `protected virtual Stream ValidateFile(BlobDetails.BlobStreamData)` |
| `BlobServiceOptions` | class | Shared, opt-in save-time validation options every provider's options type extends. | `IReadOnlyList<string> IncludedExtensions { get; set; } = []`; `int MaxFileNameLength { get; set; }`; `int MaxFileSizeInMb { get; set; }` |
| `BlobExtensions` | static class | Content-type sniffing by extension. | `static string GetContentTypeByExtension(this string fileName)` |
| `BlobTypes` | enum | Distinguishes a file target from a directory/prefix target. | `File`, `Directory` |
| `BlobRequest` | record | Base shape for every request; carries the blob's name and (derived) type. | `BlobRequest(string Name)`; `BlobTypes Type { get; init; }` — auto-computed as `Directory` when `Path.GetExtension(Name)` is empty, else `File` |
| `BlobDetails` | record | Metadata-only payload; also the namespace for the result/data record family below. | `DateTime CreatedOn { get; init; }`; `DateTime LastModified { get; init; }`; `long ContentLength { get; init; }`; `required string ContentType { get; init; }` |
| `BlobDetails.BlobResult` | record (nested) | Metadata result: a `BlobRequest` plus optional `Details`. | `BlobResult(string Name) : BlobRequest(Name)`; `BlobDetails? Details { get; init; }` (null until fetched) |
| `BlobDetails.BlobDataResult` | record (nested) | `GetAsync`'s return shape: content + metadata. | `BlobDataResult(string Name, BinaryData Data) : BlobResult(Name)` |
| `BlobDetails.BlobData` | record (nested) | `SaveAsync`'s buffered input shape. | `BlobData(string Name, BinaryData Data) : BlobRequest(Name)`; `bool Overwrite { get; set; } = false`; `string ContentType { get; init; } = Name.GetContentTypeByExtension()` |
| `BlobDetails.BlobStreamData` | record (nested) | `SaveAsync`'s stream-backed input shape — avoids buffering a large payload into `BinaryData` upfront. Caller owns and disposes `Data`. | `BlobStreamData(string Name, Stream Data) : BlobRequest(Name)`; `bool Overwrite { get; set; } = false`; `string ContentType { get; init; } = Name.GetContentTypeByExtension()` |

`SizeLimitedStream` is `internal sealed` — not part of the public surface, but explains observable behaviour under
Runtime behaviour and Gotchas below.

## Options & defaults

| Option | Type | Default | Effect |
|---|---|---|---|
| `IncludedExtensions` | `IReadOnlyList<string>` | `[]` (empty) | Empty ⇒ no extension filtering. Non-empty ⇒ `SaveAsync` accepts only listed extensions, compared case-insensitively, leading dot included. |
| `MaxFileNameLength` | `int` | `0` | `0` disables the check (not "reject zero-length names"). Otherwise `name.Length > MaxFileNameLength` throws `FileLoadException("File name is invalid.")`. |
| `MaxFileSizeInMb` | `int` | `0` | `0` disables the check — no built-in cap unless set. Otherwise the byte ceiling is `MaxFileSizeInMb * 1_000_000` (decimal MB, not `* 1024 * 1024`). |

All three checks are opt-in and live in `BlobService.ValidateFile`/`ValidateNameAndExtension`, shared by every
provider's `SaveAsync` overload (buffered `BlobData` and streamed `BlobStreamData` alike).

## Usage patterns

### Depend on `IBlobService` from application code

**When**: business/domain code needs to store or fetch a file without knowing which backend is configured.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Threading;

public sealed class DocumentService(IBlobService blobService)
{
    public async Task<string> UploadAsync(string fileName, Stream content, CancellationToken ct)
    {
        var blob = new BlobDetails.BlobData(fileName, BinaryData.FromStream(content));
        return await blobService.SaveAsync(blob, ct); // returns the stored location
    }
}
```

**Notes**: register exactly one provider package's DI extension (e.g. `AddLocalDirectoryBlobService`) in the
composition root; this package alone has no `IBlobService` to resolve.

### Save a large file without buffering it into memory first

**When**: the payload is large enough that fully materializing it into `BinaryData` up front is wasteful.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Threading;

public sealed class UploadHandler(IBlobService blobService)
{
    public Task<string> SaveStreamAsync(string fileName, Stream content, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobStreamData(fileName, content) { Overwrite = true }, ct);
}
```

**Notes**: `SaveAsync(BlobStreamData, ...)` is a default interface member on `IBlobService` (and `virtual` on
`BlobService`) — every provider gets it automatically. The *default* implementation buffers the stream into
`BinaryData` and delegates to `SaveAsync(BlobData, ...)`; a provider can override it for a genuinely streaming
write (all three shipped providers do — see their own reference pages). The caller still owns and must dispose
`content`.

### Read a blob as a stream without loading it fully into memory

**When**: consuming a large blob (e.g. proxying it to an HTTP response) without buffering the whole thing.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Threading;

public sealed class DownloadHandler(IBlobService blobService)
{
    public async Task<Stream?> OpenAsync(string fileName, CancellationToken ct) =>
        await blobService.OpenReadAsync(new BlobRequest(fileName), ct);
}
```

**Notes**: `OpenReadAsync` is a default interface member; its *default* implementation buffers via `GetAsync` and
wraps the `BinaryData` in a stream (so it does not avoid buffering unless the provider overrides it). The caller
owns and must dispose the returned stream. Returns `null` when the blob does not exist on providers that return
`null` from `GetAsync` — Local instead throws (see the provider reference pages).

### Write your own `IBlobService` provider

**When**: adding a new backend (e.g. an in-memory test double) that isn't one of the three shipped providers.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.Runtime.CompilerServices;
using System.Threading;

public sealed class InMemoryBlobService(BlobServiceOptions options) : BlobService(options)
{
    private readonly Dictionary<string, BinaryData> _blobs = new(StringComparer.Ordinal);

    public override Task<string> SaveAsync(BlobDetails.BlobData blob, CancellationToken cancellationToken = default)
    {
        ValidateFile(blob);                       // shared rules first
        _blobs[GetBlobLocation(blob)] = blob.Data; // normalized "/name"
        return Task.FromResult(blob.Name);
    }

    public override Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
        Task.FromResult(_blobs.ContainsKey(GetBlobLocation(blob)));

    public override Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
        Task.FromResult(_blobs.Remove(GetBlobLocation(blob)));

    public override Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest blob,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_blobs.TryGetValue(GetBlobLocation(blob), out var data)
            ? new BlobDetails.BlobDataResult(blob.Name, data)
            : null);

    public override Task<Uri> GetPublicAccessUrl(BlobRequest blob, TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public override async IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(BlobRequest blob,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in _blobs.Keys) yield return new BlobDetails.BlobResult(key);
        await Task.CompletedTask;
    }
}
```

**Notes**: `CheckExistsAsync`, `DeleteAsync`, `GetAsync`, `GetPublicAccessUrl`, `ListItemsAsync`, and
`SaveAsync(BlobData, ...)` are the six abstract members every provider must implement. `OpenReadAsync` and
`SaveAsync(BlobStreamData, ...)` come for free via the base class's `virtual` defaults — override only for genuine
streaming I/O.

### Validate a stream-backed save when hand-rolling a provider

**When**: implementing `SaveAsync(BlobStreamData, ...)` yourself instead of accepting the inherited default.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

public sealed class LoggingBlobService(BlobServiceOptions options) : BlobService(options)
{
    public override Task<string> SaveAsync(BlobDetails.BlobData blob, CancellationToken cancellationToken = default) =>
        SaveAsync(new BlobDetails.BlobStreamData(blob.Name, blob.Data.ToStream()) { Overwrite = blob.Overwrite },
            cancellationToken);

    public override async Task<string> SaveAsync(BlobDetails.BlobStreamData blob,
        CancellationToken cancellationToken = default)
    {
        var streamToRead = ValidateFile(blob); // may be blob.Data itself, or a size-enforcing wrapper
        await using var buffer = new MemoryStream();
        await streamToRead.CopyToAsync(buffer, cancellationToken); // write streamToRead, NOT blob.Data
        return blob.Name;
    }

    public override Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public override Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public override Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest blob,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<BlobDetails.BlobDataResult?>(null);

    public override Task<Uri> GetPublicAccessUrl(BlobRequest blob, TimeSpan? expiresFromNow = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public override async IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(BlobRequest blob,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}
```

**Notes**: `ValidateFile(BlobStreamData)` returns `item.Data` unchanged when the stream `CanSeek` (its `Length` is
checked directly), or a wrapping stream when it cannot seek — reading from `blob.Data` directly instead of the
returned stream silently skips the size ceiling for non-seekable sources.

### Derive provider options from `BlobServiceOptions`

**When**: writing a new provider's options type.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

public sealed class MyProviderOptions : BlobServiceOptions
{
    public string ConnectionString { get; set; } = "";
}
```

**Notes**: this is exactly how `LocalDirectoryOptions`, `S3Options`, and `AzureStorageOptions` are built —
inheriting `IncludedExtensions`/`MaxFileNameLength`/`MaxFileSizeInMb` for free so validation behaves identically
across backends.

## Runtime behaviour

In order, for every `SaveAsync(BlobData, ...)` call on a `BlobService`-derived provider:

1. The provider's override calls `ValidateFile(BlobDetails.BlobData item)` (inherited, `protected virtual`).
2. `ValidateFile` calls the private `ValidateNameAndExtension(name)`: checks `MaxFileNameLength` (throws
   `FileLoadException("File name is invalid.")` if exceeded), then — only if `IncludedExtensions` is non-empty —
   checks the extension (throws `FileLoadException("File extension is invalid.")` if missing or not listed).
3. If `MaxFileSizeInMb > 0`, `ValidateFile` measures `item.Data.ToMemory().Length` and throws
   `FileLoadException("File size is invalid.")` if it exceeds `MaxFileSizeInMb * 1_000_000` bytes.
4. The provider calls `GetBlobLocation(item)` (inherited, `protected virtual`) to normalize the name — default:
   prefix `/` if not already present.
5. The provider writes to its backing store using its SDK and returns the location string.

For `SaveAsync(BlobDetails.BlobStreamData, ...)` when the *default* interface/base-class implementation runs (a
provider that didn't override it): the stream is fully buffered via `BinaryData.FromStreamAsync`, then delegated to
step 1 above — so the "avoid buffering" benefit only exists for providers (or your own implementation) that
override this member and use `ValidateFile(BlobStreamData)`'s returned stream directly. When a provider does
implement true streaming, `ValidateFile(BlobStreamData)` returns either the original stream (seekable, length
checked upfront) or a `SizeLimitedStream` wrapper (non-seekable — `Read`/`ReadAsync` accumulate bytes read and throw
`FileLoadException("File size is invalid.")` the instant the running total exceeds the limit, so an oversized
non-seekable upload fails mid-stream rather than after full buffering).

## Diagnostics & exceptions

| Exception | When | Fix |
|---|---|---|
| `FileLoadException("File name is invalid.")` | `MaxFileNameLength > 0` and `Name.Length` exceeds it. | Shorten the name or raise/remove `MaxFileNameLength`. |
| `FileLoadException("File extension is invalid.")` | `IncludedExtensions` is non-empty and the blob's extension is missing or not in the (case-insensitive) list. | Use an allowed extension, or add it to `IncludedExtensions`. |
| `FileLoadException("File size is invalid.")` | `MaxFileSizeInMb > 0` and the payload exceeds `MaxFileSizeInMb * 1_000_000` bytes — checked upfront for buffered/seekable data, or incrementally while reading a non-seekable stream (`SizeLimitedStream`). | Reduce the payload or raise `MaxFileSizeInMb`. |
| `ArgumentNullException(nameof(options))` | `BlobService`'s constructor receives a `null` `options`. | Always pass a non-null `BlobServiceOptions` (or subclass) instance. |
| `NullReferenceException` | `GetContentTypeByExtension()` called on a `null` string. | Never pass a null file name; the method has no null guard by design. |
| `NotSupportedException` | Any `Stream` member on `SizeLimitedStream` other than `Read`/`ReadAsync`/`Flush` (e.g. `Seek`, `SetLength`, `Write`, `Length`, `Position`). | Don't seek/write the stream `ValidateFile(BlobStreamData)` hands back — it is read-only and forward-only. |

No analyzer/`DiagnosticDescriptor` surface — this package has no source generator or analyzer of its own.

## Gotchas

- **`MaxFileSizeInMb` is decimal megabytes (`* 1_000_000`), not `* 1024 * 1024`.** A "10 MB" cap actually rejects at
  10,000,000 bytes, ~4.6% smaller than a binary 10 MiB. Set the option slightly higher than the intended binary
  limit if that distinction matters.
- **Reading from `blob.Data` instead of `ValidateFile(BlobStreamData)`'s return value inside a hand-written
  provider's `SaveAsync(BlobStreamData, ...)` override silently skips the size ceiling** for non-seekable streams
  (no `SizeLimitedStream` wrapping happens). Always write the returned `Stream`, never the input
  `BlobStreamData.Data`, to the backing store.
- **`OpenReadAsync`'s and `SaveAsync(BlobStreamData, ...)`'s *default* implementations fully buffer the payload** —
  both the `IBlobService` default-interface-member versions and `BlobService`'s `virtual` versions. They give no
  memory-usage benefit over the plain `BlobData`/`GetAsync` path unless a specific provider overrides them for
  genuine streaming I/O (all three shipped providers do).
- **`GetContentTypeByExtension()` throws `NullReferenceException`, not `ArgumentNullException`, on a `null`
  input** — it calls `Path.GetExtension(fileName)` with no null guard. Validate the name is non-null before
  constructing a `BlobData`/`BlobStreamData` (whose `ContentType` default calls this extension).
- **`BlobRequest.Type` is derived, not independently settable unless you use `with`/an object initializer** — a
  name with no extension (`"documents"`) or an empty string defaults to `BlobTypes.Directory` automatically.
  Passing `"my-folder"` as a file name silently becomes a directory request. Always give file names a real
  extension, or explicitly set `Type = BlobTypes.File` when the name legitimately has none.
- **`MaxFileNameLength = 0` and `MaxFileSizeInMb = 0` (the defaults) mean the check is disabled, not "reject
  everything."** Relying on the default constructor gives no limits at all — an arbitrarily large name or file
  passes validation. Explicitly set a positive value if any cap is actually wanted.
- **`SizeLimitedStream` (used internally when `ValidateFile(BlobStreamData)` wraps a non-seekable stream) is
  read-only and forward-only** — `CanSeek`/`CanWrite` are `false`, and `Length`/`Position`/`Seek`/`SetLength`/`Write`
  all throw `NotSupportedException`. Only sequential `Read`/`ReadAsync` calls are supported on it.
- **This package has no DI extension methods of its own and no `ProjectReference` to any other DKNet package.**
  There is nothing to register from this package alone; `services.AddXxx()` always comes from a provider package.

## Anti-patterns & hallucination traps

- **`IBlobService.SaveAsync(byte[] data, ...)` or any `byte[]` overload — does not exist.** Every payload is
  `BinaryData` (`BlobData`) or `Stream` (`BlobStreamData`). Wrap arrays with `BinaryData.FromBytes(...)` instead.
- **A provider registry, factory interface, or `IBlobServiceFactory` — does not exist.** There is no way to select
  a provider at runtime beyond DI registration order; "the last registration wins when a single `IBlobService` is
  resolved" is the only mechanism.
- **`BlobService.ValidateFile` is `protected virtual`, not `public`.** Application code calling it directly on an
  `IBlobService` reference will not compile. It's for provider authors overriding `SaveAsync`, not consumers.
- **`BlobServiceOptions` does not default `MaxFileSizeInMb` to any "50 MB"-style cap** — the default is `0`
  (disabled). There is no built-in size cap in source.
- **Do not assume every provider except one throws on a missing blob, or that exactly one specific provider is
  "the throwing one" from memory alone** — check the provider's own reference page. (Concretely, in this DKNet
  version: S3 and Azure both return `null` from `GetAsync` on a miss; Local throws `FileNotFoundException`.)
- **Calling `blob.Data` on a `BlobStreamData` and assuming a size limit already applied is wrong** — the limit is
  enforced only through the stream `ValidateFile(BlobStreamData)` returns, not the original stream reference.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.Svc.BlobStorage.Local` | Reach for it in local development, tests, and CI where no cloud account should be needed. Provides `AddLocalDirectoryBlobService(IConfiguration)` and `LocalBlobService`/`LocalDirectoryOptions`. |
| `DKNet.Svc.BlobStorage.AwsS3` | Reach for it when blobs live in AWS S3 or an S3-compatible store (MinIO, Cloudflare R2). Provides `S3BlobService`/`S3Options`. |
| `DKNet.Svc.BlobStorage.AzureStorage` | Reach for it when blobs live in an Azure Storage account, especially with managed-identity auth. Provides `AzureStorageBlobService`/`AzureStorageOptions`. |
| `DKNet.Svc.Encryption` | Reach for it when a blob's bytes must be encrypted before they reach the store — compose in front of `SaveAsync`; this package does not encrypt anything itself. |
| `DKNet.EfCore.Events` | Reach for it to raise a domain event after `SaveAsync` returns the stored location, so a handler can attach it to an aggregate. |
| `DKNet.EfCore.Specifications` | Store the returned location string as a plain value on your entity; `IRepositorySpec` never needs to know which blob provider is in use. |

## Testing notes

Tests for this package live in a single shared test project covering Abstractions plus all three providers (not a
per-package split):

- **Pure unit tests, no fixture** for the types this package owns directly: record construction/inheritance/
  defaults, `GetContentTypeByExtension` theory data plus its null-input exception, and `BlobServiceOptions` default/
  setter coverage — xUnit `[Fact]`/`[Theory]` + Shouldly, no external dependencies, no containers.
- **Cross-provider validation tests** construct each provider's concrete options type directly (`LocalDirectoryOptions`,
  `S3Options`, `AzureStorageOptions`) with a single validation rule set (`IncludedExtensions`, `MaxFileSizeInMb`, or
  `MaxFileNameLength`), wrap in `Microsoft.Extensions.Options.Options.Create(...)`, new up the provider service
  directly (bypassing DI), and assert `Should.ThrowAsync<FileLoadException>(...)` with the exact message — proving
  the shared `BlobService.ValidateFile` behavior is identical across backends.
- **Stream round-trip tests** exercise `OpenReadAsync`/`SaveAsync(BlobStreamData, ...)` per-provider through
  Testcontainers-backed MinIO and Azurite fixtures plus a plain filesystem fixture (Local) — including a private
  non-seekable `Stream` test double (`CanSeek => false`) to exercise the `SizeLimitedStream`-wrapping path against a
  real provider.
- Fixtures are `IDisposable`, but the two sharing styles both appear, on different test classes, for the *same*
  fixture types: the cross-provider validation and stream tests (`BlobServiceSaveAsyncTests`,
  `BlobServiceStreamTests`) construct a fresh `using var fixture = new XxxBlobServiceFixture()` per test method,
  while the per-provider happy-path classes (`LocalBlobStorageTest`, `S3BlobServiceTest`,
  `AzureStorageBlobServiceTest`) instead take the same fixture type through `IClassFixture<XxxBlobServiceFixture>`,
  sharing one instance across every `[Fact]` in that class. Don't assume every test in this project gets an
  isolated fixture instance — check which base pattern the specific test class uses.
- No test project references this Abstractions package standalone without also referencing the three provider
  packages — a consumer testing only against the abstraction would typically write its own fake `IBlobService` or
  `BlobService` subclass (see the `InMemoryBlobService` pattern above) rather than reuse anything from this test
  project.
