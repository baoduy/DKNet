# DKNet.Svc.BlobStorage.Abstractions

Provider-agnostic blob storage contract — application code depends on `IBlobService` and the shared model types, and a
concrete adapter package supplies the implementation.

> [!IMPORTANT]
> All symbols on this page are current against `src/Services/DKNet.Svc.BlobStorage.Abstractions` on `dev`. Verify against
> that source before relying on a signature — earlier revisions of this page described an API that never existed.

## ✨ Why use it?

- **Swapping storage backends is a DI change, not a code change.** Business logic takes `IBlobService`; the composition
  root decides whether that is [AWS S3](./DKNet.Svc.BlobStorage.AwsS3.md),
  [Azure Storage](./DKNet.Svc.BlobStorage.AzureStorage.md), or the
  [local filesystem](./DKNet.Svc.BlobStorage.Local.md).
- **One payload type for every size of file.** Everything moves as `BinaryData`, so a 2 KB text blob and a 2 GB stream
  use the same call — no `byte[]`-versus-`Stream` overload matrix to choose between.
- **Save-time guard rails are shared, not re-implemented per provider.** Extension allow-list, name length, and size
  limits live in `BlobServiceOptions` and run inside the base class every provider derives from.
- **Content type is derived, not demanded.** `BlobDetails.BlobData` fills `ContentType` from the file extension, so
  callers that don't care never set it.

Reach for this package (plus exactly one provider package) whenever the application stores or retrieves files —
uploaded documents, generated reports, exported PDFs — and you don't want Azure, AWS, or `System.IO` types leaking into
domain code.

## 🚀 Quick Start

```bash
dotnet add package DKNet.Svc.BlobStorage.Abstractions
```

This package ships no `IBlobService` implementation. Register one provider package (Local shown here) to complete the
wiring:

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

## 🧩 Features

### `IBlobService` — the unified contract

Every provider implements the same seven operations:

| Method | Signature | Notes |
|---|---|---|
| Save | `Task<string> SaveAsync(BlobDetails.BlobData blob, CancellationToken ct = default)` | Returns the stored blob's location. Validates against `BlobServiceOptions` first (see below). |
| Get | `Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest blob, CancellationToken ct = default)` | Fetches content + metadata. **Only S3 returns `null` when the blob is missing — Local throws `FileNotFoundException` and Azure throws the SDK's `RequestFailedException` (`404`)** — code against the abstraction defensively if you support more than one provider. |
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

### Writing your own provider

The extension surface is deliberately small and all of it is public:

| Member | Accessibility | What you do with it |
|---|---|---|
| `IBlobService` | `public interface` | Implement it directly when you want no shared behaviour at all. |
| `BlobService` | `public abstract class` | The usual base — derive from it and implement the six abstract members. |
| `BlobService.ValidateFile(BlobData)` | `protected virtual` | Call it at the top of your `SaveAsync`; override it to add rules. |
| `BlobService.GetBlobLocation(BlobRequest)` | `protected virtual` | Override to change how a name becomes a provider path. |
| `BlobService.GetItemAsync(...)` | `public virtual` | Override when your store can fetch metadata without a full listing. |
| `BlobServiceOptions` | `public class` | Derive from it so your provider's options inherit the shared validation. |

```csharp
public sealed class InMemoryBlobService(BlobServiceOptions options) : BlobService(options)
{
    private readonly Dictionary<string, BinaryData> _blobs = new(StringComparer.Ordinal);

    public override Task<string> SaveAsync(BlobDetails.BlobData blob, CancellationToken ct = default)
    {
        ValidateFile(blob);                       // shared rules first
        _blobs[GetBlobLocation(blob)] = blob.Data; // normalized "/name"
        return Task.FromResult(blob.Name);
    }

    // CheckExistsAsync, DeleteAsync, GetAsync, GetPublicAccessUrl and ListItemsAsync
    // are abstract too — every provider has to answer all six.
}
```

Nothing else in the package is an extension point: there is no provider registry, no factory
interface, and no way to intercept a call to an existing provider. Composition is DI ordering — register
the `IBlobService` you want resolved last.

### Save-time validation

`BlobService.ValidateFile` runs on every `SaveAsync` call and throws `FileLoadException` (`"File name is invalid."`,
`"File extension is invalid."`, `"File size is invalid."`) when a `BlobServiceOptions` rule is violated. Each rule is
gated on its own option being set — see the reference below.

## ⚙️ Configuration reference

Every provider's own options type (`S3Options`, `AzureStorageOptions`, `LocalDirectoryOptions`) extends this base, so
validation behaves identically regardless of backend:

| Option | Type | Default | Effect |
|---|---|---|---|
| `IncludedExtensions` | `IEnumerable<string>` | `[]` (empty) | No extension filtering when empty; when non-empty, only listed extensions pass `SaveAsync` (compared case-insensitively, leading dot included). |
| `MaxFileNameLength` | `int` | `0` | `0` disables the check — **not** "zero-length names rejected". |
| `MaxFileSizeInMb` | `int` | `0` | `0` disables the check — there is no built-in size cap unless you set one. |

All three checks are opt-in; do not assume a default limit exists (older revisions of this page claimed a 50MB
default — there is none).

## 🧱 Where it fits

Every `SaveAsync` runs the same two shared steps before a provider ever touches its SDK, and only the
last step differs per backend:

![Workflow diagram: SaveAsync runs ValidateFile and GetBlobLocation in the shared BlobService base class, throws FileLoadException when a BlobServiceOptions rule is violated, and otherwise hands the normalized name to the provider override, which writes to its backing store and returns the location.](../diagrams/svc-blobstorage-abstractions-save-path.svg)

- **Provider adapters** — [AwsS3](./DKNet.Svc.BlobStorage.AwsS3.md),
  [AzureStorage](./DKNet.Svc.BlobStorage.AzureStorage.md), and [Local](./DKNet.Svc.BlobStorage.Local.md) derive from the
  `BlobService` base class in this package and are the only components that reference a storage SDK.
- **`DKNet.EfCore.Events`** — raise a domain event after `SaveAsync` returns the stored location so a handler can
  attach it to an aggregate.
- **`DKNet.EfCore.Repos`** — store the returned location string as a value on your entity; the repository layer never
  needs to know which blob provider is in use.
- **`DKNet.Fw.Extensions`** — general-purpose extensions used incidentally by the storage adapters; no hard dependency
  from your own code.

## ⚠️ Gotchas & limits

- **No `byte[]` API.** Wrap arrays with `BinaryData.FromBytes(...)` if you have one.
- **Inconsistent miss behavior.** `GetAsync` returns `null` on S3, throws `FileNotFoundException` on Local, and throws
  `Azure.RequestFailedException` (`404`) on Azure Storage — an abstraction leak worth a defensive `try/catch` or a
  provider-neutral wrapper if you need one behavior everywhere.
- **`MaxFileSizeInMb` counts decimal megabytes.** The limit is `MaxFileSizeInMb * 1_000_000` bytes, not `* 1024 * 1024` —
  a "10 MB" cap rejects at 10,000,000 bytes.
- **`GetItemAsync`'s default implementation reads the whole listing** to find the first match unless a provider
  overrides it — for a single known file, prefer `GetAsync`/`CheckExistsAsync` over `GetItemAsync` when you don't
  actually need metadata-only semantics.
- **Public URL support is not universal.** Always check the target provider's page before relying on
  `GetPublicAccessUrl` — Local never supports it.
- **Configuration section keys are not uniform across providers.** Local binds `BlobStorage:LocalFolder`, S3 binds
  `BlobService:S3`, Azure binds `BlobService:AzureStorage` — check each provider page rather than assuming one prefix.

## 🔗 Related packages

- [DKNet.Svc.BlobStorage.AwsS3](./DKNet.Svc.BlobStorage.AwsS3.md) – reach for it when blobs live in AWS S3 or an
  S3-compatible store (MinIO, Cloudflare R2).
- [DKNet.Svc.BlobStorage.AzureStorage](./DKNet.Svc.BlobStorage.AzureStorage.md) – reach for it when blobs live in an
  Azure Storage account, especially with managed-identity auth.
- [DKNet.Svc.BlobStorage.Local](./DKNet.Svc.BlobStorage.Local.md) – reach for it in local development, tests, and CI
  where no cloud account should be needed.
- [DKNet.Svc.Encryption](./DKNet.Svc.Encryption.md) – reach for it when a blob's bytes must be encrypted before they
  reach the store.
