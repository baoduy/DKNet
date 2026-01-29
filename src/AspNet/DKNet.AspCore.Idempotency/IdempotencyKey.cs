// <copyright file="IdempotencyKey.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Text.RegularExpressions;

namespace DKNet.AspCore.Idempotency;

/// <summary>
///     Represents a validated idempotency key from HTTP requests.
///     Provides type-safe validation and prevents injection attacks.
/// </summary>
public readonly record struct IdempotencyKey
{
    private static readonly Regex KeyValidationRegex = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    /// <summary>
    ///     Gets the validated key value.
    /// </summary>
    public string Value { get; }

    private IdempotencyKey(string value) => Value = value;

    /// <summary>
    ///     Attempts to create an <see cref="IdempotencyKey" /> from a string value.
    /// </summary>
    /// <param name="value">The raw key value from the request header.</param>
    /// <param name="key">The validated key if successful.</param>
    /// <param name="maxLength">Maximum allowed key length. Default: 256 characters.</param>
    /// <returns><c>true</c> if the key is valid; otherwise, <c>false</c>.</returns>
    /// <remarks>
    ///     A valid key must:
    ///     1. Not be null or empty
    ///     2. Be no longer than <paramref name="maxLength" /> characters
    ///     3. Contain only alphanumeric characters, hyphens, and underscores
    /// </remarks>
    public static bool TryCreate(string? value, out IdempotencyKey key, int maxLength = 256)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            key = default;
            return false;
        }

        var trimmedValue = value.Trim();
        if (!KeyValidationRegex.IsMatch(trimmedValue))
        {
            key = default;
            return false;
        }

        key = new IdempotencyKey(trimmedValue);
        return true;
    }

    /// <summary>
    ///     Implicitly converts the <see cref="IdempotencyKey" /> to its string value.
    /// </summary>
    public static implicit operator string(IdempotencyKey key) => key.Value;

    /// <summary>
    ///     Returns the string representation of the idempotency key.
    /// </summary>
    public override string ToString() => Value;
}