# DKNet.Svc.BlobStorage.AzureStorage

| Field | Value |
|---|---|
| Area | Services |
| NuGet | `dotnet add package DKNet.Svc.BlobStorage.AzureStorage` |
| Docs | [DKNet.Svc.BlobStorage.AzureStorage.md](https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.BlobStorage.AzureStorage.md) |
| Source | [src/Services/DKNet.Svc.BlobStorage.AzureStorage](https://github.com/baoduy/DKNet/tree/dev/src/Services/DKNet.Svc.BlobStorage.AzureStorage) |
| Depends on (DKNet) | `DKNet.Svc.BlobStorage.Abstractions` |
| Depends on (3rd party) | `Azure.Storage.Blobs`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Options` |
| Target framework | net10.0 |

## Purpose

The Azure Blob Storage implementation of `IBlobService`, backed by the `Azure.Storage.Blobs` SDK. It resolves a
`BlobContainerClient` once per service instance (from either a connection string or a caller-supplied factory
delegate), lazily creates the container on first use, and implements save/get/list/delete/exists/SAS-URL
operations against it, including client-side "folder" semantics over Azure's flat blob namespace.

It is **not** a general-purpose Azure SDK wrapper — it only implements the shared `IBlobService` surface, and
application code should depend on `IBlobService` from the Abstractions package; only the composition root (DI
registration) touches this package.

## Entry points

| Call | Signature | Notes |
|---|---|---|
| `AddAzureStorageAdapter` (config-bound) | `IServiceCollection AddAzureStorageAdapter(IConfiguration configuration)` — a C# 14 extension member on `IServiceCollection`, declared by static class `AzureStorageSetup` in the ambient `Microsoft.Extensions.DependencyInjection` namespace (call sites are unaffected: `services.AddAzureStorageAdapter(configuration)`) | Binds `AzureStorageOptions` from `configuration.GetSection(AzureStorageOptions.Name)` (`"BlobService:AzureStorage"`). Registers `IBlobService → AzureStorageBlobService` as **Singleton**, only if no such registration already exists. Idempotent to call twice. |
| `AddAzureStorageAdapter` (delegate) | `IServiceCollection AddAzureStorageAdapter(Action<AzureStorageOptions> config)` — same extension-member block | Builds a new `AzureStorageOptions`, invokes `config` on it, registers that populated instance as `services.AddSingleton(option)` (a concrete `AzureStorageOptions`, **not** `IOptions<AzureStorageOptions>`), then registers the same Singleton `IBlobService → AzureStorageBlobService`. Because `AzureStorageBlobService` consumes `IOptions<AzureStorageOptions>` and nothing here registers an `IConfigureOptions<AzureStorageOptions>`, the options the service actually resolves are a **fresh default** `AzureStorageOptions` — this overload's values never reach the service (see Gotchas). Prefer the `IConfiguration` overload plus `services.Configure<AzureStorageOptions>(...)`. |
| `AzureStorageBlobService` constructor | `AzureStorageBlobService(IOptions<AzureStorageOptions> options)` | Not needed in normal app code — DI resolves it. A null `options` *wrapper* throws `NullReferenceException` (the primary constructor's base-call argument dereferences it before this class's own field initializers run); when `options` is non-null but `options.Value` is null, the *base* `BlobService` constructor throws `ArgumentNullException` first. Neither path matters with normal DI resolution. |

## Public surface

### `DKNet.Svc.BlobStorage.AzureStorage`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `AzureStorageOptions` | class (extends `BlobServiceOptions`) | Azure-specific configuration: container, connection string or client factory. | `Func<AzureStorageOptions, Task<BlobServiceClient>>? BlobServiceClientFactory { get; set; }` (default `null`) · `string? ConnectionString { get; set; }` (default `null`) · `string ContainerName { get; set; } = null!` (required) · `static string Name => "BlobService:AzureStorage"` |
| `AzureStorageBlobService` | sealed class, extends `BlobService` | The provider itself. | `AzureStorageBlobService(IOptions<AzureStorageOptions> options)` · `override Task<bool> CheckExistsAsync(BlobRequest, CancellationToken = default)` · `override Task<bool> DeleteAsync(BlobRequest, CancellationToken = default)` · `override Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest, CancellationToken = default)` · `override Task<Stream?> OpenReadAsync(BlobRequest, CancellationToken = default)` (true streaming read, not buffered) · `override Task<Uri> GetPublicAccessUrl(BlobRequest, TimeSpan? expiresFromNow = null, CancellationToken = default)` · `override IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(BlobRequest, CancellationToken = default)` · `override Task<string> SaveAsync(BlobDetails.BlobData, CancellationToken = default)` · `override Task<string> SaveAsync(BlobDetails.BlobStreamData, CancellationToken = default)` (true streaming write, not buffered) · inherits `GetItemAsync` unchanged (returns the first `ListItemsAsync` result) |
| `AzureStorageSetup` | static class | Holds the `IServiceCollection` extension members. Deliberately declares the ambient `Microsoft.Extensions.DependencyInjection` namespace so consumers don't need an extra `using` to *call* it. | See Entry points. |
| `AzureStorageExtensions` | static class | Small path/blob helpers used internally. | `static string EnsureTrailingSlash(this string path)` · `static bool IsDirectory(this BlobItem blob)` (true when `ContentLength <= 0 && string.IsNullOrEmpty(ContentType)`) · `static string RemoveHeadingSlash(this string path)` |

`AzureStorageOptions`, `AzureStorageBlobService`, and `AzureStorageExtensions` all live in
`DKNet.Svc.BlobStorage.AzureStorage` — only `AzureStorageSetup` uses the ambient DI namespace, so referencing the
options type still needs `using DKNet.Svc.BlobStorage.AzureStorage;`.

## Options & defaults

| Option | Type | Default | Effect |
|---|---|---|---|
| `ContainerName` | `string` | *(required)* | Target container name; created if missing on first operation. |
| `ConnectionString` | `string?` | `null` | Storage account connection string used to build a `BlobServiceClient` **when `BlobServiceClientFactory` is not set**. |
| `BlobServiceClientFactory` | `Func<AzureStorageOptions, Task<BlobServiceClient>>?` | `null` | Delegate that builds the `BlobServiceClient` (e.g. with `DefaultAzureCredential`). Takes priority over `ConnectionString` when set. Cannot be bound from configuration (it's a delegate) — set it via `services.Configure<AzureStorageOptions>(o => o.BlobServiceClientFactory = ...)`. |
| `IncludedExtensions`, `MaxFileNameLength`, `MaxFileSizeInMb` | inherited | `[]` / `0` / `0` (opt-in) | Shared validation applied by `BlobService.ValidateFile` before every save. |
| `AzureStorageOptions.Name` (static) | `string` | `"BlobService:AzureStorage"` | The configuration section key `AddAzureStorageAdapter(IConfiguration)` binds from. |

Neither `ConnectionString` nor `BlobServiceClientFactory` set → the first operation against the service throws
`ArgumentException`.

## Usage patterns

### Register with a connection string (the common case)

**When**: standard app startup, connection string in configuration.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Threading;

// appsettings.json:
// { "BlobService": { "AzureStorage": { "ConnectionString": "...", "ContainerName": "documents" } } }

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAzureStorageAdapter(builder.Configuration);
var app = builder.Build();

app.MapPost("/reports", async (IBlobService blobService, Stream body, CancellationToken ct) =>
{
    var location = await blobService.SaveAsync(
        new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(body)),
        ct);
    return Results.Ok(location);
});

app.Run();
```

**Notes**: `AddAzureStorageAdapter(IConfiguration)` is idempotent — calling it twice does not duplicate the
`IBlobService` registration. `IBlobService` is registered as a Singleton, so anything the DI-resolved
`AzureStorageBlobService` itself needs must also be safe as a long-lived singleton.

### Managed identity via `BlobServiceClientFactory`

**When**: no connection string / account key is allowed; auth via Azure AD / managed identity.

```csharp
using Azure.Identity;
using Azure.Storage.Blobs;
using DKNet.Svc.BlobStorage.AzureStorage;

var builder = WebApplication.CreateBuilder();
builder.Services.AddAzureStorageAdapter(builder.Configuration); // still binds ContainerName etc.
builder.Services.Configure<AzureStorageOptions>(options =>
{
    options.ContainerName = "documents";
    options.BlobServiceClientFactory = _ => Task.FromResult(
        new BlobServiceClient(
            new Uri("https://myaccount.blob.core.windows.net"),
            new DefaultAzureCredential()));
});
```

**Notes**: `BlobServiceClientFactory` takes priority over `ConnectionString` whenever it is set. It runs at most
once per `AzureStorageBlobService` instance — the built `BlobContainerClient` is cached behind a
`Lazy<Task<BlobContainerClient>>`, so concurrent first callers all await the same build instead of racing separate
`CreateIfNotExistsAsync` calls. A client built this way (token credential) generally cannot sign SAS URLs — see
`GetPublicAccessUrl` below. Use the `IConfiguration` overload plus `Configure<AzureStorageOptions>`, never the
`Action<AzureStorageOptions>` overload of `AddAzureStorageAdapter`, for this pattern (see Gotchas). This
package's own dependency list does not include `Azure.Identity`, but do not add that package yourself: the
`Azure.Storage.Blobs` dependency already pulls in an `Azure.Core` version that re-exports the whole
`Azure.Identity` credential surface (`DefaultAzureCredential` included) under the same namespace, so
`using Azure.Identity;` alone already resolves it (see Gotchas for what goes wrong if you add the package anyway).

### Save with explicit overwrite

**When**: writing a blob that may already exist and should be replaced.

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

var blob = new BlobDetails.BlobData("reports/2026/q1.pdf", BinaryData.FromString("..."))
{
    Overwrite = true
};
var location = await blobService.SaveAsync(blob, CancellationToken.None); // "reports/2026/q1.pdf"
```

**Notes**: `Overwrite` defaults to `false`. With `Overwrite = false` against an existing blob, this provider
throws no DKNet-specific `InvalidOperationException` — the Azure SDK's `UploadAsync` surfaces
`RequestFailedException` with `Status == 409` ("BlobAlreadyExists") directly. Unlike the Local/AwsS3 providers, do
not expect a DKNet-specific duplicate-file exception here.

### Stream-based save (no full in-memory buffering)

**When**: uploading a large payload without holding the whole thing in memory as `BinaryData`.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AzureStorage;
using System.IO;
using System.Threading;

var services = new ServiceCollection();
services.AddAzureStorageAdapter(new ConfigurationBuilder().Build());
services.Configure<AzureStorageOptions>(o =>
{
    o.ConnectionString = "UseDevelopmentStorage=true";
    o.ContainerName = "documents";
});
var blobService = services.BuildServiceProvider().GetRequiredService<IBlobService>();

await using var fileStream = File.OpenRead("large-export.csv");
var name = await blobService.SaveAsync(
    new BlobDetails.BlobStreamData("exports/large-export.csv", fileStream) { ContentType = "text/csv" },
    CancellationToken.None);
```

**Notes**: `AzureStorageBlobService` overrides `SaveAsync(BlobDetails.BlobStreamData, ...)` to upload directly from
the stream (via `BlobClient.UploadAsync`) instead of using the Abstractions default, which buffers the whole
payload into `BinaryData` first. `MaxFileSizeInMb` is still enforced — for a non-seekable stream it's enforced
while reading via `SizeLimitedStream`, not upfront.

### Read-only SAS URL with a custom expiry

**When**: sharing a time-limited, read-only link to a single blob.

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

**Notes**: `expiresFromNow` defaults to `TimeSpan.FromDays(1)` when omitted. Throws `NotSupportedException` when
the underlying `BlobContainerClient.CanGenerateSasUri` is `false` — a client built from a token credential
(managed identity) cannot sign a SAS; an account-key/connection-string client can.

### Recursive folder delete

**When**: removing an entire "folder" (prefix) and everything nested under it.

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

var deleted = await blobService.DeleteAsync(
    new BlobRequest("reports/2025") { Type = BlobTypes.Directory },
    CancellationToken.None);
```

**Notes**: `BlobRequest.Type` defaults to `Directory` automatically whenever `Name` has no file extension, so the
explicit `Type = BlobTypes.Directory` above is only needed to be unambiguous. Deletion walks the prefix
breadth-first, deletes every nested blob it finds, and removes folder markers from the deepest level upward.

## Runtime behaviour

1. The first call to any `AzureStorageBlobService` method builds the container client exactly once
   (thread-safe via `LazyThreadSafetyMode.ExecutionAndPublication`, even across concurrent first callers).
2. The build step creates a `BlobServiceClient` — from `options.BlobServiceClientFactory` if set, otherwise from
   `options.ConnectionString` — or throws `ArgumentException` if neither is set.
3. It then gets the container client for `options.ContainerName` and calls `CreateIfNotExistsAsync()`, so the
   identity in use needs container-create rights unless the container already exists.
4. Every subsequent call on that same `AzureStorageBlobService` instance reuses the same cached container client —
   no repeated `CreateIfNotExists` round trip.
5. `SaveAsync(BlobData)` delegates to `SaveAsync(BlobStreamData)` by wrapping `BinaryData` in a stream; both paths
   call `ValidateFile` (inherited from `BlobService`) before touching Azure, then `BlobClient.UploadAsync(stream,
   overwrite, cancellationToken)`.
6. `GetAsync` calls `BlobClient.DownloadContentAsync`, and on a `RequestFailedException` with `Status == 404`
   catches it and returns `null` rather than propagating.
7. `DeleteAsync` on a `BlobTypes.File` request calls `DeleteIfExistsAsync` directly. On a `BlobTypes.Directory`
   request it enumerates blobs breadth-first with a queue of prefixes to explore and a stack of explored prefixes,
   deletes every non-directory blob as it's found, then pops the stack (deepest level first) deleting each folder
   marker.
8. `GetPublicAccessUrl` checks `CanGenerateSasUri` first, then builds a read-only `BlobSasBuilder` (`Resource =
   "b"`, starting now, expiring after `expiresFromNow ?? TimeSpan.FromDays(1)`) and generates the SAS URI.
9. `ListItemsAsync` streams the container's blob listing and yields one `BlobDetails.BlobResult` per blob item,
   using the blob's own name and populating `Details` from its properties unless the directory heuristic says it's
   a folder marker, in which case `Details` is `null`.

## Diagnostics & exceptions

| Exception | When | Fix |
|---|---|---|
| `ArgumentException` | Neither `ConnectionString` nor `BlobServiceClientFactory` is set on `AzureStorageOptions` at first use. | Set one of the two — usually via `AddAzureStorageAdapter(IConfiguration)` binding `ConnectionString`, or `Configure<AzureStorageOptions>` for `BlobServiceClientFactory`. |
| `ArgumentNullException` | `options` is non-null but `options.Value` is null — thrown by the *base* `BlobService` constructor. A null `options` *wrapper* instead throws `NullReferenceException`. | Not reachable via normal DI resolution; only relevant when constructing `AzureStorageBlobService` manually with a broken `IOptions<AzureStorageOptions>`. |
| `NotSupportedException` | `GetPublicAccessUrl` called against a client where `CanGenerateSasUri == false` (typically a token-credential/managed-identity client). | Use an account-key or connection-string-backed client for SAS generation, or don't call `GetPublicAccessUrl` with a managed-identity client. |
| `RequestFailedException` (Azure SDK, unwrapped) | `SaveAsync` with `Overwrite = false` against an existing blob name → `Status == 409` ("BlobAlreadyExists"). Any other Azure Storage failure also surfaces this type directly. | Set `Overwrite = true` for intentional replace, or check `CheckExistsAsync` first; `404` on `GetAsync` is caught internally (see below) and does not propagate. |
| `FileLoadException` (shared, from Abstractions) | Name/extension/size validation fails against configured `IncludedExtensions`, `MaxFileNameLength`, or `MaxFileSizeInMb`. | Adjust the file or the configured `BlobServiceOptions` limits. |

No analyzer/`DiagnosticDescriptor` surface — this package ships runtime code only.

## Gotchas

- **Registration lifetime is Singleton, not Scoped.** Both `AddAzureStorageAdapter` overloads register
  `IBlobService → AzureStorageBlobService` as a singleton. If code inside `BlobServiceClientFactory` or anything
  else captured by the options depends on a scoped service, that dependency is effectively pinned to whatever was
  resolved for the first caller.
- **`GetAsync` returns `null` on a missing blob — it does not throw.** The 404 from `DownloadContentAsync` is
  caught and converted to `null`.
- **`ListItemsAsync` returns each blob's own name**, not the requested prefix repeated for every item.
- **The `Action<AzureStorageOptions>` overload silently drops its values.** It registers the populated instance as
  a singleton `AzureStorageOptions`, but `AzureStorageBlobService` consumes `IOptions<AzureStorageOptions>` — with
  no `IConfigureOptions<AzureStorageOptions>` registered, that resolves to a fresh default instance, so
  `ContainerName` ends up `null` and the first call throws `ArgumentException`. Use the `IConfiguration` overload
  plus `services.Configure<AzureStorageOptions>(...)` for anything configuration can't bind.
- **`BlobServiceClientFactory` cannot be bound from configuration** — it's a `Func<...>` delegate, so
  `IConfiguration` binding silently leaves it `null`. It must be set via `services.Configure<AzureStorageOptions>`
  in code.
- **Folder detection is a heuristic, not a real Azure concept.** `IsDirectory()` treats any blob with
  `ContentLength <= 0` and empty `ContentType` as a folder marker — a genuine zero-length blob with no content
  type is indistinguishable from a folder and will be swept up by `DeleteAsync`'s recursive folder path.
- **The container client build (and `CreateIfNotExistsAsync`) runs at most once per `AzureStorageBlobService`
  instance, race-safe even under concurrent first callers.** Since the DI registration is Singleton, that means
  once for the app's whole lifetime in practice — don't expect `BlobServiceClientFactory` to run again if you
  rotate credentials at runtime.
- **Multiple `IBlobService` registrations don't evict each other.** Nothing removes a prior `IBlobService`
  registration from another provider (e.g. `Local` or `AwsS3`); the last registration wins when a single
  `IBlobService` is resolved.
- **No blob-name-length or upload-size cap beyond the shared, opt-in `BlobServiceOptions` checks.**
  `MaxFileNameLength` and `MaxFileSizeInMb` both default to `0`, which disables the check entirely — nothing
  Azure-specific caps these.
- **This package's `.csproj` does not reference `Azure.Identity`.** Its third-party dependencies are
  `Azure.Storage.Blobs`, `Microsoft.Extensions.Configuration.Binder`, and `Microsoft.Extensions.Options` only.
  That's not a gap to fill by adding the `Azure.Identity` package yourself, though: `Azure.Storage.Blobs`
  transitively pulls in an `Azure.Core` version that already re-exports the entire `Azure.Identity` credential
  surface (`DefaultAzureCredential`, `ManagedIdentityCredential`, `ClientSecretCredential`, etc.) under the
  identical `Azure.Identity` namespace — `using Azure.Identity;` in the consuming project resolves `DefaultAzureCredential`
  from `Azure.Core` with no extra package. Explicitly running `dotnet add package Azure.Identity` on top adds a
  second, separately-versioned copy of the same type name and breaks the build with `error CS0433: The type
  'DefaultAzureCredential' exists in both 'Azure.Core...' and 'Azure.Identity...'`. If some other dependency
  really needs a credential type that `Azure.Core` doesn't carry, resolve the clash with an `extern alias`
  rather than assuming the plain package add is safe.

## Anti-patterns & hallucination traps

- There is **no** `AzureStorageOptions.UseManagedIdentity` flag or similar boolean — managed identity is expressed
  purely by setting `BlobServiceClientFactory` and leaving `ConnectionString` unset. Don't invent a flag property.
- Don't expect `SaveAsync` with `Overwrite = false` against an existing blob to throw a DKNet-specific
  `InvalidOperationException` — that's how the Local/AwsS3 providers behave, but this provider lets the SDK's
  `RequestFailedException` (409) surface unwrapped.
- Don't call `services.AddScoped<IBlobService, AzureStorageBlobService>()` yourself expecting to override the
  registration lifetime — `AddAzureStorageAdapter` always registers Singleton, and calling it after your own
  registration does nothing extra (its dedup guard only prevents *its own* double-registration; it does not remove
  yours, so you'd end up with two `IBlobService` registrations and the last one wins).
- Don't hand-roll container creation before calling `SaveAsync`/`GetAsync` — `CreateIfNotExistsAsync` already runs
  automatically on first use.
- There is no synchronous/blocking API surface (no `.Save(...)`, no `.Result`/`.Wait()` usage inside the package) —
  every public member is `async`/`Task`-returning; don't add blocking wrappers.
- `AzureStorageOptions` does not live under `Microsoft.Extensions.DependencyInjection` — only `AzureStorageSetup`
  (the DI extension holder) uses that ambient namespace. `AzureStorageOptions`, `AzureStorageBlobService`, and
  `AzureStorageExtensions` are all in `DKNet.Svc.BlobStorage.AzureStorage`.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.Svc.BlobStorage.Abstractions` | Always the dependency application code references directly (`IBlobService`, `BlobRequest`, `BlobDetails.*`, `BlobServiceOptions`). This package's types (`AzureStorageOptions`, `AzureStorageBlobService`) are only touched at the composition root. |
| `DKNet.Svc.BlobStorage.Local` | Swap in for local dev/CI runs where no real storage account or emulator (Azurite) should be required — same `IBlobService` contract. |
| `DKNet.Svc.BlobStorage.AwsS3` | Reach for it instead of this package when blobs live in AWS S3 or an S3-compatible store. |

## Testing notes

- Tests live in the shared blob-storage test project, alongside the other providers.
- The fixture spins up a **TestContainers Azurite** container (`--skipApiVersionCheck`), builds a real
  `IServiceCollection`/`AddAzureStorageAdapter(config)` pipeline against an in-memory `IConfiguration`, and exposes
  both the resolved `IBlobService` and the raw `AzureStorageOptions` it used (so tests can build a second,
  manually-configured `AzureStorageBlobService` directly).
- Tests share the Azurite container per test class (not per-test isolation) and use GUID-suffixed blob names to
  avoid collisions within it.
- A concurrent-first-callers test builds its own counting `BlobServiceClientFactory`, wraps it in a fresh
  `AzureStorageOptions` passed to a manually-constructed `AzureStorageBlobService`, fires many concurrent
  `CheckExistsAsync` calls, and asserts the factory ran exactly once — the pattern for testing the lazy client-build
  path.
- No mocking of `BlobServiceClient`/`BlobContainerClient` — all tests exercise the real Azurite container, matching
  the repo-wide "avoid mocking the store" convention.
