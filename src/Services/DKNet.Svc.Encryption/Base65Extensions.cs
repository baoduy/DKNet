using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DKNet.Svc.Encryption;

/// <summary>
///     Provides utility methods for working with Base64 and Base64URL encoded strings.
/// </summary>
/// <remarks>
///     Purpose: To provide a set of utility methods for handling Base64 / Base64URL encoded strings.
///     Differences:
///     - Base64URL (RFC 4648) replaces '+' with '-' and '/' with '_' and omits '=' padding for URL / filename safety.
///     Best Practices:
///     - Prefer Base64URL for tokens embedded in URLs, cookies or HTML where reserved characters cause encoding overhead.
///     - Validate user supplied Base64 with <see cref="IsBase64String" /> before decoding to avoid exceptions.
/// </remarks>
[SuppressMessage("Design", "CA1055:URI-like return values should not be strings")]
public static class Base64StringExtensions
{
    #region Methods

    /// <summary>
    ///     Decodes a given Base64-encoded string to its original plain text form.
    /// </summary>
    /// <param name="encryptedText">The Base64-encoded string.</param>
    /// <returns>The decrypted plain text string.</returns>
    public static string FromBase64String(string encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
        {
            return string.Empty;
        }

        var base64EncodedBytes = Convert.FromBase64String(encryptedText);
        return Encoding.UTF8.GetString(base64EncodedBytes);
    }

    /// <summary>
    ///     Decodes a Base64URL encoded string (RFC 4648) back to its UTF-8 plain text representation.
    /// </summary>
    /// <param name="encryptedText">The Base64URL encoded input (may be null / whitespace).</param>
    /// <returns>
    ///     The decoded UTF-8 string. Returns <see cref="string.Empty" /> if <paramref name="encryptedText" /> is null, empty
    ///     or whitespace.
    /// </returns>
    /// <exception cref="FormatException">Thrown when the input contains invalid Base64URL characters or incorrect length.</exception>
    /// <remarks>
    ///     This method tolerates missing padding (standard for Base64URL). It does <b>not</b> accept the standard Base64
    ///     alphabet characters '+' or '/' unless they are properly transformed into '-' and '_' respectively.
    /// </remarks>
    public static string FromBase64UrlString(string encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
        {
            return string.Empty;
        }

        var bytes = Base64Url.DecodeFromChars(encryptedText);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    ///     Determines whether the provided string is a valid Base64 encoded string.
    /// </summary>
    /// <param name="base64String">The string to check.</param>
    /// <returns>True if the string is a valid Base64 encoded string; otherwise, false.</returns>
    public static bool IsBase64String(string base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String))
        {
            return false;
        }

        Span<byte> buffer = new byte[base64String.Length];
        return Convert.TryFromBase64String(base64String, buffer, out _);
    }

    /// <summary>
    ///     Encodes a given string to its Base64-encoded form.
    /// </summary>
    /// <param name="plainText">The plain text to encode.</param>
    /// <returns>The Base64-encoded string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when plainText is null.</exception>
    public static string ToBase64String(string plainText) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));

    /// <summary>
    ///     Encodes a given UTF-8 string to a Base64URL encoded representation (RFC 4648, URL-safe alphabet, no padding '=')
    ///     suitable for inclusion in URLs, cookies and filenames without additional escaping.
    /// </summary>
    /// <param name="plainText">The UTF-8 plain text to encode.</param>
    /// <returns>
    ///     The Base64URL encoded form of <paramref name="plainText" /> (never null). Empty string encodes to empty
    ///     string.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="plainText" /> is null.</exception>
    /// <example>
    ///     <code>
    /// var token = "test".ToBase64UrlString();        // dGVzdA
    /// var roundtrip = token.FromBase64UrlString();    // "test"
    /// </code>
    /// </example>
    public static string ToBase64UrlString(string plainText) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(plainText));

    #endregion
}

/// <summary>
///     Provides extension methods for working with Base64 and Base64URL encoded strings.
/// </summary>
/// <remarks>
///     Obsolete: this type is misspelled. Use <see cref="Base64StringExtensions" /> instead.
/// </remarks>
[Obsolete("Use Base64StringExtensions instead (this type name is misspelled).")]
[SuppressMessage("Design", "CA1055:URI-like return values should not be strings")]
public static class Base65StringExtensions
{
    #region Methods

    /// <summary>
    ///     FromBase64String operation.
    /// </summary>
    public static string FromBase64String(this string encryptedText) =>
        Base64StringExtensions.FromBase64String(encryptedText);

    /// <summary>
    ///     FromBase64UrlString operation.
    /// </summary>
    public static string FromBase64UrlString(this string encryptedText) =>
        Base64StringExtensions.FromBase64UrlString(encryptedText);

    /// <summary>
    ///     IsBase64String operation.
    /// </summary>
    public static bool IsBase64String(this string base64String) =>
        Base64StringExtensions.IsBase64String(base64String);

    /// <summary>
    ///     ToBase64String operation.
    /// </summary>
    public static string ToBase64String(this string plainText) =>
        Base64StringExtensions.ToBase64String(plainText);

    /// <summary>
    ///     ToBase64UrlString operation.
    /// </summary>
    public static string ToBase64UrlString(this string plainText) =>
        Base64StringExtensions.ToBase64UrlString(plainText);

    #endregion
}
