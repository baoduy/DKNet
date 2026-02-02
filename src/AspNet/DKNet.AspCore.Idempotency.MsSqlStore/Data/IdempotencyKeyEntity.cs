// <copyright file="IdempotencyKey.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace DKNet.AspCore.Idempotency.MsSqlStore.Data;

/// <summary>
///     Represents an idempotency key stored in the database with its associated cached HTTP response.
///     This entity enables persistent, reliable idempotency across application restarts and distributed environments.
/// </summary>
internal sealed class IdempotencyKeyEntity
{
    #region Constructors

    public IdempotencyKeyEntity(IdempotentKeyInfo info, CachedResponse item)
    {
        if (string.IsNullOrEmpty(info.IdempotentKey))
            throw new ArgumentException("IdempotentKey cannot be null or empty", nameof(info));

        Id = Guid.CreateVersion7();
        IdempotentKey = info.IdempotentKey;
        Endpoint = info.Endpoint;
        Method = info.Method;
        CompositeKey = SanitizeKey(info.CompositeKey);
        ContentType = item.ContentType;
        CreatedAt = item.CreatedAt;
        ExpiresAt = item.ExpiresAt;
        Body = item.Body;
        StatusCode = item.StatusCode;
    }

    private IdempotencyKeyEntity()
    {
        IdempotentKey = string.Empty;
        Endpoint = string.Empty;
        Method = string.Empty;
        CompositeKey = string.Empty;
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets or sets the cached response body (typically JSON, serialized as string).
    ///     Maximum length: 1MB.
    /// </summary>
    public string? Body { get; private set; }

    /// <summary>
    ///     Gets or sets the user-provided idempotency key (typically a UUID or custom alphanumeric string).
    ///     Maximum length: 128 characters.
    /// </summary>
    public string CompositeKey { get; private set; }

    /// <summary>
    ///     Gets or sets the MIME type of the cached response (e.g., "application/json").
    ///     Maximum length: 256 characters.
    /// </summary>
    public string? ContentType { get; private set; }

    /// <summary>
    ///     Gets or sets the UTC timestamp when the idempotency key was first processed.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    [MaxLength(250)] public string Endpoint { get; private set; }

    /// <summary>
    ///     Gets or sets the UTC timestamp when the idempotency key expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    ///     Gets or sets the unique identifier for this idempotency record.
    /// </summary>
    public Guid Id { get; private set; }

    [MaxLength(150)] public string IdempotentKey { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether this idempotency key has expired.
    /// </summary>
    [NotMapped]
    public bool IsExpired => ExpiresAt is not null && DateTime.UtcNow >= ExpiresAt;

    [MaxLength(20)] public string Method { get; private set; }

    /// <summary>
    ///     Gets or sets the HTTP response status code from the cached response (100-599).
    /// </summary>
    public int StatusCode { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    ///     Sanitizes an idempotency key for use as a database key.
    ///     Removes invalid characters to prevent injection attacks.
    /// </summary>
    /// <param name="key">The idempotency key to sanitize.</param>
    /// <returns>A sanitized key with only alphanumeric characters and hyphens.</returns>
    internal static string SanitizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Idempotency key cannot be null or empty.", nameof(key));

        // Remove non-alphanumeric characters except hyphens
        var sanitized = Regex.Replace(key, @"[^a-zA-Z0-9\-]", string.Empty);

        if (string.IsNullOrEmpty(sanitized))
            throw new ArgumentException("Idempotency key contains no valid characters.", nameof(key));

        if (sanitized.Length > 128) sanitized = sanitized[..128];

        return sanitized.ToUpperInvariant();
    }

    #endregion
}