using System.Text;
using System.Text.Json;
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
    /// <param name="keyInfo">The composite idempotency key.</param>
    /// <param name="requestId">The request identifier for logging.</param>
    /// <returns>A completed task.</returns>
    private async ValueTask CacheResponseAsync(
        EndpointFilterInvocationContext context,
        object? resultValue,
        int? statusCode,
        IdempotentKeyInfo keyInfo,
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

            await store.MarkKeyAsProcessedAsync(keyInfo, cachedResponse).ConfigureAwait(false);
            logger.LogInformation(
                "RequestId={RequestId}: Response cached. Key={Key}, StatusCode={StatusCode}, SerializedSize={Size}",
                requestId,
                keyInfo.IdempotentKey,
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
                keyInfo.IdempotentKey,
                resultValue?.GetType().Name ?? "unknown");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "RequestId={RequestId}: Unexpected error while caching response for idempotency key={Key}. " +
                "Continuing without cache.",
                requestId,
                keyInfo.IdempotentKey);
        }
    }

    /// <summary>
    ///     Caches the response if the status code is configured for caching.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context containing HTTP context.</param>
    /// <param name="result">The result from the endpoint handler.</param>
    /// <param name="keyInfo">The composite idempotency key for caching.</param>
    /// <param name="requestId">The request identifier for logging.</param>
    /// <returns>A completed task.</returns>
    private async ValueTask CacheResponseIfApplicableAsync(
        EndpointFilterInvocationContext context,
        object? result,
        IdempotentKeyInfo keyInfo,
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
                keyInfo.IdempotentKey);
            return;
        }

        await CacheResponseAsync(context, resultValue, statusCode, keyInfo, requestId)
            .ConfigureAwait(false);
    }

    private IdempotentKeyInfo GetIdempotentKeyInfo(EndpointFilterInvocationContext context)
    {
        var idempotencyKey = context.HttpContext.Request.Headers[_options.IdempotencyHeaderKey].FirstOrDefault();
        var endpoint = context.HttpContext.GetEndpoint();
        var routeTemplate = endpoint?.Metadata.GetMetadata<RouteAttribute>()?.Template
                            ?? context.HttpContext.Request.Path.Value
                            ?? "/";
        var httpMethod = context.HttpContext.Request.Method.ToUpperInvariant();

        return new IdempotentKeyInfo
        {
            IdempotentKey = idempotencyKey,
            Endpoint = routeTemplate,
            Method = httpMethod
        };
    }

    /// <summary>
    ///     Handles a duplicate request by either returning a 409 Conflict or the cached response.
    /// </summary>
    /// <param name="existingResult">The result from the cache containing the duplicate flag and cached response.</param>
    /// <param name="keyInfo">The idempotency key for logging.</param>
    /// <param name="requestId">The request identifier for logging.</param>
    /// <returns>A problem response or the cached response text result.</returns>
    private object? HandleDuplicateRequest(
        (bool processed, CachedResponse? response) existingResult,
        IdempotentKeyInfo keyInfo,
        string requestId)
    {
        logger.LogInformation(
            "RequestId={RequestId}: Duplicate request detected. Key={Key}, Strategy={Strategy}",
            requestId,
            keyInfo.IdempotentKey,
            _options.ConflictHandling);

        if (_options.ConflictHandling == IdempotentConflictHandling.ConflictResponse)
        {
            logger.LogWarning(
                "RequestId={RequestId}: Returning 409 Conflict for duplicate request. Key={Key}",
                requestId,
                keyInfo);
            return TypedResults.Problem(
                $"The request with the same idempotent key `{keyInfo}` has already been processed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (existingResult.response is null)
        {
            logger.LogWarning(
                "RequestId={RequestId}: Cached response metadata missing for idempotency key: {Key}",
                requestId,
                keyInfo.IdempotentKey);
            return TypedResults.Problem(
                $"The request with the same idempotent key `{keyInfo}` has already been processed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        logger.LogInformation(
            "RequestId={RequestId}: Returning cached response for duplicate request. Key={Key}, StatusCode={StatusCode}",
            requestId,
            keyInfo.IdempotentKey,
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
        var idempotencyKeyInfo = GetIdempotentKeyInfo(context);

        // Validate idempotency key
        var validationResult = ValidateIdempotencyKey(idempotencyKeyInfo, requestId);
        if (validationResult is not null) return validationResult;

        // Check for duplicate request
        var existingResult = await store.IsKeyProcessedAsync(idempotencyKeyInfo).ConfigureAwait(false);
        if (existingResult.processed) return HandleDuplicateRequest(existingResult, idempotencyKeyInfo, requestId);

        logger.LogDebug(
            "RequestId={RequestId}: New request detected, proceeding with processing. Key={Key}",
            requestId,
            idempotencyKeyInfo.IdempotentKey);

        // Process the request
        var result = await next(context).ConfigureAwait(false);

        // Cache the response if appropriate
        await CacheResponseIfApplicableAsync(context, result, idempotencyKeyInfo, requestId)
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
    private object? ValidateIdempotencyKey(IdempotentKeyInfo info, string requestId)
    {
        var rs = info.IsValid(_options);
        if (rs.IsFailed)
        {
            logger.LogWarning(
                "RequestId={RequestId}: Idempotency header '{HeaderName}' is invalid: {Error}.",
                requestId,
                _options.IdempotencyHeaderKey,
                rs.Errors[0].Message);

            return TypedResults.Problem(
                $"The '{_options.IdempotencyHeaderKey}' header is invalid.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }

    #endregion
}