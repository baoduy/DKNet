// <copyright file="CreateItemResponse.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace AspCore.Idempotency.ApiTests;

/// <summary>
///     Response model for item creation.
/// </summary>
public sealed class CreateItemResponse
{
    #region Properties

    /// <summary>
    ///     Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    ///     Gets or sets the created item ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     Gets or sets the item name.
    /// </summary>
    public required string Name { get; set; }

    #endregion
}