# Write a custom IBlobService provider

Part of [DKNet.Svc.BlobStorage.Abstractions](index.md). Derive from the abstract `BlobService` class to get the
shared validation and path-normalization rules for free, and implement only the backend-specific plumbing.

`BlobService` declares six abstract members: `CheckExistsAsync`, `DeleteAsync`, `GetAsync`, `GetPublicAccessUrl`,
`ListItemsAsync`, and `SaveAsync(BlobDetails.BlobData, ...)`. `OpenReadAsync` and `SaveAsync(BlobDetails.BlobStreamData, ...)`
are `virtual` with a default implementation that buffers through `GetAsync`/`SaveAsync(BlobData)` — override them
only when your backend has a genuinely streaming read or write:

```csharp
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

    public override Task<BlobDetails.BlobDataResult?> GetAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
        Task.FromResult(_blobs.TryGetValue(GetBlobLocation(blob), out var data)
            ? new BlobDetails.BlobDataResult(blob.Name, data) { Details = new BlobDetails { ContentType = blob.Name.GetContentTypeByExtension() } }
            : null);

    public override Task<Uri> GetPublicAccessUrl(BlobRequest blob, TimeSpan? expiresFromNow = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("In-memory storage has no public URL.");

    public override async IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(BlobRequest blob, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in _blobs.Keys.Where(k => k.StartsWith(GetBlobLocation(blob), StringComparison.Ordinal)))
            yield return new BlobDetails.BlobResult(key);
        await Task.CompletedTask;
    }
}
```

![ValidateFile's three independent, opt-in checks — name length, extension allow-list, and size — each throw FileLoadException on their own violation; a provider's SaveAsync calls ValidateFile once and then writes to its own backing store.](diagrams/write-a-custom-blob-service-provider.svg)

The diagram does not show the stream-based overload: `ValidateFile(BlobDetails.BlobStreamData)` returns the stream a
provider should read from — the source stream unchanged when it is seekable and within the configured limit, or a
`SizeLimitedStream` wrapper that throws mid-read once a non-seekable source exceeds the limit, since its length can't
be checked upfront.

## ⚙️ Configuration reference

Every provider's own options type extends `BlobServiceOptions`, so validation behaves identically regardless of
backend. All three checks are opt-in — do not assume a default limit exists.

| Option | Type | Default | Effect |
|---|---|---|---|
| `IncludedExtensions` | `IReadOnlyList<string>` | `[]` (empty) | No extension filtering when empty; when non-empty, only listed extensions pass (compared case-insensitively, leading dot included). |
| `MaxFileNameLength` | `int` | `0` | `0` disables the check — not "zero-length names rejected". |
| `MaxFileSizeInMb` | `int` | `0` | `0` disables the check; otherwise the limit is `MaxFileSizeInMb * 1_000_000` bytes — decimal megabytes, not `* 1024 * 1024`. |

`GetBlobLocation` is `protected virtual` too: the default prefixes the name with `/` if it doesn't already start with
one; override it to change how a name becomes a provider path.

## 🧱 Where it fits

`BlobService` and `BlobServiceOptions` live in this package; every shipped provider —
[AwsS3](../DKNet.Svc.BlobStorage.AwsS3/index.md), [AzureStorage](../DKNet.Svc.BlobStorage.AzureStorage/index.md), and
[Local](../DKNet.Svc.BlobStorage.Local/index.md) — derives from them the same way a custom provider would. None of
them override `GetBlobLocation`; each trims or reshapes the normalized `/name` on top of it for its own backend
instead (see each provider's page).

## ⚠️ Gotchas & limits

- `MaxFileSizeInMb` counts **decimal** megabytes. A "10 MB" cap rejects at 10,000,000 bytes, not 10,485,760.
- `fileName.GetContentTypeByExtension()` throws `NullReferenceException` for a `null` input — always pass a non-null
  file name; it falls back to `"application/octet-stream"` for an unrecognized extension, never throws for that case.
- There is no provider registry or factory interface — composition is DI registration order. Multiple `IBlobService`
  registrations can coexist in one `IServiceCollection`; the last one registered wins when a single `IBlobService` is
  resolved.
- `GetItemAsync`'s default implementation is "first item from `ListItemsAsync`" — cheap for a single file, wasteful
  for a directory listing. Override it when your backend can fetch metadata directly.
- Built-in providers disagree on how a missing blob is signaled from `GetAsync`: S3 and Azure Storage return `null`,
  the Local provider throws `FileNotFoundException`. A custom provider can pick either shape, but document which one
  it uses — code written against more than one provider needs to handle both.
