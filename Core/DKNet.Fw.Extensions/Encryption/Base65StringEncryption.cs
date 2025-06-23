using System.Buffers;
using System.Text;

namespace DKNet.Fw.Extensions.Encryption;

/// <summary>
/// Provides utility methods for working with Base64 encoded strings.
/// </summary>
/// <remarks>
/// Purpose: To provide a set of utility methods for handling Base64 encoded strings.
/// Rationale: Simplifies the process of working with Base64 encoded strings, reducing the risk of errors.
/// 
/// Functionality:
/// - Validates if a string is a valid Base64 encoded string
/// - Encodes a plain text string to its Base64 representation
/// - Decodes a Base64 encoded string to its original plain text form
/// 
/// Integration:
/// - Can be used as an extension method for string manipulation
/// - Works seamlessly with other encryption utilities
/// 
/// Best Practices:
/// - Use IsBase64String to validate strings before attempting to decode them
/// - Use ToBase64String to encode plain text strings that need to be safely transmitted
/// - Use FromBase64String to decode Base64 encoded strings back to their original form
/// - Handle encoding/decoding exceptions appropriately
/// </remarks>
public static class Base65StringEncryption
{
    private static readonly char[] _allowedBase64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=".ToCharArray();

    /// <summary>
    /// Determines whether the provided string is a valid Base64 encoded string.
    /// </summary>
    /// <param name="base64String">The string to check.</param>
    /// <returns>True if the string is a valid Base64 encoded string; otherwise, false.</returns>
    public static bool IsBase64String(this string base64String)
    {
        // Base64 strings must be a multiple of 4 in length
        if (!base64String.IsBase64StringLengthValid() || !base64String.ContainsOnlyAllowedChars())
            return false;

        var minLength = ((base64String.Length * 3) + 3) / 4;
        var buffer = ArrayPool<byte>.Shared.Rent(minLength);
        // Try to decode the Base64 string into bytes.
        if (!Convert.TryFromBase64String(base64String, buffer, out var bytesWritten))
            return false;

        var decoded = Encoding.UTF8.GetChars(buffer, 0, bytesWritten);
        return decoded.All(c => c is >= (char)32 and <= (char)126);
    }

    /// <summary>
    /// Encodes a given string to its Base64-encoded form.
    /// </summary>
    /// <param name="plainText">The plain text to encode.</param>
    /// <returns>The Base64-encoded string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when plainText is null.</exception>
    public static string ToBase64String(this string plainText)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));

    /// <summary>
    /// Decodes a given Base64-encoded string to its original plain text form.
    /// </summary>
    /// <param name="encryptedText">The Base64-encoded string.</param>
    /// <returns>The decrypted plain text string.</returns>
    public static string FromBase64String(this string encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText)) return string.Empty;

        var base64EncodedBytes = Convert.FromBase64String(encryptedText);
        return Encoding.UTF8.GetString(base64EncodedBytes);
    }

    /// <summary>
    /// Determines whether the provided string is a valid Base64 encoded string based on its length.
    /// </summary>
    /// <param name="base64String">The string to check.</param>
    /// <returns>True if the string's length is valid for a Base64 encoded string; otherwise, false.</returns>
    private static bool IsBase64StringLengthValid(this string base64String)
        => !string.IsNullOrWhiteSpace(base64String) && base64String.Length % 4 == 0;

    /// <summary>
    /// Determines whether the provided string contains only allowed Base64 characters.
    /// </summary>
    /// <param name="base64String">The string to check.</param>
    /// <returns>True if the string contains only allowed Base64 characters; otherwise, false.</returns>
    private static bool ContainsOnlyAllowedChars(this string base64String)
        => base64String.All(c => _allowedBase64Chars.Contains(c));
}
