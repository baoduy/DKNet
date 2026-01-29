// <copyright file="IdempotencyEndpointFilter.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Text.Json;
using DKNet.AspCore.Idempotency.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Idempotency.Filters;

/// <summary>
///     Endpoint filter that handles idempotency for API endpoints.
///     Caches responses and returns cached responses for duplicate requests with the same idempotency key.
/// </summary>
public sealed class IdempotencyEndpointFilter : IEndpointFilter
{
    #region Fields

    private readonly ILogger<IdempotencyEndpointFilter> _logger;
    private readonly IdempotencyOptions _options;

    private readonly IIdempotencyKeyRepository _repository;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="IdempotencyEndpointFilter" /> class.
    /// </summary>
    /// <param name="repository">The idempotency key repository.</param>
    /// <param name="options">The idempotency options.</param>
    /// <param name="logger">The logger.</param>
    public IdempotencyEndpointFilter(
        IIdempotencyKeyRepository repository,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyEndpointFilter> logger)
    {
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Applies idempotency status headers to the response.
    /// </summary>
    private static void ApplyResponseHeaders(
        HttpContext httpContext,
        IdempotencyKey key,
        CachedResponse? cachedResponse,
        bool isCached)
    {
        var status = isCached ? IdempotencyConstants.StatusCached : IdempotencyConstants.StatusCreated;
        httpContext.Response.Headers[IdempotencyConstants.StatusHeader] = status;

        if (cachedResponse is not null)
            httpContext.Response.Headers[IdempotencyConstants.ExpiresHeader] = cachedResponse.ExpiresAt.ToString("O");
    }

    /// <summary>
    ///     Caches the response from the endpoint execution.
    /// </summary>
    private async Task CacheResponseAsync(HttpContext httpContext, IdempotencyKey key, object? result,
        IdempotencyOptions options)
    {
        // Only cache successful or configured error responses
        var statusCode = httpContext.Response.StatusCode;
        var shouldCache = statusCode < 300 ||
                          (options.CacheErrorResponses && statusCode < 500);

        if (!shouldCache)
        {
            _logger.LogDebug("Response not cached due to status code: {StatusCode}", statusCode);
            return;
        }


        var body = result != null ? JsonSerializer.Serialize(result, options.JsonSerializerOptions) : null;

        // Check body size
        var bodySize = body?.Length ?? 0;
        if (bodySize > options.MaxBodySize)
        {
            _logger.LogWarning("Response body exceeds max size ({Size}), not caching", options.MaxBodySize);
            return;
        }

        var contentType = httpContext.Response.ContentType ?? "application/json";
        var expiresAt = DateTimeOffset.UtcNow.Add(options.Expiration);

        var cachedResponse = new CachedResponse
        {
            StatusCode = statusCode,
            Body = body,
            ContentType = contentType,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            RequestBodyHash = null // TODO: Implement fingerprinting
        };

        await _repository.SetAsync(key, cachedResponse, httpContext.RequestAborted)
            .ConfigureAwait(false);

        _logger.LogInformation("Response cached for idempotency key");
    }

    /// <summary>
    ///     Invokes the endpoint filter to handle idempotency.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="next">The next delegate to invoke.</param>
    /// <returns>The result of the endpoint invocation.</returns>
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Check for per-endpoint configuration override
        var options = _options;
        var endpointMetadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<IdempotencyEndpointMetadata>();
        if (endpointMetadata is not null)
        {
            // Apply per-endpoint configuration
            options = new IdempotencyOptions
            {
                IdempotencyHeaderKey = _options.IdempotencyHeaderKey,
                CachePrefix = _options.CachePrefix,
                Expiration = _options.Expiration,
                MaxKeyLength = _options.MaxKeyLength,
                JsonSerializerOptions = _options.JsonSerializerOptions,
                ConflictHandling = _options.ConflictHandling,
                CacheErrorResponses = _options.CacheErrorResponses,
                EnableFingerprinting = _options.EnableFingerprinting,
                LockTimeout = _options.LockTimeout,
                MaxBodySize = _options.MaxBodySize
            };
            endpointMetadata.Configure(options);
        }

        // Extract idempotency key from header
        var rawKey = httpContext.Request.Headers[options.IdempotencyHeaderKey].FirstOrDefault();

        if (string.IsNullOrEmpty(rawKey))
        {
            _logger.LogWarning("Idempotency key is missing from header: {Header}", options.IdempotencyHeaderKey);
            return TypedResults.Problem(
                IdempotencyConstants.MissingKeyError,
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Validate and create idempotency key
        if (!IdempotencyKey.TryCreate(rawKey, out var idempotencyKey, options.MaxKeyLength))
        {
            _logger.LogWarning("Invalid idempotency key format: {Header}", options.IdempotencyHeaderKey);
            return TypedResults.Problem(
                IdempotencyConstants.InvalidKeyError,
                statusCode: StatusCodes.Status400BadRequest);
        }

        _logger.LogDebug("Processing request with idempotency key");

        // Try to acquire lock for concurrent request handling
        var lockAcquired = await _repository.TryAcquireLockAsync(
                idempotencyKey,
                options.LockTimeout,
                httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!lockAcquired)
        {
            if (options.ConflictHandling == IdempotentConflictHandling.ConflictResponse)
            {
                _logger.LogWarning("Concurrent request detected, returning 409 Conflict");
                return TypedResults.Problem(
                    IdempotencyConstants.ConflictError,
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?>
                    {
                        [IdempotencyConstants.RetryAfterHeader] = "5"
                    });
            }

            // Wait and check cache
            await Task.Delay(100, httpContext.RequestAborted).ConfigureAwait(false);
            var cachedResult = await _repository.GetAsync(idempotencyKey, httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (cachedResult is not null)
            {
                ApplyResponseHeaders(httpContext, idempotencyKey, cachedResult, true);
                return cachedResult.Body;
            }

            // Still no lock, fail
            return TypedResults.Problem(
                "Could not process request. Please retry.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            // Check if response is already cached
            var cachedResponse = await _repository.GetAsync(idempotencyKey, httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (cachedResponse is not null && !cachedResponse.IsExpired)
            {
                _logger.LogInformation("Returning cached response for idempotency key");
                ApplyResponseHeaders(httpContext, idempotencyKey, cachedResponse, true);
                return cachedResponse.Body;
            }

            // Execute the endpoint
            _logger.LogDebug("Executing endpoint");
            var result = await next(context).ConfigureAwait(false);

            // Cache the response using per-endpoint options
            await CacheResponseAsync(httpContext, idempotencyKey, result, options)
                .ConfigureAwait(false);

            ApplyResponseHeaders(httpContext, idempotencyKey, null, false);
            return result;
        }
        finally
        {
            await _repository.ReleaseLockAsync(idempotencyKey, httpContext.RequestAborted)
                .ConfigureAwait(false);
        }
    }

    #endregion
}