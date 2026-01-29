// <copyright file="CachedResponse.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.AspCore.Idempotency;

/// <summary>
///     Represents a cached HTTP response for idempotent request replay.
/// </summary>
public sealed record CachedResponse
{
    /// <summary>
    ///     Gets the HTTP status code of the original response.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    ///     Gets the response body as a JSON string.
    /// </summary>
    public required string? Body { get; init; }

    /// <summary>
    ///     Gets the content type of the response.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    ///     Gets the timestamp when the response was cached (UTC).
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    ///     Gets the timestamp when the cached response expires (UTC).
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    ///     Gets the optional SHA256 hash of the original request body for fingerprinting.
    /// </summary>
    public string? RequestBodyHash { get; init; }

    /// <summary>
    ///     Determines whether the cached response has expired based on the current UTC time.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}