// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DKNet.RandomCreator;

/// <summary>
///     Options for configuring the StringCreator random string generator.
/// </summary>
public sealed class StringCreatorOptions
{
    #region Properties

    // Commented out for future use
    // /// <summary>
    // /// If true, only alphabetic characters will be used.
    // /// </summary>
    // public bool AlphabeticOnly { get; set; }

    /// <summary>
    ///     Gets or sets minimum number of numeric characters required.
    /// </summary>
    public int MinNumbers { get; set; }

    /// <summary>
    ///     Gets or sets minimum number of special characters required.
    /// </summary>
    public int MinSpecials { get; set; }

    #endregion
}