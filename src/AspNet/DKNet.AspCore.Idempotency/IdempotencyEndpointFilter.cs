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
///     2. Checks if the key has been processed before
///     3. Returns cached results for duplicate requests (based on conflict handling strategy)
///     4. Caches successful responses (2xx status codes) for future duplicate requests
///     5. Uses a distributed cache for scalability across multiple application instances
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
    ///     Invokes the idempotency endpoint filter.
    ///     This method intercepts the endpoint invocation to implement idempotent request processing.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context containing the HTTP context and endpoint metadata.</param>
    /// <param name="next">The next endpoint filter or endpoint handler delegate in the pipeline.</param>
    /// <returns>
    ///     The result from the endpoint handler if the key is new, or:
    ///     - A cached response if the key was already processed and conflict handling is set to return cached results
    ///     - A 409 Conflict problem response if the key was already processed and conflict handling is set to return conflicts
    ///     - A 400 Bad Request if the idempotency header is missing
    /// </returns>
    /// <remarks>
    ///     The filter creates a composite key from the route template and idempotency key to support
    ///     the same key being used across different endpoints. Only successful responses (2xx status codes)
    ///     are cached to prevent caching error responses.
    /// </remarks>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var idempotencyKey = context.HttpContext.Request.Headers[_options.IdempotencyHeaderKey].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            logger.LogWarning("Idempotency header key is missing. Returning 400 Bad Request.");
            return TypedResults.Problem($"{_options.IdempotencyHeaderKey} header is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        logger.LogDebug("Checking idempotency header key: {Key}", _options.IdempotencyHeaderKey);

        var endpoint = context.HttpContext.GetEndpoint();
        var routeTemplate = endpoint?.Metadata.GetMetadata<RouteAttribute>()?.Template ??
                            context.HttpContext.Request.Path;
        var compositeKey = $"{routeTemplate}_{idempotencyKey}";

        var existingResult = await store.IsKeyProcessedAsync(compositeKey).ConfigureAwait(false);
        if (existingResult.processed)
        {
            logger.LogInformation("Existing result found for idempotency key: {Key}", idempotencyKey);

            if (_options.ConflictHandling == IdempotentConflictHandling.ConflictResponse)
            {
                logger.LogWarning("Returning 409 Conflict.");
                return TypedResults.Problem(
                    $"The request with the same idempotent key `{idempotencyKey}` has already been processed.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            if (existingResult.response is null)
            {
                logger.LogWarning("Cached response metadata missing for idempotency key: {Key}", idempotencyKey);
                return TypedResults.Problem(
                    $"The request with the same idempotent key `{idempotencyKey}` has already been processed.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return TypedResults.Text(
                existingResult.response.Body ?? string.Empty,
                existingResult.response.ContentType,
                Encoding.UTF8,
                existingResult.response.StatusCode);
        }

        // Process the request
        var result = await next(context).ConfigureAwait(false);

        var resultValue = result is null ? null : result.GetPropertyValue("Value") ?? result;
        var statusCode = result is IStatusCodeHttpResult r ? r.StatusCode : context.HttpContext.Response.StatusCode;

        // Only cache successful responses (2xx)
        if (statusCode is >= 200 and < 300)
        {
            var now = DateTimeOffset.UtcNow;
            var cachedResponse = new CachedResponse
            {
                StatusCode = statusCode ?? 200,
                Body = resultValue is null
                    ? null
                    : JsonSerializer.Serialize(resultValue, _options.JsonSerializerOptions),
                ContentType = context.HttpContext.Response.ContentType ?? "application/json",
                CreatedAt = now,
                ExpiresAt = now.Add(_options.Expiration),
                RequestBodyHash = null
            };

            await store.MarkKeyAsProcessedAsync(compositeKey, cachedResponse).ConfigureAwait(false);
            logger.LogInformation("Caching the response for idempotency key: {Key}", idempotencyKey);
        }

        logger.LogDebug("Returning result to the client");
        return result;
    }

    #endregion
}