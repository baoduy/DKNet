// <copyright file="CreateItemRequest.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace AspCore.Idempotency.ApiTests;

/// <summary>
///     Request model for creating an item.
/// </summary>
public sealed class CreateItemRequest
{
    #region Properties

    /// <summary>
    ///     Gets or sets the item name.
    /// </summary>
    public required string Name { get; set; }

    #endregion
}