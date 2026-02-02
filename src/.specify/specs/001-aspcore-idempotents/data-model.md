# Data Model: DKNet.AspCore.Idempotents

## Entities

### IdempotencyKey (Value Object)

**Purpose**: Encapsulates and validates the idempotency key from client requests.

```csharp
/// <summary>
///     Represents a validated idempotency key extracted from HTTP requests.
/// </summary>
public readonly record struct IdempotencyKey
{
    /// <summary>
    ///     Gets the validated key value.
    /// </summary>
    public string Value { get; }

    private IdempotencyKey(string value) => Value = value;

    /// <summary>
    ///     Attempts to create an IdempotencyKey from a string value.
    /// </summary>
    /// <param name="value">The raw key value from the request header.</param>
    /// <param name="key">The validated key if successful.</param>
    /// <param name="maxLength">Maximum allowed key length (default: 256).</param>
    /// <returns>True if the key is valid; otherwise, false.</returns>
    public static bool TryCreate(string? value, out IdempotencyKey key, int maxLength = 256);

    /// <summary>
    ///     Implicitly converts the key to its string value.
    /// </summary>
    public static implicit operator string(IdempotencyKey key) => key.Value;
}
```

**Validation Rules**:
| Rule | Description |
|------|-------------|
| Not null/empty | Key must have content |
| Max length | Default 256 characters |
| Allowed characters | `[a-zA-Z0-9_-]` |
| No whitespace | Trimmed, no internal spaces |

---

### CachedResponse (Storage Entity)

**Purpose**: Stores the complete HTTP response for replay on duplicate requests.

```csharp
/// <summary>
///     Represents a cached HTTP response for idempotent request replay.
/// </summary>
public sealed record CachedResponse
{
    /// <summary>
    ///     The HTTP status code of the original response.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    ///     The response headers to replay (excludes hop-by-hop headers).
    /// </summary>
    public required IReadOnlyDictionary<string, string[]> Headers { get; init; }

    /// <summary>
    ///     The response body as a byte array.
    /// </summary>
    public required byte[] Body { get; init; }

    /// <summary>
    ///     When the response was originally created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    ///     When the cached response expires and should no longer be returned.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    ///     Optional SHA256 hash of the original request body for fingerprinting.
    /// </summary>
    public string? RequestBodyHash { get; init; }

    /// <summary>
    ///     Checks if the cached response has expired.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}
```

**Serialization**: JSON with Base64 encoding for body bytes.

---

### IdempotencyOptions (Configuration)

**Purpose**: Configuration options for the idempotency middleware.

```csharp
/// <summary>
///     Configuration options for idempotency behavior.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>
    ///     The HTTP header name to extract the idempotency key from.
    ///     Default: "Idempotency-Key"
    /// </summary>
    public string HeaderName { get; set; } = IdempotencyConstants.DefaultHeaderName;

    /// <summary>
    ///     Default time-to-live for cached responses.
    ///     Default: 24 hours
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    ///     Maximum allowed length for idempotency keys.
    ///     Default: 256 characters
    /// </summary>
    public int MaxKeyLength { get; set; } = 256;

    /// <summary>
    ///     When true, validates that the request body hash matches the original request.
    ///     Default: false
    /// </summary>
    public bool EnableFingerprinting { get; set; }

    /// <summary>
    ///     When true, caches error responses (4xx, 5xx). When false, only success responses are cached.
    ///     Default: false
    /// </summary>
    public bool CacheErrorResponses { get; set; }

    /// <summary>
    ///     How to handle concurrent requests with the same idempotency key.
    ///     Default: Wait
    /// </summary>
    public ConcurrencyMode ConcurrencyMode { get; set; } = ConcurrencyMode.Wait;

    /// <summary>
    ///     Maximum time to wait for a lock when ConcurrencyMode is Wait.
    ///     Default: 30 seconds
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Maximum request body size to cache (in bytes). Larger bodies are not cached.
    ///     Default: 1MB
    /// </summary>
    public int MaxBodySize { get; set; } = 1024 * 1024;

    // Internal storage type selection
    internal Type? StoreType { get; private set; }
    internal bool UseDistributed { get; private set; }

    /// <summary>
    ///     Use the in-memory store (default for single-server deployments).
    /// </summary>
    public IdempotencyOptions UseInMemory()
    {
        StoreType = typeof(InMemoryIdempotencyStore);
        UseDistributed = false;
        return this;
    }

    /// <summary>
    ///     Use IDistributedCache for storage (Redis, SQL, etc.).
    /// </summary>
    public IdempotencyOptions UseDistributedCache()
    {
        StoreType = typeof(DistributedIdempotencyStore);
        UseDistributed = true;
        return this;
    }

    /// <summary>
    ///     Use a custom store implementation.
    /// </summary>
    public IdempotencyOptions UseStore<TStore>() where TStore : class, IIdempotencyStore
    {
        StoreType = typeof(TStore);
        UseDistributed = false;
        return this;
    }
}
```

---

### ConcurrencyMode (Enum)

**Purpose**: Defines behavior when concurrent requests arrive with the same key.

```csharp
/// <summary>
///     Defines how concurrent requests with the same idempotency key are handled.
/// </summary>
public enum ConcurrencyMode
{
    /// <summary>
    ///     Wait for the first request to complete and return its cached response.
    /// </summary>
    Wait,

    /// <summary>
    ///     Immediately reject with 409 Conflict and Retry-After header.
    /// </summary>
    RejectWithConflict
}
```

---

### IdempotencyConstants (Static Class)

**Purpose**: Constants for header names, defaults, and error messages.

```csharp
/// <summary>
///     Constants used throughout the idempotency library.
/// </summary>
public static class IdempotencyConstants
{
    // Request Headers
    public const string DefaultHeaderName = "Idempotency-Key";

    // Response Headers
    public const string StatusHeader = "Idempotency-Key-Status";
    public const string ExpiresHeader = "Idempotency-Key-Expires";
    public const string RetryAfterHeader = "Retry-After";

    // Status Values
    public const string StatusCreated = "created";
    public const string StatusCached = "cached";

    // Error Messages
    public const string MissingKeyError = "Idempotency key is required for this endpoint.";
    public const string InvalidKeyError = "Idempotency key format is invalid.";
    public const string FingerprintMismatchError = "Request body does not match original request for this idempotency key.";
    public const string ConflictError = "A request with this idempotency key is already in progress.";

    // Cache Key Prefix
    public const string CacheKeyPrefix = "idempotency:";
    public const string LockKeyPrefix = "idempotency:lock:";
}
```

---

## Relationships

```
┌─────────────────────────────────────────────────────────────┐
│                     HTTP Request                            │
│  Header: Idempotency-Key: abc-123-xyz                       │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              IdempotencyEndpointFilter                      │
│  - Extracts key → IdempotencyKey.TryCreate()                │
│  - Checks store → IIdempotencyStore.GetAsync()              │
│  - Executes or returns cached                               │
└─────────────────────────────────────────────────────────────┘
                            │
            ┌───────────────┴───────────────┐
            │                               │
            ▼                               ▼
┌─────────────────────┐         ┌─────────────────────┐
│   IdempotencyKey    │         │   CachedResponse    │
│   (Value Object)    │         │   (Storage Entity)  │
│                     │         │                     │
│ + Value: string     │ ◄─────► │ + StatusCode: int   │
│ + TryCreate()       │   key   │ + Headers: dict     │
│                     │         │ + Body: byte[]      │
└─────────────────────┘         │ + ExpiresAt: date   │
                                └─────────────────────┘
                                          │
                                          ▼
                                ┌─────────────────────┐
                                │  IIdempotencyStore  │
                                │                     │
                                │ + GetAsync()        │
                                │ + SetAsync()        │
                                │ + TryAcquireLock()  │
                                │ + ReleaseLock()     │
                                └─────────────────────┘
                                          │
                    ┌─────────────────────┴─────────────────────┐
                    │                                           │
                    ▼                                           ▼
        ┌─────────────────────┐                     ┌─────────────────────┐
        │ InMemoryIdempotency │                     │ DistributedIdempot- │
        │       Store         │                     │      encyStore      │
        │                     │                     │                     │
        │ ConcurrentDictionary│                     │ IDistributedCache   │
        │ SemaphoreSlim locks │                     │ Distributed locks   │
        └─────────────────────┘                     └─────────────────────┘
```

## State Transitions

### Request Processing States

```
                    ┌──────────────┐
                    │   Incoming   │
                    │   Request    │
                    └──────┬───────┘
                           │
                           ▼
                    ┌──────────────┐
              No    │  Has Valid   │   Yes
           ┌────────│ Idempotency  │────────┐
           │        │    Key?      │        │
           │        └──────────────┘        │
           ▼                                ▼
    ┌──────────────┐                 ┌──────────────┐
    │  Return 400  │           Yes  │   Cached     │   No
    │  Bad Request │        ┌───────│  Response?   │───────┐
    └──────────────┘        │       └──────────────┘       │
                            │                              │
                            ▼                              ▼
                     ┌──────────────┐              ┌──────────────┐
                     │   Return     │              │  Acquire     │
                     │   Cached     │              │    Lock      │
                     │  Response    │              └──────┬───────┘
                     │  + Headers   │                     │
                     └──────────────┘                     ▼
                                                  ┌──────────────┐
                                                  │   Execute    │
                                                  │   Endpoint   │
                                                  └──────┬───────┘
                                                         │
                                                         ▼
                                                  ┌──────────────┐
                                                  │    Cache     │
                                                  │   Response   │
                                                  └──────┬───────┘
                                                         │
                                                         ▼
                                                  ┌──────────────┐
                                                  │   Release    │
                                                  │    Lock      │
                                                  └──────┬───────┘
                                                         │
                                                         ▼
                                                  ┌──────────────┐
                                                  │   Return     │
                                                  │  Response    │
                                                  │  + Headers   │
                                                  └──────────────┘
```
