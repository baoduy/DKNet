// <copyright file="IdempotencyKey.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.AspCore.Idempotency.MsSqlStore.Data;

/// <summary>
///     Represents an idempotency key stored in the database with its associated cached HTTP response.
///     This entity enables persistent, reliable idempotency across application restarts and distributed environments.
/// </summary>
internal sealed class IdempotencyKeyEntity
{
    #region Constructors

    public IdempotencyKeyEntity(string key, CachedResponse item)
    {
        Id = Guid.CreateVersion7();
        Key = key;
        ContentType = item.ContentType;
        CreatedAt = item.CreatedAt;
        ExpiresAt = item.ExpiresAt;
        Body = item.Body;
        StatusCode = item.StatusCode;
    }

    private IdempotencyKeyEntity() => Key = string.Empty;

    #endregion

    #region Properties

    /// <summary>
    ///     Gets or sets the cached response body (typically JSON, serialized as string).
    ///     Maximum length: 1MB.
    /// </summary>
    public string? Body { get; private set; }

    /// <summary>
    ///     Gets or sets the MIME type of the cached response (e.g., "application/json").
    ///     Maximum length: 256 characters.
    /// </summary>
    public string? ContentType { get; private set; }

    /// <summary>
    ///     Gets or sets the UTC timestamp when the idempotency key was first processed.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    ///     Gets or sets the UTC timestamp when the idempotency key expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    ///     Gets or sets the unique identifier for this idempotency record.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether this idempotency key has expired.
    /// </summary>
    public bool IsExpired => ExpiresAt is not null && DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    ///     Gets or sets the user-provided idempotency key (typically a UUID or custom alphanumeric string).
    ///     Maximum length: 128 characters.
    /// </summary>
    public string Key { get; private set; }

    /// <summary>
    ///     Gets or sets the HTTP response status code from the cached response (100-599).
    /// </summary>
    public int StatusCode { get; private set; }

    #endregion
}