> [!IMPORTANT]
> All symbols on this page are current against `src/Services/DKNet.Svc.BlobStorage.Abstractions` on `dev`. Verify against
> that source before relying on a signature — earlier revisions of this page described an API that never existed.

# DKNet.Svc.BlobStorage.Abstractions

Provider-agnostic contract for blob storage. Application code depends on `IBlobService` and the shared model types;
a concrete adapter package — [AWS S3](./DKNet.Svc.BlobStorage.AwsS3.md), [Azure Storage](./DKNet.Svc.BlobStorage.AzureStorage.md),
or the [local filesystem](./DKNet.Svc.BlobStorage.Local.md) — supplies the implementation. Swapping providers is a DI
change, not a code change.

## When to reach for it

Reach for this package (and one provider package) whenever your application needs to store or retrieve files —
uploaded documents, generated reports, exported PDFs — without hard-coupling business logic to Azure, AWS, or the
local disk. Depend on `IBlobService` from your application/domain code; register the concrete provider only at the
composition root.

## Install and minimal wiring

```bash
dotnet add package DKNet.Svc.BlobStorage.Abstractions
```

`DKNet.Svc.BlobStorage.Abstractions` on its own has no `IBlobService` implementation — register one provider package
(shown here: Local) to complete the wiring:

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

// Program.cs
builder.Services.AddLocalDirectoryBlobService(builder.Configuration);
```

```csharp
public sealed class DocumentService(IBlobService blobService)
{
    public async Task<string> UploadAsync(string fileName, Stream content, CancellationToken ct)
    {
        var blob = new BlobDetails.BlobData(fileName, BinaryData.FromStream(content));
        return await blobService.SaveAsync(blob, ct); // returns the stored location
    }
}
```

## Features

### `IBlobService` — the unified contract

Every provider implements the same seven operations:

| Method | Signature | Notes |
|---|---|---|
| Save | `Task<string> SaveAsync(BlobDetails.BlobData blob, CancellationToken ct = default)` | Returns the stored blob's location. Validates against `BlobServiceOptions` first (see below). |
| Get | `Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest blob, CancellationToken ct = default)` | Fetches content + metadata. **S3 and Azure return `null` when the blob is missing; the Local provider throws `FileNotFoundException` instead** — code against the abstraction defensively if you support multiple providers. |
| Get metadata only | `Task<BlobDetails.BlobResult?> GetItemAsync(BlobRequest blob, CancellationToken ct = default)` | No content transferred. The `BlobService` base class's default implementation is "first item from `ListItemsAsync`" — cheap for a single file, wasteful for a directory. |
| Exists | `Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken ct = default)` | |
| Delete | `Task<bool> DeleteAsync(BlobRequest blob, CancellationToken ct = default)` | Providers that support folders delete recursively when `blob.Type == BlobTypes.Directory`. |
| List | `IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(BlobRequest blob, CancellationToken ct = default)` | Streams results — safe for large containers/folders. |
| Public URL | `Task<Uri> GetPublicAccessUrl(BlobRequest blob, TimeSpan? expiresFromNow = null, CancellationToken ct = default)` | Support and expiry semantics are provider-specific; the Local provider always throws `NotSupportedException`. |

There is **no `byte[]` overload and no separate "streaming" method** — every payload is a `BinaryData`
(`BlobDetails.BlobData`/`BlobDataResult.Data`), which itself wraps either bytes or a stream (`BinaryData.FromStream(...)`,
`BinaryData.FromString(...)`). Pass a `Stream` through `BinaryData.FromStream` for large files instead of buffering to
`byte[]` first.

### Naming and path rules

- `BlobRequest(string Name)` auto-derives `Type` (`BlobTypes.File` or `BlobTypes.Directory`) from whether `Name` has a
  file extension (`Path.GetExtension`) — a name ending in `/` or with no extension is treated as a directory.
- The `BlobService` base class's `GetBlobLocation` normalizes every name to start with `/` before handing it to a
  provider — this is the only naming rule enforced by the abstraction itself; path-traversal protection and
  provider-native prefix rules are provider-specific (documented on each provider's page).

### Models

- `BlobRequest(string Name)` — the base request shape; every other request/result type derives from it.
- `BlobDetails { CreatedOn, LastModified, ContentLength, required ContentType }` — metadata, nested inside:
  - `BlobDetails.BlobResult(string Name) : BlobRequest(Name)` — adds `Details` (`null` until fetched via `GetItemAsync`/`GetAsync`).
  - `BlobDetails.BlobDataResult(string Name, BinaryData Data) : BlobResult(Name)` — the `GetAsync` return shape (content + metadata).
  - `BlobDetails.BlobData(string Name, BinaryData Data) : BlobRequest(Name)` — the `SaveAsync` input shape; `Overwrite`
    (`bool`, default `false`) and `ContentType` (defaults to `Name.GetContentTypeByExtension()`) are settable via
    object/`with` syntax on top of the positional `Name`/`Data`.
- `BlobTypes { File, Directory }`.

### Content-type detection

`fileName.GetContentTypeByExtension()` maps ~20 known extensions to their MIME type and falls back to
`"application/octet-stream"` for anything else. It throws `NullReferenceException` for a `null` input — always pass a
non-null file name.

### Validation

`BlobService.ValidateFile` runs on every `SaveAsync` call and throws `FileLoadException` (`"File name is invalid."`,
`"File extension is invalid."`, `"File size is invalid."`) when a `BlobServiceOptions` rule is violated. See
Configuration below for how each rule is gated.

## Configuration — `BlobServiceOptions`

Every provider's own options type (`S3Options`, `AzureStorageOptions`, `LocalDirectoryOptions`) extends this base, so
validation behaves identically regardless of backend:

| Property | Default | Effect |
|---|---|---|
| `IEnumerable<string> IncludedExtensions` | `[]` (empty) | No extension filtering when empty; when non-empty, only listed extensions pass `SaveAsync`. |
| `int MaxFileNameLength` | `0` | `0` disables the check — **not** "zero-length names rejected". |
| `int MaxFileSizeInMb` | `0` | `0` disables the check — there is no built-in size cap unless you set one. |

All three checks are opt-in; do not assume a default limit exists (older revisions of this page claimed a 50MB
default — there is none).

## Composing with other DKNet packages

- **`DKNet.EfCore.Events`** — raise a domain event after `SaveAsync` returns the stored location so a handler can
  attach it to an aggregate.
- **`DKNet.EfCore.Repos`** — store the returned location string as a value on your entity; the repository layer never
  needs to know which blob provider is in use.
- **`DKNet.Fw.Extensions`** — general-purpose extensions used incidentally by the storage adapters; no hard dependency
  from your own code.

## Gotchas and limits

- **No `byte[]` API.** Wrap arrays with `BinaryData.FromBytes(...)` if you have one.
- **Inconsistent miss behavior.** `GetAsync` returns `null` on S3/Azure, throws `FileNotFoundException` on Local — an
  abstraction leak worth a defensive `try/catch` or a provider-neutral wrapper if you need one behavior everywhere.
- **`GetItemAsync`'s default implementation reads the whole listing** to find the first match unless a provider
  overrides it — for a single known file, prefer `GetAsync`/`CheckExistsAsync` over `GetItemAsync` when you don't
  actually need metadata-only semantics.
- **Public URL support is not universal.** Always check the target provider's page before relying on
  `GetPublicAccessUrl` — Local never supports it.
