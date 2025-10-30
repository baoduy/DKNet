// <copyright file="EnumInfo.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace DKNet.Fw.Extensions;

/// <summary>
///     Represents information extracted from an enum value's display attributes.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record EnumInfo
{
    #region Properties

    /// <summary>
    ///     Gets or initializes the key (enum value name) of the enum value.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    ///     Gets or initializes the display name of the enum value.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets or initializes the description of the enum value.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Gets or initializes the group name of the enum value.
    /// </summary>
    public string? GroupName { get; init; }

    #endregion
}