using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DKNet.AspCore.Idempotency.Store;
using DKNet.Fw.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Idempotency;

/// <summary>
///     An ASP.NET Core endpoint filter that enforces idempotency for API endpoints.
///     This filter ensures that requests with the same idempotency key are not processed multiple times,
///     preventing unintended side effects from duplicate requests caused by network timeouts or retries.
/// </summary>
/// <remarks>
///     The filter:
///     1. Validates the presence of the idempotency key header (default: X-Idempotency-Key)
///     2. Validates the idempotency key format and length
///     3. Checks if the key has been processed before
///     4. Returns cached results for duplicate requests (based on conflict handling strategy)
///     5. Caches successful responses (configurable status codes) for future duplicate requests
///     6. Uses a distributed cache for scalability across multiple application instances
/// </remarks>
internal sealed class IdempotencyEndpointFilter(
    IIdempotencyKeyStore store,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyEndpointFilter> logger) : IEndpointFilter
{
    #region Fields

    /// <summary>
    ///     Gets the configured idempotency options.
    /// </summary>
    private readonly IdempotencyOptions _options = options.Value;

    #endregion

    #region Methods

    /// <summary>
    ///     Serializes and caches the response for future duplicate requests.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context containing HTTP context.</param>
    /// <param name="resultValue">The response value to serialize and cache.</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <param name="compositeKey">The composite idempotency key.</param>
    /// <param name="idempotencyKey">The original idempotency key for logging.</param>
    /// <param name="requestId">The request identifier for logging.</param>
    /// <returns>A completed task.</returns>
    private async ValueTask CacheResponseAsync(
        EndpointFilterInvocationContext context,
        object? resultValue,
        int? statusCode,
        string compositeKey,
        string idempotencyKey,
        string requestId)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var serializedResult = JsonSerializer.Serialize(
                resultValue,
                _options.JsonSerializerOptions);

            var cachedResponse = new CachedResponse
            {
                StatusCode = statusCode ?? 200,
                Body = serializedResult,
                ContentType = context.HttpContext.Response.ContentType ?? "application/json",
                CreatedAt = now,
                ExpiresAt = now.Add(_options.Expiration)
            };

            await store.MarkKeyAsProcessedAsync(compositeKey, cachedResponse).ConfigureAwait(false);
            logger.LogInformation(
                "RequestId={RequestId}: Response cached. Key={Key}, StatusCode={StatusCode}, SerializedSize={Size}",
                requestId,
                idempotencyKey,
                statusCode,
                serializedResult.Length);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "RequestId={RequestId}: Failed to serialize response for caching. Key={Key}, Type={ResponseType}. " +
                "Marking as processed without caching result.",
                requestId,
                idempotencyKey,
                resultValue?.GetType().Name ?? "unknown");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "RequestId={RequestId}: Unexpected error while caching response for idempotency key={Key}. " +
                "Continuing without cache.",
                requestId,
                idempotencyKey);
        }
    }

    /// <summary>
    ///     Caches the response if the status code is configured for caching.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context containing HTTP context.</param>
    /// <param name="result">The result from the endpoint handler.</param>
    /// <param name="compositeKey">The composite idempotency key for caching.</param>
    /// <param name="idempotencyKey">The original idempotency key for logging.</param>
    /// <param name="requestId">The request identifier for logging.</param>
    /// <returns>A completed task.</returns>
    private async ValueTask CacheResponseIfApplicableAsync(
        EndpointFilterInvocationContext context,
        object? result,
        string compositeKey,
        string idempotencyKey,
        string requestId)
    {
        var resultValue = result is null ? null : result.GetPropertyValue("Value") ?? result;
        var statusCode = result is IStatusCodeHttpResult r ? r.StatusCode : context.HttpContext.Response.StatusCode;

        // Check if status code should be cached
        if (!ShouldCacheStatusCode(statusCode ?? StatusCodes.Status200OK))
        {
            logger.LogDebug(
                "RequestId={RequestId}: Not caching response. StatusCode={StatusCode} is not configured for caching. Key={Key}",
                requestId,
                statusCode,
                idempotencyKey);
            return;
        }

        await CacheResponseAsync(context, resultValue, statusCode, compositeKey, idempotencyKey, requestId)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Creates a composite idempotency key from HTTP method, route template, and idempotency key.
    ///     This ensures uniqueness across different endpoints and HTTP methods.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="idempotencyKey">The idempotency key from the request header.</param>
    /// <param name="requestId">The request identifier for logging.</param>
    /// <returns>The composite key in the format "{METHOD}:{ROUTE}_{KEY}".</returns>
    private string CreateCompositeKey(EndpointFilterInvocationContext context, string idempotencyKey, string requestId)
    {
        var httpMethod = context.HttpContext.Request.Method;
        var endpoint = context.HttpContext.GetEndpoint();

        // Try to get route template from endpoint metadata
        // For minimal APIs, RouteAttribute may not be populated, so fallback to Request.Path
        var routeTemplate = endpoint?.Metadata.GetMetadata<RouteAttribute>()?.Template
                            ?? context.HttpContext.Request.Path.Value
                            ?? "/";

        // Create composite key with HTTP method to ensure uniqueness across different endpoint operations
        var compositeKey = $"{httpMethod}:{routeTemplate}_{idempotencyKey}".ToUpperInvariant();

        logger.LogDebug(
            "RequestId={RequestId}: Created composite idempotency key. Method={Method}, Route={Route}, CompositeKey={CompositeKey}",
            requestId,
            httpMethod,
            routeTemplate,
            compositeKey);

        return compositeKey;
    }

    /// <summary>
    ///     Handles a duplicate request by either returning a 409 Conflict or the cached response.
    /// </summary>
    /// <param name="existingResult">The result from the cache containing the duplicate flag and cached response.</param>
    /// <param name="idempotencyKey">The idempotency key for logging.</param>
    /// <param name="requestId">The request identifier for logging.</param>
    /// <returns>A problem response or the cached response text result.</returns>
    private object? HandleDuplicateRequest(
        (bool processed, CachedResponse? response) existingResult,
        string idempotencyKey,
        string requestId)
    {
        logger.LogInformation(
            "RequestId={RequestId}: Duplicate request detected. Key={Key}, Strategy={Strategy}",
            requestId,
            idempotencyKey,
            _options.ConflictHandling);

        if (_options.ConflictHandling == IdempotentConflictHandling.ConflictResponse)
        {
            logger.LogWarning(
                "RequestId={RequestId}: Returning 409 Conflict for duplicate request. Key={Key}",
                requestId,
                idempotencyKey);
            return TypedResults.Problem(
                $"The request with the same idempotent key `{idempotencyKey}` has already been processed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (existingResult.response is null)
        {
            logger.LogWarning(
                "RequestId={RequestId}: Cached response metadata missing for idempotency key: {Key}",
                requestId,
                idempotencyKey);
            return TypedResults.Problem(
                $"The request with the same idempotent key `{idempotencyKey}` has already been processed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        logger.LogInformation(
            "RequestId={RequestId}: Returning cached response for duplicate request. Key={Key}, StatusCode={StatusCode}",
            requestId,
            idempotencyKey,
            existingResult.response.StatusCode);

        return TypedResults.Text(
            existingResult.response.Body ?? string.Empty,
            existingResult.response.ContentType,
            Encoding.UTF8,
            existingResult.response.StatusCode);
    }

    /// <summary>
    ///     Invokes the idempotency endpoint filter.
    ///     This method intercepts the endpoint invocation to implement idempotent request processing.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context containing the HTTP context and endpoint metadata.</param>
    /// <param name="next">The next endpoint filter or endpoint handler delegate in the pipeline.</param>
    /// <returns>
    ///     The result from the endpoint handler if the key is new, or:
    ///     - A cached response if the key was already processed and conflict handling is set to return cached results
    ///     - A 409 Conflict problem response if the key was already processed and conflict handling is set to return conflicts
    ///     - A 400 Bad Request if validation fails (missing, invalid format, or exceeds length)
    /// </returns>
    /// <remarks>
    ///     The filter creates a composite key from the HTTP method, route template, and idempotency key to support
    ///     the same key being used across different endpoints and methods. Only responses with configurable status codes
    ///     are cached to prevent caching error responses.
    /// </remarks>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var requestId = context.HttpContext.TraceIdentifier;
        var idempotencyKey = context.HttpContext.Request.Headers[_options.IdempotencyHeaderKey].FirstOrDefault();

        // Validate idempotency key
        var validationResult = ValidateIdempotencyKey(idempotencyKey, requestId);
        if (validationResult is not null) return validationResult;

        // Create composite key
        var compositeKey = CreateCompositeKey(context, idempotencyKey!, requestId);

        // Check for duplicate request
        var existingResult = await store.IsKeyProcessedAsync(compositeKey).ConfigureAwait(false);
        if (existingResult.processed) return HandleDuplicateRequest(existingResult, idempotencyKey!, requestId);

        logger.LogDebug(
            "RequestId={RequestId}: New request detected, proceeding with processing. Key={Key}",
            requestId,
            idempotencyKey);

        // Process the request
        var result = await next(context).ConfigureAwait(false);

        // Cache the response if appropriate
        await CacheResponseIfApplicableAsync(context, result, compositeKey, idempotencyKey!, requestId)
            .ConfigureAwait(false);

        logger.LogDebug("RequestId={RequestId}: Returning result to the client", requestId);
        return result;
    }

    /// <summary>
    ///     Determines if a given HTTP status code should be cached based on configuration.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to check.</param>
    /// <returns>True if the status code is within the cacheable range or in additional cacheable codes; false otherwise.</returns>
    private bool ShouldCacheStatusCode(int statusCode)
    {
        if (statusCode >= _options.MinStatusCodeForCaching && statusCode <= _options.MaxStatusCodeForCaching)
            return true;

        return _options.AdditionalCacheableStatusCodes.Contains(statusCode);
    }

    /// <summary>
    ///     Validates the idempotency key with three-layer validation (presence, length, format).
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to validate.</param>
    /// <param name="requestId">The request identifier for logging.</param>
    /// <returns>A problem response if validation fails; otherwise null.</returns>
    private object? ValidateIdempotencyKey(string? idempotencyKey, string requestId)
    {
        // Validate header presence
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            logger.LogWarning(
                "RequestId={RequestId}: Idempotency header '{HeaderName}' is missing. Returning 400 Bad Request.",
                requestId,
                _options.IdempotencyHeaderKey);
            return TypedResults.Problem(
                $"The '{_options.IdempotencyHeaderKey}' header is required for idempotent requests.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.1");
        }

        // Validate key length
        if (idempotencyKey.Length > _options.MaxIdempotencyKeyLength)
        {
            logger.LogWarning(
                "RequestId={RequestId}: Idempotency key exceeds maximum length of {MaxLength}. Key length: {ActualLength}",
                requestId,
                _options.MaxIdempotencyKeyLength,
                idempotencyKey.Length);
            return TypedResults.Problem(
                $"Idempotency key must not exceed {_options.MaxIdempotencyKeyLength} characters.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.1");
        }

        // Validate key format
        if (!Regex.IsMatch(idempotencyKey, _options.IdempotencyKeyPattern))
        {
            logger.LogWarning(
                "RequestId={RequestId}: Idempotency key format is invalid. Key must match pattern: {Pattern}",
                requestId,
                _options.IdempotencyKeyPattern);
            return TypedResults.Problem(
                "Idempotency key format is invalid. Allowed characters: alphanumeric, hyphens, underscores.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.1");
        }

        logger.LogDebug(
            "RequestId={RequestId}: Validated idempotency key. HeaderKey={HeaderKey}, KeyLength={KeyLength}",
            requestId,
            _options.IdempotencyHeaderKey,
            idempotencyKey.Length);

        return null;
    }

    #endregion
}