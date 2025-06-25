using System.Security.Cryptography;
using System.Text;

namespace DKNet.Fw.Extensions.Encryption;

/// <summary>
/// Provides string hashing utilities for common security operations.
/// </summary>
/// <remarks>
/// Purpose: To provide a set of utility methods for hashing strings.
/// Rationale: Ensures data integrity and security by providing hashing algorithms.
/// 
/// Functionality:
/// - Generates HMAC-SHA256 hashes with a specified key
/// - Generates SHA256 hashes
/// 
/// Integration:
/// - Can be used as an extension method for string manipulation
/// - Works with other encryption utilities in the framework
/// 
/// Best Practices:
/// - Use strong, randomly generated keys for HMAC-SHA256
/// - Store hashes securely, not the original data
/// - Use SHA256 for general-purpose hashing
/// - Validate input strings before hashing
/// </remarks>
public static class StringHashing
{
    /// <summary>
    /// Generates an HMAC-SHA256 hash for the provided value using the specified key.
    /// </summary>
    /// <param name="value">The input string to be hashed.</param>
    /// <param name="key">The secret key used for hashing.</param>
    /// <returns>A hexadecimal string representing the HMAC-SHA256 hash of the input value.</returns>
    /// <exception cref="ArgumentException">Thrown when value is null or empty.</exception>
    public static string ToCmd5(this string value, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Generates a SHA256 hash for the provided value.
    /// </summary>
    /// <param name="value">The input string to be hashed.</param>
    /// <returns>A hexadecimal string representing the SHA256 hash of the input value.</returns>
    /// <exception cref="ArgumentException">Thrown when value is null or empty.</exception>
    public static string ToSha256(this string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        // Convert the input string to a byte array and compute the hash using SHA256.HashData
        var inputBytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = SHA256.HashData(inputBytes);

        // Use built-in .NET framework method for hex conversion
        return Convert.ToHexStringLower(hashBytes);
    }
}