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
internal sealed class StringCreator(int bufferLength, StringCreatorOptions options) : IDisposable
{
    #region Fields

    private const string DefaultChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string DefaultNumbers = "1234567890";
    private const string DefaultSymbols = "!@#$%^&*()-_=+[]{{}}|;:',.<>/?`~";

    private readonly RandomNumberGenerator _cryptoGen = RandomNumberGenerator.Create();
    private bool _disposed;

    #endregion

    #region Methods

    /// <inheritdoc />
    public void Dispose()
    {
        this._cryptoGen.Dispose();
        this._disposed = true;
    }

    private char[] Generate(string validChars, int length)
    {
        ObjectDisposedException.ThrowIf(this._disposed, nameof(StringCreator));

        var buffer = new byte[length * 8];
        this._cryptoGen.GetBytes(buffer);
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            var rnd = BitConverter.ToUInt64(buffer, i * 8);
            result[i] = validChars[(int)(rnd % (uint)validChars.Length)];
        }

        return result;
    }

    /// <summary>
    ///     To a character array.
    /// </summary>
    /// <returns>character array.</returns>
    /// <exception cref="ArgumentException">The exception if the options are invalid.</exception>
    public char[] ToChars()
    {
        // Prepare result list
        if (bufferLength <= 0)
        {
            throw new ArgumentException("Length must be greater than zero.", nameof(bufferLength));
        }

        if (options.MinNumbers + options.MinSpecials >= bufferLength)
        {
            throw new ArgumentException(
                "The sum of MinNumbers and MinSpecials must be less than the total length.",
                nameof(options));
        }

        var result = new List<char>(bufferLength);

        // Add minimum numbers
        result.AddRange(
            options.MinNumbers <= 0
                ? []
                : this.Generate(DefaultNumbers, options.MinNumbers));

        // Add minimum specials
        result.AddRange(
            options.MinSpecials <= 0
                ? []
                : this.Generate(DefaultSymbols, options.MinSpecials));

        // Fill the rest
        var remaining = bufferLength - result.Count;
        result.AddRange(this.Generate(DefaultChars, remaining));

        var array = result.ToArray();
        var span = array.AsSpan();
        RandomNumberGenerator.Shuffle(span);
        return span.ToArray();
    }

    /// <summary>
    ///     To string.
    /// </summary>
    /// <returns>The generated string.</returns>
    public override string ToString()
    {
        var chars = this.ToChars();
        return new string(chars);
    }

    #endregion
}