// <copyright file="StringCreator.cs" company="https://drunkcoding.net">
// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Security.Cryptography;

namespace DKNet.RandomCreator;

/// <summary>
///     Random String generator.
/// </summary>
/// <param name="bufferLength">The length of the string.</param>
/// <param name="options">the option of the generation.</param>
internal sealed class StringCreator(int bufferLength, StringCreatorOptions options)
{
    #region Fields

    private const string DefaultChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string DefaultNumbers = "1234567890";
    private const string DefaultSymbols = "!@#$%^&*()-_=+[]{}|;:',.<>/?`~";

    #endregion

    #region Methods

    /// <summary>
    ///     To a character array.
    /// </summary>
    /// <returns>character array.</returns>
    /// <exception cref="ArgumentException">The exception if the options are invalid.</exception>
    public char[] ToChars()
    {
        // Prepare result buffer
        if (bufferLength <= 0) throw new ArgumentException("Length must be greater than zero.", nameof(bufferLength));

        if (options.MinNumbers + options.MinSpecials >= bufferLength)
            throw new ArgumentException(
                "The sum of MinNumbers and MinSpecials must be less than the total length.",
                nameof(options));

        // One buffer for the whole result: each segment is drawn straight into its slice, and
        // Shuffle mutates it in place, so there is only ever the one allocation returned to the caller.
        var buffer = new char[bufferLength];
        var offset = 0;

        if (options.MinNumbers > 0)
        {
            RandomNumberGenerator.GetItems<char>(DefaultNumbers, buffer.AsSpan(offset, options.MinNumbers));
            offset += options.MinNumbers;
        }

        if (options.MinSpecials > 0)
        {
            RandomNumberGenerator.GetItems<char>(DefaultSymbols, buffer.AsSpan(offset, options.MinSpecials));
            offset += options.MinSpecials;
        }

        RandomNumberGenerator.GetItems<char>(DefaultChars, buffer.AsSpan(offset, bufferLength - offset));

        RandomNumberGenerator.Shuffle(buffer.AsSpan());
        return buffer;
    }

    /// <summary>
    ///     To string.
    /// </summary>
    /// <returns>The generated string.</returns>
    public override string ToString()
    {
        var chars = ToChars();
        return new string(chars);
    }

    #endregion
}