# DKNet.Svc.BlobStorage.Local

| Field | Value |
|---|---|
| Area | Services |
| NuGet | `dotnet add package DKNet.Svc.BlobStorage.Local` |
| Docs | [DKNet.Svc.BlobStorage.Local.md](https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.BlobStorage.Local.md) |
| Source | [src/Services/DKNet.Svc.BlobStorage.Local](https://github.com/baoduy/DKNet/tree/dev/src/Services/DKNet.Svc.BlobStorage.Local) |
| Depends on (DKNet) | `DKNet.Svc.BlobStorage.Abstractions` |
| Depends on (3rd party) | `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`, `System.Memory.Data` |
| Target framework | net10.0 |

## Purpose

The filesystem-backed implementation of `IBlobService`: it stores every blob as a file under one configured root
folder, resolving and re-checking every request path against that root before touching disk so a caller-supplied
`../` cannot escape it. It exists so application code written against `IBlobService` can run — in local dev, unit
tests, and CI, or in a genuine single-instance production deployment — with nothing to provision and nothing to
clean up but a directory; swapping to S3 or Azure Storage for a real multi-instance deployment is a one-line DI
change, not a code change.

It is **not** a distributed or multi-instance store: there is no locking or coordination, so two processes (or two
instances of the same app) writing the same path race on the filesystem. Do not use it as shared storage behind a
load balancer.

## Entry points

| Call | Signature | Notes |
|---|---|---|
| `AddLocalDirectoryBlobService` | `public static IServiceCollection AddLocalDirectoryBlobService(this IServiceCollection services, IConfiguration configuration)` — ambient `Microsoft.Extensions.DependencyInjection` namespace | Binds `LocalDirectoryOptions` from the section named by `LocalDirectoryOptions.Name` (`"BlobStorage:LocalFolder"` — note the `BlobStorage:` prefix; the cloud providers use `BlobService:`). Registers `IBlobService → LocalBlobService` as **Scoped**, only if no existing registration already targets `LocalBlobService` — idempotent to call twice, but it does not guard against a *different* `IBlobService` implementation (S3/Azure) being registered first; DKNet's providers are designed to coexist as distinct registrations of the same interface. Returns the same `IServiceCollection` for chaining. |
| `IsDirectory` | `public static bool IsDirectory(this string path)` — also in the ambient `Microsoft.Extensions.DependencyInjection` namespace (colocated with the setup class, not a filesystem-specific namespace) | Thin wrapper over `Directory.Exists(path)`. Never throws — returns `false` for a missing path, a missing intermediate segment, or a path containing invalid characters. `LocalBlobService.ListItemsAsync` uses it to decide whether to enumerate a directory or treat the resolved path as a single file. |

## Public surface

### `DKNet.Svc.BlobStorage.Local`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `LocalBlobService` | class (not `sealed` — open for inheritance) | Filesystem `IBlobService` implementation; derives from `BlobService`. | `LocalBlobService(IOptions<LocalDirectoryOptions> options, ILogger<LocalBlobService> logger)` · `override Task<bool> CheckExistsAsync(BlobRequest, CancellationToken = default)` · `override Task<bool> DeleteAsync(BlobRequest, CancellationToken = default)` · `override Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest, CancellationToken = default)` · `override Task<Stream?> OpenReadAsync(BlobRequest, CancellationToken = default)` · `override Task<Uri> GetPublicAccessUrl(BlobRequest, TimeSpan? expiresFromNow = null, CancellationToken = default)` (always throws `NotSupportedException`) · `override IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(BlobRequest, CancellationToken = default)` · `override Task<string> SaveAsync(BlobDetails.BlobData, CancellationToken = default)` · `override Task<string> SaveAsync(BlobDetails.BlobStreamData, CancellationToken = default)`. Does **not** override `GetItemAsync` — consumers get the inherited default (calls `ListItemsAsync`, returns the first result). |
| `LocalDirectoryOptions` | class (extends `BlobServiceOptions`) | Binds the `"BlobStorage:LocalFolder"` section. | `static string Name => "BlobStorage:LocalFolder"` · `string? RootFolder { get; set; }` (nullable; a runtime fallback applies when unset — see Options & defaults). Inherits `IncludedExtensions`, `MaxFileNameLength`, `MaxFileSizeInMb`. |
| `LocalDirectorySetup` (ambient `Microsoft.Extensions.DependencyInjection`) | static class | DI registration extension + the `IsDirectory` filesystem helper. | See Entry points. |

## Options & defaults

| Option | Type | Default | Effect |
|---|---|---|---|
| `RootFolder` | `string?` | `null` → resolved at construction time to `$"{Directory.GetCurrentDirectory()}/LocalStore"` | Base directory for every path this provider touches; also the boundary the path-traversal guard enforces. |
| `LocalDirectoryOptions.Name` (static) | `string` | `"BlobStorage:LocalFolder"` | The configuration section key `AddLocalDirectoryBlobService` binds from. |
| `IncludedExtensions` (inherited) | `IReadOnlyList<string>` | `[]` (no restriction) | When non-empty, `SaveAsync` throws `FileLoadException("File extension is invalid.")` for a disallowed extension. |
| `MaxFileNameLength` (inherited) | `int` | `0` (disabled) | When `> 0`, `SaveAsync` throws `FileLoadException("File name is invalid.")` for a longer name. |
| `MaxFileSizeInMb` (inherited) | `int` | `0` (disabled) | When `> 0`, `SaveAsync` throws `FileLoadException("File size is invalid.")` for an oversized payload; a non-seekable stream is wrapped in `SizeLimitedStream` to enforce the ceiling while copying. |
| Per-call `Overwrite` | `bool` (on `BlobData`/`BlobStreamData`) | `false` | Saving to an existing path without `Overwrite = true` throws `InvalidOperationException("File already existed")`. |

## Usage patterns

### Register the provider and save a blob

**When**: standard app startup — bind `RootFolder` from configuration and inject `IBlobService`.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json: { "BlobStorage": { "LocalFolder": { "RootFolder": "/var/app/storage" } } }
builder.Services.AddLocalDirectoryBlobService(builder.Configuration);

var app = builder.Build();

app.MapPost("/reports", async (IBlobService blobService, Stream pdf, CancellationToken ct) =>
{
    var location = await blobService.SaveAsync(
        new BlobDetails.BlobData("reports/monthly.pdf", BinaryData.FromStream(pdf)) { Overwrite = true },
        ct);
    return Results.Ok(location);
});

app.Run();
```

**Notes**: `RootFolder` left unset resolves to `{CurrentDirectory}/LocalStore` at construction time — fine for a
quick local run, but always set it explicitly for anything deployed, because the process working directory
differs between `dotnet run`, a published binary, and a container.

### Reading a blob and handling the provider-specific "missing" signal

**When**: code that must run unmodified against Local, S3, and Azure Storage has to special-case each provider's
miss behavior, because `GetAsync` throws on this provider instead of returning `null`.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Threading;

public sealed class ReportReader(IBlobService blobService)
{
    public async Task<BinaryData?> TryReadAsync(string name, CancellationToken ct)
    {
        try
        {
            var result = await blobService.GetAsync(new BlobRequest(name), ct);
            return result?.Data;
        }
        catch (FileNotFoundException)
        {
            return null; // Local provider's "missing" signal; S3 returns null, Azure throws RequestFailedException
        }
    }
}
```

**Notes**: `CheckExistsAsync` does not have this split — it returns `false` for a missing file or directory on
every provider, so prefer it over a try/catch when you only need existence, not content.

### Listing a directory recursively

**When**: enumerating everything under a folder, including nested subfolders.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.Threading;

public sealed class ReportLister(IBlobService blobService)
{
    public async Task<List<string>> ListFileNamesAsync(CancellationToken ct)
    {
        var names = new List<string>();
        await foreach (var item in blobService.ListItemsAsync(new BlobRequest("reports") { Type = BlobTypes.Directory }, ct))
            if (item.Type == BlobTypes.File)
                names.Add(item.Name);
        return names;
    }
}
```

**Notes**: files and directories are yielded from a single recursive filesystem walk, so they can interleave in
whatever order the filesystem returns them — do not assume "all files, then all directories". Listing a path that
is neither an existing file nor an existing directory yields an empty sequence rather than throwing (same "not
found ⇒ nothing" shape as S3/Azure). Names come back relative to the root, correctly even when a subfolder's name
happens to repeat the root folder's own name.

### Streaming a large upload without buffering it fully

**When**: saving a large payload where you want to avoid holding the whole thing as `BinaryData` in memory.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;
using System.Threading;

public sealed class LargeUploadHandler(IBlobService blobService)
{
    public Task<string> SaveLargeFileAsync(string name, Stream content, CancellationToken ct) =>
        blobService.SaveAsync(new BlobDetails.BlobStreamData(name, content) { Overwrite = true }, ct);
}
```

**Notes**: `LocalBlobService` overrides the `BlobStreamData` overload directly (rather than relying on the default
interface member that buffers into `BinaryData`), writing through a `FileStream` opened with `useAsync: true` via
`CopyToAsync`. The caller still owns and must dispose `content`.

### Testing code that depends on `IBlobService`

**When**: exercising blob-writing application code in a unit/integration test without a storage emulator.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using System.IO;

public sealed class LocalBlobServiceFixture : IDisposable
{
    public LocalBlobServiceFixture()
    {
        TestRoot = Path.Combine(Path.GetTempPath(), "test-blobs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TestRoot);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobStorage:LocalFolder:RootFolder"] = TestRoot
            })
            .Build();

        var provider = new ServiceCollection()
            .AddLogging()
            .AddLocalDirectoryBlobService(config)
            .BuildServiceProvider();

        Service = provider.GetRequiredService<IBlobService>();
    }

    public IBlobService Service { get; }
    public string TestRoot { get; }

    public void Dispose()
    {
        if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, true);
    }
}
```

**Notes**: give each test fixture instance its own `Guid`-suffixed temp root so parallel test classes never share
files. This is the pattern to reach for instead of Azurite/MinIO containers whenever the code under test only
needs *some* `IBlobService`, not S3/Azure-specific behavior.

## Runtime behaviour

Every operation on `LocalBlobService` **except** `GetPublicAccessUrl` (which always throws immediately, before
any path is resolved) funnels through a `GetFinalPath(BlobRequest blob)` helper:

1. Strip a single leading `/` from `blob.Name`, if present.
2. Compute the root's full path and the resolved full path for `RootFolder` combined with the (stripped) name.
3. Compare the resolved path against the root path with `StringComparison.OrdinalIgnoreCase` on Windows or
   `StringComparison.Ordinal` elsewhere.
4. If the resolved path does not start with the root path, throw `UnauthorizedAccessException` — the filesystem is
   never touched.
5. Otherwise return the resolved path, and the calling method proceeds:
   - `SaveAsync(BlobStreamData)`: calls `ValidateFile` (name/extension/size checks), checks `CheckExistsAsync` +
     `Overwrite` (throws `InvalidOperationException` if it exists and `Overwrite` is `false`), creates missing
     parent directories, opens a `FileStream` and `CopyToAsync`s the validated content stream into it, then
     returns `blob.Name` unchanged.
   - `GetAsync`: throws `FileNotFoundException` if the file is absent; otherwise opens a read stream, buffers it
     into `BinaryData`, disposes the stream, and returns a `BlobDataResult` whose `Name` is the root-relative path.
   - `DeleteAsync`: for `BlobTypes.File`, deletes via `File.Delete` if it exists, else returns `false`; for
     `BlobTypes.Directory`, logs an `Error` (sanitized) and returns `false` if the directory is missing, otherwise
     deletes it recursively and unconditionally.
   - `ListItemsAsync`: if the resolved path is a directory, walks it once, yielding a `BlobResult` per entry (files
     get `Details`; directories don't). Otherwise, if the resolved path is an existing file, yields exactly that
     one `BlobResult`; if neither, yields nothing.
   - `GetPublicAccessUrl`: always throws `NotSupportedException`, regardless of input.

## Diagnostics & exceptions

| Exception | When | Fix |
|---|---|---|
| `UnauthorizedAccessException` | The path-traversal guard resolves a request name outside `RootFolder` (e.g. `"../../etc/passwd"`). | Pass a name that stays under the configured root; do not accept raw caller-supplied paths without validating intent. |
| `FileNotFoundException` | `GetAsync` called for a path that does not exist. | Catch it explicitly when writing provider-agnostic code (S3 returns `null`, Azure throws `RequestFailedException` for the same miss). |
| `InvalidOperationException("File already existed")` | `SaveAsync` targets an existing file with `Overwrite == false` (the default). | Set `Overwrite = true` on the request to replace it. |
| `NotSupportedException` | `GetPublicAccessUrl` is called on this provider — there is no shareable URL for a local path. | Use `DKNet.Svc.BlobStorage.AwsS3` or `DKNet.Svc.BlobStorage.AzureStorage` when public URLs are required. |
| `FileLoadException` (shared, from Abstractions) | `SaveAsync` violates a configured `MaxFileNameLength`, `IncludedExtensions`, or `MaxFileSizeInMb`. | Adjust the payload/name, or the configured limit, to comply. |
| Error log record (`LogLevel.Error`, not thrown): `"The directory {FolderLocation} was not found"` | `DeleteAsync` targets a missing directory; the call still returns `false`. | Check `CheckExistsAsync` first if the missing-directory case shouldn't produce an error-level log entry. |

## Gotchas

- **`RootFolder`'s default depends on the process working directory.** Left unset, it resolves to
  `Directory.GetCurrentDirectory() + "/LocalStore"` at construction time — which differs between `dotnet run`, a
  published binary, and a container.
- **Deleting a directory is recursive and unconditional.** There is no confirmation or dry-run, and `BlobRequest`'s
  own constructor infers `Type = Directory` whenever the name has no file extension — so a request built from a
  bare name is silently treated as a directory-delete.
- **`GetAsync` throws instead of returning `null` on a miss, breaking provider-agnostic code that assumes S3's
  `null` shape.** Callers that run the same code against multiple providers must special-case
  `FileNotFoundException` for this one provider.
- **`IsDirectory` never throws** — it is a bare `Directory.Exists(path)` call, so a missing intermediate segment
  or an invalid character in the path both produce `false`, not an exception. `ListItemsAsync` relies on that
  no-throw behavior to fall through to its "check as a single file" branch.
- **`AddLocalDirectoryBlobService`'s idempotency guard only matches on the exact `(IBlobService, LocalBlobService)`
  pair.** Registering this provider after S3 or Azure Storage is already registered adds a second `IBlobService`
  implementation rather than replacing or erroring — by design (DKNet's providers coexist), but a consumer who
  expects a single `IBlobService` to be resolvable via `GetRequiredService<IBlobService>()` will get an
  `InvalidOperationException` for multiple registrations, or an arbitrary one via `GetServices<IBlobService>().Last()`.
- **`SaveAsync(BlobData)` round-trips through `SaveAsync(BlobStreamData)`**, converting the `BinaryData` payload to
  a stream first — so calling the `BlobData` overload does not skip the streaming write path; it is a thin adapter
  over it, not a separate write implementation.
- **The path-comparison rule inside the traversal guard is platform-dependent** (`OrdinalIgnoreCase` on Windows,
  `Ordinal` elsewhere), so a root and blob name that differ only by case behave differently across OSes even
  though the rest of the framework treats paths uniformly.
- **`IsDirectory` is declared in `Microsoft.Extensions.DependencyInjection`, not a filesystem or Local
  namespace** — it is colocated with the setup class's registration method purely because both are `static`
  members of the same class, not because it is DI-related. Don't expect to find a filesystem helper in
  `DKNet.Svc.BlobStorage.Local` under that name.
- **Log messages containing caller-controlled path segments are sanitized before being logged** (CR/LF/line
  separators/control characters stripped) specifically to prevent log-forging (CWE-117) from a blob name
  containing a newline — worth preserving if this logging path is ever touched.

## Anti-patterns & hallucination traps

- **`LocalBlobService.SaveAsync` does NOT call `File.WriteAllBytesAsync`.** The current implementation writes via
  a `FileStream` opened for async I/O and `CopyToAsync` — don't generate code or explanations assuming
  `File.WriteAllBytesAsync` is involved.
- **Do not assume `ListItemsAsync` yields "all files, then all directories".** The current implementation performs
  one combined filesystem walk, so entries interleave; code that sorts/filters by type after listing is fine, code
  that assumes emission order is not.
- **There is no `LocalBlobServiceOptions` type** — the options class is named `LocalDirectoryOptions`. Don't guess
  `LocalBlobServiceOptions`, `LocalStorageOptions`, or similar.
- **The DI method is `AddLocalDirectoryBlobService`, not `AddLocalBlobStorage`, `AddLocalBlobService`, or
  `UseLocalBlobStorage`.** Its configuration section is `"BlobStorage:LocalFolder"` — the cloud providers use a
  `"BlobService:"` prefix instead, so don't copy that prefix here.
- **Don't hand-roll path-traversal checks around calls to this provider.** The built-in guard already resolves and
  validates every request; wrapping calls in an additional manual `..` string check is redundant and can produce
  false rejections that diverge from the real guard's normalization rules.
- **`GetPublicAccessUrl` is not "not yet implemented" — it is permanently unsupported by design** on this
  provider. Don't suggest a workaround using `file://` URIs as a substitute; recommend switching providers
  instead.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.Svc.BlobStorage.Abstractions` | Always reference this alongside `Local` — it defines `IBlobService`, `BlobRequest`, `BlobDetails.*`, `BlobServiceOptions`, and the `BlobService` base class `LocalBlobService` derives from. Application code should depend on `IBlobService`/`Abstractions` types only, never on `DKNet.Svc.BlobStorage.Local` directly, so the provider can be swapped without touching consumers. |
| `DKNet.Svc.BlobStorage.AwsS3` | Reach for it instead of `Local` when the deployed environment stores blobs in S3 or an S3-compatible service (production, multi-instance). |
| `DKNet.Svc.BlobStorage.AzureStorage` | Reach for it instead of `Local` when the deployed environment stores blobs in an Azure Storage account (production, multi-instance, and when `GetPublicAccessUrl` support is required). |

## Testing notes

- Covered in the same shared blob-storage test project as the S3 and Azure providers.
- Canonical fixture: builds an in-memory `IConfiguration` with `"BlobStorage:LocalFolder:RootFolder"` pointed at a
  fresh `Guid`-suffixed temp directory, runs it through `ServiceCollection().AddLogging().AddLocalDirectoryBlobService(config)`,
  and resolves `IBlobService` from the built provider — no mocking of the filesystem or of `IBlobService` itself.
  `Dispose()` recursively deletes the temp root.
- Happy-path coverage: exists/delete for files and directories, get (found + `FileNotFoundException`), public-URL
  `NotSupportedException`, listing (directory recursive, single file, and the single-tree-walk regression that
  both files and directories still surface even though they can interleave), and save/overwrite/already-exists.
  A separate fixture test proves two fixture instances never share a root (parallel-safe).
- Edge-case tests construct `LocalBlobService` directly (not through DI) to hit security/defensive branches: null
  `RootFolder` fallback, path-traversal `UnauthorizedAccessException` on `CheckExistsAsync`/`DeleteAsync`,
  leading-slash normalization, cancelled-token early exits on delete (file and folder), and the "root name repeats
  as a subfolder name" relative-path regression — using a custom capturing logger to assert on the
  missing-directory log record.
- A log-sanitization test suite targets the CWE-117 log-forging fix specifically: a blob name containing `\n`,
  `\r`, ESC, or the Unicode line separators must not appear raw in the logged message or its structured data.
- Shared setup tests cover DI idempotency (`AddLocalDirectoryBlobService` called twice registers `LocalBlobService`
  once), chaining, `IsDirectory`'s no-throw behavior on a temp path, a path with a missing intermediate directory,
  and a path with an invalid character — plus a multi-provider coexistence test proving Local, S3, and Azure
  Storage registrations survive side by side as distinct `IBlobService` implementations.
- No `TestContainers` or Docker dependency for this provider — it is pure filesystem I/O against a temp directory.
