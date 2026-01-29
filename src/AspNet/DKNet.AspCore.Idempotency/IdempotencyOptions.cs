// <copyright file="IdempotencyOptions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Text.Json;

namespace DKNet.AspCore.Idempotency;

/// <summary>
///     Defines how concurrent requests with the same idempotency key are handled.
/// </summary>
public enum IdempotentConflictHandling
{
    /// <summary>
    ///     Wait for the first request to complete and return its cached response.
    /// </summary>
    ReturnCachedResult,

    /// <summary>
    ///     Immediately return a 409 Conflict response.
    /// </summary>
    ConflictResponse
}

/// <summary>
///     Configuration options for idempotency behavior.
/// </summary>
public sealed class IdempotencyOptions
{
    #region Properties

    /// <summary>
    ///     Gets or sets whether to cache error responses (4xx, 5xx).
    ///     When false, only successful responses (2xx) are cached.
    ///     Default: false
    /// </summary>
    public bool CacheErrorResponses { get; set; }

    /// <summary>
    ///     Gets or sets the cache key prefix to prevent collisions.
    ///     Default: "idem"
    /// </summary>
    public string CachePrefix { get; set; } = "idem";

    /// <summary>
    ///     Gets or sets how to handle concurrent requests with the same idempotency key.
    ///     Default: ReturnCachedResult
    /// </summary>
    public IdempotentConflictHandling ConflictHandling { get; set; } = IdempotentConflictHandling.ReturnCachedResult;

    /// <summary>
    ///     Gets or sets whether to enable request body fingerprinting.
    ///     When enabled, subsequent requests with the same key but different body will return 422 Unprocessable Entity.
    ///     Default: false
    /// </summary>
    public bool EnableFingerprinting { get; set; }

    /// <summary>
    ///     Gets or sets the default time-to-live for cached responses.
    ///     Default: 24 hours
    /// </summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    ///     Gets or sets the HTTP header name to extract the idempotency key from.
    ///     Default: "Idempotency-Key"
    /// </summary>
    public string IdempotencyHeaderKey { get; set; } = IdempotencyConstants.DefaultHeaderName;

    /// <summary>
    ///     Gets or sets the JSON serialization options used for caching responses.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    ///     Gets or sets the maximum timeout for acquiring a distributed lock during concurrent request handling.
    ///     Default: 30 seconds
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Gets or sets the maximum request body size to cache (in bytes).
    ///     Larger bodies are not cached.
    ///     Default: 1 MB
    /// </summary>
    public int MaxBodySize { get; set; } = 1024 * 1024;

    /// <summary>
    ///     Gets or sets the maximum allowed length for idempotency keys.
    ///     Default: 256 characters
    /// </summary>
    public int MaxKeyLength { get; set; } = 256;

    #endregion
}