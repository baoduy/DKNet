// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ITokenDefinition.cs
// Description: Token definition interface and implementation used by token extractors.

namespace DKNet.Svc.Transformation.TokenExtractors;

/// <summary>
///     Represents a token boundary definition used by token extractors.
/// </summary>
public interface ITokenDefinition
{
    /// <summary>
    ///     Gets the string that marks the beginning of a token (for example "{{").
    /// </summary>
    string BeginTag { get; }

    /// <summary>
    ///     Gets the string that marks the end of a token (for example "}}").
    /// </summary>
    string EndTag { get; }

    /// <summary>
    ///     Checks whether the provided value is a valid token for this definition.
    ///     A valid token starts with the BeginTag, ends with the EndTag, and contains a non-empty inner payload
    ///     that does not include characters from either tag.
    /// </summary>
    /// <param name="value">The string value to evaluate.</param>
    /// <returns>True when the value is a valid token for this definition; otherwise false.</returns>
    bool IsToken(string value);
}

/// <summary>
///     Concrete token definition with explicit begin and end tag strings.
/// </summary>
public sealed class TokenDefinition : ITokenDefinition
{
    #region Constructors

    /// <summary>
    ///     Initializes a new instance of TokenDefinition with the specified begin and end tags.
    /// </summary>
    /// <param name="begin">The begin tag string (must not be null or whitespace).</param>
    /// <param name="end">The end tag string (must not be null or whitespace).</param>
    public TokenDefinition(string begin, string end)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(begin);
        ArgumentException.ThrowIfNullOrWhiteSpace(end);

        BeginTag = begin;
        EndTag = end;
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the string that marks the beginning of a token.
    /// </summary>
    public string BeginTag { get; }

    /// <summary>
    ///     Gets the string that marks the end of a token.
    /// </summary>
    public string EndTag { get; }

    #endregion

    #region Methods

    /// <summary>
    ///     Determines whether the provided value conforms to this token definition.
    ///     The value must start with the BeginTag, end with the EndTag, and contain a non-empty inner payload
    ///     that does not include any characters from the begin or end tags.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns>True when the value matches the token definition; otherwise false.</returns>
    public bool IsToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var span = value.AsSpan();

        if (!span.StartsWith(BeginTag.AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;

        if (!span.EndsWith(EndTag.AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;

        var inner = span.Slice(BeginTag.Length, span.Length - BeginTag.Length - EndTag.Length);
        if (inner.Length == 0) return false;

        // Ensure none of the characters that compose the begin or end tag appear inside the inner payload.
        foreach (var c in BeginTag)
            if (inner.Contains(c))
                return false;

        foreach (var c in EndTag)
            if (inner.Contains(c))
                return false;

        return true;
    }

    #endregion
}