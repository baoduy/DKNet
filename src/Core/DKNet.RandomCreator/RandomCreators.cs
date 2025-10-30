// <copyright file="RandomCreators.cs" company="https://drunkcoding.net">
// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.RandomCreator;

/// <summary>
///     Provides utility methods for generating random characters and strings.
/// </summary>
public static class RandomCreators
{
    #region Methods

    /// <summary>
    ///     Creates a new character array with random characters.
    /// </summary>
    /// <param name="length">The length of the character array to generate. Default is 25.</param>
    /// <param name="options">Optional configuration options for string generation.</param>
    /// <returns>A character array containing randomly generated characters.</returns>
    public static char[] NewChars(int length = 25, StringCreatorOptions? options = null)
    {
        using var gen = new StringCreator(
            length,
            options ?? new StringCreatorOptions());

        return gen.ToChars();
    }

    /// <summary>
    ///     Creates a new string with random characters.
    /// </summary>
    /// <param name="length">The length of the string to generate. Default is 25.</param>
    /// <param name="options">Optional configuration options for string generation.</param>
    /// <returns>A string containing randomly generated characters.</returns>
    public static string NewString(int length = 25, StringCreatorOptions? options = null)
    {
        using var gen = new StringCreator(
            length,
            options ?? new StringCreatorOptions());
        return gen.ToString();
    }

    #endregion
}