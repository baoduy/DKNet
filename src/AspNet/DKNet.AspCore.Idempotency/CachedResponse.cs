// <copyright file="CachedResponse.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.AspCore.Idempotency;

/// <summary>
///     Represents a cached HTTP response for idempotent request replay.
///     This record stores the complete response metadata to allow accurate replay of the original response.
/// </summary>
public sealed record CachedResponse
{
    /// <summary>
    ///     Gets the HTTP status code of the original response.
    ///     This is critical for idempotent replay to ensure the client receives the same status code.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    ///     Gets the response body as a JSON string.
    ///     Can be null for responses with no body (e.g., 204 No Content).
    /// </summary>
    public required string? Body { get; init; }

    /// <summary>
    ///     Gets the content type of the response.
    ///     Defaults to "application/json" for most API responses.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    ///     Gets the timestamp when the response was cached (UTC).
    ///     Useful for monitoring and debugging cache behavior.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    ///     Gets the timestamp when the cached response expires (UTC).
    ///     The cache system will remove or ignore entries after this timestamp.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    ///     Gets the optional hash of the request body for fingerprinting.
    ///     Can be used to detect if a duplicate request has a different payload.
    /// </summary>
    public string? RequestBodyHash { get; init; }

    /// <summary>
    ///     Determines if the cached response has expired based on the current UTC time.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}