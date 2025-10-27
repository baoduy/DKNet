using System.Text;
using System.Text.Json;
using SlimBus.Share.Extensions;

namespace SlimBus.Api.Configs.Idempotency;

/// <summary>
///     Filter to handle idempotency for API endpoints.
/// </summary>
internal sealed class IdempotencyEndpointFilter(
    IIdempotencyKeyRepository cacher,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyEndpointFilter> logger) : IEndpointFilter
{
    #region Fields

    private readonly IdempotencyOptions _options = options.Value;

    #endregion

    #region Methods

    /// <summary>
    ///     Invokes the endpoint filter to handle idempotency.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="next">The next delegate to invoke.</param>
    /// <returns>The result of the endpoint invocation.</returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var idempotencyKey = context.HttpContext.Request.Headers[_options.IdempotencyHeaderKey].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            logger.LogWarning("Idempotency header key is missing. Returning 400 Bad Request.");
            return TypedResults.Problem($"{_options.IdempotencyHeaderKey} header is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        idempotencyKey = idempotencyKey.SanitizeForLogging(); // Sanitize user input
        logger.LogDebug("Checking idempotency header key: {Key}", _options.IdempotencyHeaderKey);

        var endpoint = context.HttpContext.GetEndpoint();
        var routeTemplate = endpoint?.Metadata.GetMetadata<RouteAttribute>()?.Template ??
                            context.HttpContext.Request.Path;
        var compositeKey = $"{routeTemplate}_{idempotencyKey}";

        var existingResult = await cacher.IsKeyProcessedAsync(compositeKey);
        if (existingResult.processed)
        {
            logger.LogInformation("Existing result found for idempotency key: {Key}", idempotencyKey);

            if (_options.ConflictHandling == IdempotentConflictHandling.ConflictResponse)
            {
                logger.LogWarning("Returning 409 Conflict.");
                return TypedResults.Problem("The request has already been processed.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return TypedResults.Text(existingResult.result!, "application/json", Encoding.UTF8);
        }

        // Process the request
        var result = await next(context);

        // Cache the response to prevent duplicate processing
        if (result != null)
        {
            var resultValue = result.GetPropertyValue("Value") ?? result;
            await cacher.MarkKeyAsProcessedAsync(compositeKey,
                JsonSerializer.Serialize(resultValue, _options.JsonSerializerOptions));
            logger.LogInformation("Caching the response for idempotency key: {Key}", idempotencyKey);
        }

        logger.LogDebug("Returning result to the client");
        return result;
    }

    #endregion
}