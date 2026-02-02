# DKNet.AspCore.Idempotency - Implementation Guide for Improvements

## 1. Critical Fixes (Implement Immediately)

### Fix 1.1: InternalsVisibleTo Typo

**File**: `DKNet.AspCore.Idempotency.csproj`

```xml
<!-- BEFORE -->
<InternalsVisibleTo Include="AspCore.Idempotents.Tests"/>

<!-- AFTER -->
<InternalsVisibleTo Include="AspCore.Idempotency.Tests"/>
```

---

### Fix 1.2: Add Cache Exception Handling

**File**: `IIdempotencyKeyRepository.cs`

**Current Code** (lines 51-59):
```csharp
public async ValueTask<(bool processed, string? result)> IsKeyProcessedAsync(string idempotencyKey)
{
    var cacheKey = SanitizeKey(idempotencyKey);
    logger.LogDebug("Trying to get existing result for cache key: {CacheKey}", cacheKey);

    var result = await cache.GetStringAsync(cacheKey).ConfigureAwait(false);

    logger.LogDebug("Existing result found: {Result}", result);
    return (!string.IsNullOrWhiteSpace(result), result);
}
```

**Improved Code**:
```csharp
/// <summary>
///     Checks if the idempotency key has been processed and retrieves its cached result if available.
///     If the cache is unavailable, logs a warning and returns (false, null) to allow the request to proceed.
/// </summary>
/// <param name="idempotencyKey">The idempotency key to check in the distributed cache.</param>
/// <returns>
///     A tuple containing:
///     - A boolean indicating whether the key exists in the cache (has been processed)
///     - The cached result string if it exists, or null if no result was cached
///     - Returns (false, null) if cache operation fails, allowing fail-open behavior
/// </returns>
public async ValueTask<(bool processed, string? result)> IsKeyProcessedAsync(string idempotencyKey)
{
    try
    {
        var cacheKey = SanitizeKey(idempotencyKey);
        logger.LogDebug("Trying to get existing result for cache key: {CacheKey}", cacheKey);

        var result = await cache.GetStringAsync(cacheKey).ConfigureAwait(false);

        logger.LogDebug("Existing result found: {Result}", result);
        return (!string.IsNullOrWhiteSpace(result), result);
    }
    catch (OperationCanceledException ex)
    {
        logger.LogWarning(
            ex,
            "Cache operation timed out while checking idempotency key. Allowing request to proceed (fail-open).");
        return (processed: false, null);
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Unexpected error accessing cache for idempotency key check. Allowing request to proceed (fail-open).");
        return (processed: false, null);
    }
}
```

**File**: `IIdempotencyKeyRepository.cs`

**Current Code** (lines 65-77):
```csharp
public async ValueTask MarkKeyAsProcessedAsync(string idempotencyKey, string? result = null)
{
    var cacheKey = SanitizeKey(idempotencyKey);
    logger.LogDebug("Setting cache result for cache key: {CacheKey}", cacheKey);

    await cache.SetStringAsync(cacheKey, string.IsNullOrWhiteSpace(result) ? bool.TrueString : result,
        new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.Expiration
        }).ConfigureAwait(false);
}
```

**Improved Code**:
```csharp
/// <summary>
///     Marks an idempotency key as processed and optionally caches its result.
///     If the cache operation fails, logs a warning but does not throw (fail-safe).
/// </summary>
/// <param name="idempotencyKey">The idempotency key to mark as processed.</param>
/// <param name="result">
///     The result to cache. If null or whitespace, only a boolean marker is cached.
///     This allows the key to be marked as processed without storing a result.
/// </param>
/// <returns>A task representing the asynchronous cache operation.</returns>
public async ValueTask MarkKeyAsProcessedAsync(string idempotencyKey, string? result = null)
{
    try
    {
        var cacheKey = SanitizeKey(idempotencyKey);
        logger.LogDebug("Setting cache result for cache key: {CacheKey}", cacheKey);

        await cache.SetStringAsync(cacheKey, string.IsNullOrWhiteSpace(result) ? bool.TrueString : result,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.Expiration
            }).ConfigureAwait(false);
    }
    catch (OperationCanceledException ex)
    {
        logger.LogWarning(
            ex,
            "Cache operation timed out while marking idempotency key as processed. Continuing without caching.");
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Unexpected error writing to cache for idempotency key. Continuing without caching.");
    }
}
```

---

## 2. High-Priority Fixes (Implement in Sprint 1)

### Fix 2.1: Fix Route Template Retrieval

**File**: `IdempotencyEndpointFilter.cs`

**Current Code** (lines 63-65):
```csharp
var endpoint = context.HttpContext.GetEndpoint();
var routeTemplate = endpoint?.Metadata.GetMetadata<RouteAttribute>()?.Template ??
                    context.HttpContext.Request.Path;
var compositeKey = $"{routeTemplate}_{idempotencyKey}";
```

**Improved Code**:
```csharp
var endpoint = context.HttpContext.GetEndpoint();

// Get the HTTP method from the request
var httpMethod = context.HttpContext.Request.Method;

// Try to get route template from endpoint metadata
// For minimal APIs, RouteAttribute may not be populated, so fallback to Request.Path
var routeTemplate = endpoint?.Metadata.GetMetadata<RouteAttribute>()?.Template 
                    ?? context.HttpContext.Request.Path.Value 
                    ?? "/unknown";

// Create composite key with HTTP method to ensure uniqueness across different endpoint operations
var compositeKey = $"{httpMethod}:{routeTemplate}_{idempotencyKey}".ToUpperInvariant();

logger.LogDebug(
    "Created composite idempotency key. Method={Method}, Route={Route}, Key={CompositeKey}",
    httpMethod,
    routeTemplate,
    compositeKey);
```

**Update Documentation** in `README.md`:
```markdown
### Composite Key Format

The filter creates a composite key from the HTTP method, route template, and idempotency key 
to uniquely identify requests:

```
CompositeKey = "{httpMethod}:{routeTemplate}_{idempotencyKey}".ToUpperInvariant()

Examples:
- Route: POST /orders, Key: abc-123 → "POST:/ORDERS_ABC-123"
- Route: PUT /orders/{id}, Key: abc-123 → "PUT:/ORDERS/{ID}_ABC-123"
- Route: DELETE /orders/{id}, Key: abc-123 → "DELETE:/ORDERS/{ID}_ABC-123"
```

This ensures the same idempotency key can be used across different HTTP methods on the same endpoint.
```

---

### Fix 2.2: Add Idempotency Key Validation

**File**: `IdempotencyOptions.cs`

Add to the class (after line 58):
```csharp
/// <summary>
///     Gets or sets the maximum allowed length for an idempotency key.
///     Keys longer than this will be rejected with a 400 Bad Request.
///     Default is 255 characters.
/// </summary>
public int MaxIdempotencyKeyLength { get; set; } = 255;

/// <summary>
///     Gets or sets a regular expression pattern used to validate idempotency key format.
///     Keys that don't match this pattern will be rejected with a 400 Bad Request.
///     Default pattern allows alphanumeric characters, hyphens, and underscores (UUID v4 compatible).
/// </summary>
public string IdempotencyKeyPattern { get; set; } = @"^[a-zA-Z0-9\-_]+$";
```

**File**: `IdempotencyEndpointFilter.cs`

Replace the idempotency key check (lines 52-58) with:
```csharp
var idempotencyKey = context.HttpContext.Request.Headers[_options.IdempotencyHeaderKey].FirstOrDefault();

// Validate header presence
if (string.IsNullOrEmpty(idempotencyKey))
{
    logger.LogWarning(
        "Idempotency header '{HeaderName}' is missing. Returning 400 Bad Request.",
        _options.IdempotencyHeaderKey);
    return TypedResults.Problem(
        detail: $"The '{_options.IdempotencyHeaderKey}' header is required for idempotent requests.",
        statusCode: StatusCodes.Status400BadRequest,
        type: "https://tools.ietf.org/html/rfc7231#section-6.5.1");
}

// Validate key length
if (idempotencyKey.Length > _options.MaxIdempotencyKeyLength)
{
    logger.LogWarning(
        "Idempotency key exceeds maximum length of {MaxLength}. Key length: {ActualLength}",
        _options.MaxIdempotencyKeyLength,
        idempotencyKey.Length);
    return TypedResults.Problem(
        detail: $"Idempotency key must not exceed {_options.MaxIdempotencyKeyLength} characters.",
        statusCode: StatusCodes.Status400BadRequest,
        type: "https://tools.ietf.org/html/rfc7231#section-6.5.1");
}

// Validate key format
if (!System.Text.RegularExpressions.Regex.IsMatch(idempotencyKey, _options.IdempotencyKeyPattern))
{
    logger.LogWarning(
        "Idempotency key format is invalid. Key must match pattern: {Pattern}",
        _options.IdempotencyKeyPattern);
    return TypedResults.Problem(
        detail: $"Idempotency key format is invalid. Allowed characters: alphanumeric, hyphens, underscores.",
        statusCode: StatusCodes.Status400BadRequest,
        type: "https://tools.ietf.org/html/rfc7231#section-6.5.1");
}

logger.LogDebug(
    "Validated idempotency key. HeaderKey={HeaderKey}, KeyLength={KeyLength}",
    _options.IdempotencyHeaderKey,
    idempotencyKey.Length);
```

---

### Fix 2.3: Handle Response Serialization Failures

**File**: `IdempotencyEndpointFilter.cs`

Replace the caching logic (lines 99-110) with:
```csharp
// Cache the response to prevent duplicate processing
if (result is null)
    return result;

var resultValue = result.GetPropertyValue("Value") ?? result;

// Only cache successful responses (2xx)
if (context.HttpContext.Response.StatusCode is >= 200 and < 300)
{
    if (_options.ConflictHandling == IdempotentConflictHandling.ConflictResponse)
    {
        await cacher.MarkKeyAsProcessedAsync(compositeKey).ConfigureAwait(false);
        logger.LogInformation(
            "Marked idempotency key as processed (conflict response mode). Key={Key}",
            idempotencyKey);
    }
    else
    {
        try
        {
            var serializedResult = JsonSerializer.Serialize(
                resultValue,
                _options.JsonSerializerOptions);
            
            await cacher.MarkKeyAsProcessedAsync(compositeKey, serializedResult)
                .ConfigureAwait(false);
            
            logger.LogInformation(
                "Cached response for idempotency key. Key={Key}, SerializedSize={Size}",
                idempotencyKey,
                serializedResult.Length);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Failed to serialize response for caching. Key={Key}, Type={ResponseType}. " +
                "Marking as processed without caching result.",
                idempotencyKey,
                resultValue?.GetType().Name ?? "unknown");
            
            // Mark processed but don't cache the result
            await cacher.MarkKeyAsProcessedAsync(compositeKey)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error while caching response for idempotency key={Key}. " +
                "Continuing without cache.",
                idempotencyKey);
        }
    }
}
else
{
    logger.LogDebug(
        "Not caching response. StatusCode={StatusCode} is not in 2xx range. Key={Key}",
        context.HttpContext.Response.StatusCode,
        idempotencyKey);
}

logger.LogDebug("Returning result to the client");
return result;
```

---

### Fix 2.4: Update Status Code Logic & Configuration

**File**: `IdempotencyOptions.cs`

Add configuration properties (after line 58):
```csharp
/// <summary>
///     Gets or sets the minimum HTTP status code (inclusive) that will be cached.
///     Default: 200 (OK)
/// </summary>
public int MinStatusCodeForCaching { get; set; } = 200;

/// <summary>
///     Gets or sets the maximum HTTP status code (inclusive) that will be cached.
///     Default: 299 (last 2xx success code)
/// </summary>
public int MaxStatusCodeForCaching { get; set; } = 299;

/// <summary>
///     Gets or sets additional status codes that should be cached even if outside the 2xx range.
///     For example, 301 (Moved Permanently) might be cacheable for redirects.
///     Default: empty set
/// </summary>
public HashSet<int> AdditionalCacheableStatusCodes { get; set; } = new();
```

**File**: `IdempotencyEndpointFilter.cs`

Update the status code check logic:
```csharp
// Helper method to determine if status code should be cached
private bool ShouldCacheStatusCode(int statusCode)
{
    if (statusCode >= _options.MinStatusCodeForCaching 
        && statusCode <= _options.MaxStatusCodeForCaching)
        return true;
    
    return _options.AdditionalCacheableStatusCodes.Contains(statusCode);
}

// Then use it:
if (ShouldCacheStatusCode(context.HttpContext.Response.StatusCode))
{
    // Cache logic...
}
else
{
    logger.LogDebug(
        "Status code {StatusCode} is not configured for caching. Key={Key}",
        context.HttpContext.Response.StatusCode,
        idempotencyKey);
}
```

---

## 3. Configuration Validation

**File**: `IdempotentSetup.cs`

Enhance `AddIdempotencyKey` method with validation:

```csharp
public static IServiceCollection AddIdempotencyKey(this IServiceCollection services,
    Action<IdempotencyOptions>? config = null)
{
    var options = new IdempotencyOptions();
    config?.Invoke(options);

    // Validate configuration
    ValidateOptions(options);

    services
        .AddSingleton<IIdempotencyKeyRepository, IdempotencyDistributedCacheRepository>()
        .AddSingleton(Options.Create(options));
    _configAdded = true;
    IdempotentHeaderKey = options.IdempotencyHeaderKey;

    return services;
}

private static void ValidateOptions(IdempotencyOptions options)
{
    if (string.IsNullOrWhiteSpace(options.IdempotencyHeaderKey))
        throw new ArgumentException(
            "IdempotencyHeaderKey cannot be empty or whitespace.",
            nameof(options.IdempotencyHeaderKey));

    if (string.IsNullOrWhiteSpace(options.CachePrefix))
        throw new ArgumentException(
            "CachePrefix cannot be empty or whitespace.",
            nameof(options.CachePrefix));

    if (options.Expiration <= TimeSpan.Zero)
        throw new ArgumentException(
            $"Expiration must be positive. Current value: {options.Expiration}",
            nameof(options.Expiration));

    if (options.JsonSerializerOptions is null)
        throw new ArgumentException(
            "JsonSerializerOptions cannot be null.",
            nameof(options.JsonSerializerOptions));

    if (options.MaxIdempotencyKeyLength < 1)
        throw new ArgumentException(
            "MaxIdempotencyKeyLength must be at least 1.",
            nameof(options.MaxIdempotencyKeyLength));

    if (string.IsNullOrWhiteSpace(options.IdempotencyKeyPattern))
        throw new ArgumentException(
            "IdempotencyKeyPattern cannot be empty.",
            nameof(options.IdempotencyKeyPattern));

    if (options.MinStatusCodeForCaching < 100)
        throw new ArgumentException(
            "MinStatusCodeForCaching must be >= 100.",
            nameof(options.MinStatusCodeForCaching));

    if (options.MaxStatusCodeForCaching > 599)
        throw new ArgumentException(
            "MaxStatusCodeForCaching must be <= 599.",
            nameof(options.MaxStatusCodeForCaching));

    if (options.MinStatusCodeForCaching > options.MaxStatusCodeForCaching)
        throw new ArgumentException(
            $"MinStatusCodeForCaching ({options.MinStatusCodeForCaching}) cannot be greater than " +
            $"MaxStatusCodeForCaching ({options.MaxStatusCodeForCaching}).",
            nameof(options.MinStatusCodeForCaching));
}
```

---

## 4. Method Naming Consistency

### Option A: Rename Everything to AddIdempotency (Recommended)

**File**: `IdempotentSetup.cs`

```csharp
/// <summary>
///     Registers idempotency services into the dependency injection container.
///     This method must be called during service registration before using RequireIdempotency on endpoints.
/// </summary>
/// <param name="services">The service collection to register idempotency services into.</param>
/// <param name="config">
///     An optional action to configure <see cref="IdempotencyOptions"/>.
///     If null, default options are used.
/// </param>
/// <returns>The service collection for method chaining.</returns>
public static IServiceCollection AddIdempotency(this IServiceCollection services,
    Action<IdempotencyOptions>? config = null)
{
    // ... implementation
}

// Keep AddIdempotentKey as a deprecated alias for backwards compatibility
/// <summary>
///     Deprecated. Use <see cref="AddIdempotency"/> instead.
/// </summary>
[Obsolete("Use AddIdempotency instead. This method will be removed in v11.0.", false)]
public static IServiceCollection AddIdempotentKey(this IServiceCollection services,
    Action<IdempotencyOptions>? config = null)
{
    return AddIdempotency(services, config);
}

/// <summary>
///     Adds the idempotency endpoint filter to a route handler.
/// </summary>
/// <param name="builder">The route handler builder to add the filter to.</param>
/// <returns>The route handler builder for method chaining.</returns>
public static RouteHandlerBuilder RequireIdempotency(this RouteHandlerBuilder builder)
{
    if (_configAdded)
        builder.AddEndpointFilter<IdempotencyEndpointFilter>();
    return builder;
}

// Keep RequiredIdempotentKey as a deprecated alias
/// <summary>
///     Deprecated. Use <see cref="RequireIdempotency"/> instead.
/// </summary>
[Obsolete("Use RequireIdempotency instead. This method will be removed in v11.0.", false)]
public static RouteHandlerBuilder RequiredIdempotentKey(this RouteHandlerBuilder builder)
{
    return RequireIdempotency(builder);
}
```

**Update Usage Examples** in README and code:
```csharp
// OLD
builder.Services.AddIdempotentKey();
app.MapPost("/orders", CreateOrder).RequiredIdempotentKey();

// NEW
builder.Services.AddIdempotency();
app.MapPost("/orders", CreateOrder).RequireIdempotency();
```

---

## 5. Enhanced Logging

**File**: `IdempotencyEndpointFilter.cs`

Add comprehensive logging at key points:

```csharp
public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
{
    var requestId = context.HttpContext.TraceIdentifier;
    
    // ... validation code ...

    logger.LogInformation(
        "Processing idempotent request. RequestId={RequestId}, Key={Key}, Route={Route}",
        requestId,
        idempotencyKey,
        routeTemplate);

    var existingResult = await cacher.IsKeyProcessedAsync(compositeKey).ConfigureAwait(false);
    
    if (existingResult.processed)
    {
        logger.LogInformation(
            "Duplicate request detected. RequestId={RequestId}, Key={Key}, Strategy={Strategy}",
            requestId,
            idempotencyKey,
            _options.ConflictHandling);

        if (_options.ConflictHandling == IdempotentConflictHandling.ConflictResponse)
        {
            logger.LogWarning(
                "Returning 409 Conflict for duplicate request. RequestId={RequestId}, Key={Key}",
                requestId,
                idempotencyKey);
            return TypedResults.Problem(
                $"The request with the same idempotent key `{idempotencyKey}` has already been processed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        logger.LogInformation(
            "Returning cached response for duplicate request. RequestId={RequestId}, Key={Key}",
            requestId,
            idempotencyKey);
        return TypedResults.Text(existingResult.result!, "application/json", Encoding.UTF8);
    }

    logger.LogDebug(
        "New request detected, proceeding with processing. RequestId={RequestId}, Key={Key}",
        requestId,
        idempotencyKey);

    // ... rest of implementation with logging ...
}
```

---

## 6. Recommended Test Examples

See separate test implementation guide document for comprehensive test examples.

---

## Implementation Order

1. **Week 1 (Critical)**:
   - Fix InternalsVisibleTo typo
   - Add cache exception handling
   - Deploy and test

2. **Week 2 (High Priority)**:
   - Fix route template retrieval
   - Add key validation
   - Add configuration validation
   - Implement serialization error handling

3. **Week 3 (Testing)**:
   - Implement P0 test suite
   - Fix any issues discovered

4. **Week 4 (Polish)**:
   - Method naming consistency
   - Enhanced logging
   - Status code configuration

5. **Ongoing**:
   - Low-priority improvements
   - Documentation updates
   - Performance testing

---

