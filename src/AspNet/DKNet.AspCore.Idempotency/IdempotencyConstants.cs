// <copyright file="IdempotencyConstants.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.AspCore.Idempotency;

/// <summary>
///     Constants used throughout the idempotency library.
/// </summary>
public static class IdempotencyConstants
{
    // Cache Key Prefixes
    /// <summary>
    ///     Prefix for idempotency cache keys.
    /// </summary>
    public const string CacheKeyPrefix = "idempotency:";

    /// <summary>
    ///     Error message when a concurrent request is in progress.
    /// </summary>
    public const string ConflictError = "A request with this idempotency key is already in progress.";

    // Request Headers
    /// <summary>
    ///     The default HTTP header name for idempotency keys.
    /// </summary>
    public const string DefaultHeaderName = "Idempotency-Key";

    /// <summary>
    ///     Header name indicating when the cached response expires.
    /// </summary>
    public const string ExpiresHeader = "Idempotency-Key-Expires";

    /// <summary>
    ///     Error message when request body fingerprint does not match.
    /// </summary>
    public const string FingerprintMismatchError =
        "Request body does not match original request for this idempotency key.";

    /// <summary>
    ///     Error message when idempotency key format is invalid.
    /// </summary>
    public const string InvalidKeyError =
        "Idempotency key format is invalid. Key must be 1-256 alphanumeric characters, hyphens, or underscores.";

    /// <summary>
    ///     Prefix for distributed lock keys.
    /// </summary>
    public const string LockKeyPrefix = "idempotency:lock:";

    // Error Messages
    /// <summary>
    ///     Error message when idempotency key is missing.
    /// </summary>
    public const string MissingKeyError = "Idempotency key is required for this endpoint.";

    /// <summary>
    ///     HTTP header for specifying retry delay.
    /// </summary>
    public const string RetryAfterHeader = "Retry-After";

    /// <summary>
    ///     Status value indicating a cached response was returned.
    /// </summary>
    public const string StatusCached = "cached";

    // Status Values
    /// <summary>
    ///     Status value indicating a fresh request execution.
    /// </summary>
    public const string StatusCreated = "created";

    // Response Headers
    /// <summary>
    ///     Header name indicating idempotency status (created or cached).
    /// </summary>
    public const string StatusHeader = "Idempotency-Key-Status";
}