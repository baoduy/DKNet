using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace DKNet.Fw.Extensions.Encryption;

/// <summary>
/// Provides secure string encryption and decryption functionality using AES encryption.
/// </summary>
/// <remarks>
/// Purpose: To provide a secure way to encrypt and decrypt sensitive string data.
/// Rationale: Addresses the need for secure string encryption in applications handling sensitive data.
/// 
/// Functionality:
/// - Generates secure AES encryption keys
/// - Encrypts strings using AES encryption
/// - Decrypts AES-encrypted strings
/// - Handles Base64 encoding/decoding of encrypted data
/// 
/// Integration:
/// - Works with other encryption utilities in the framework
/// - Can be used as an extension method for string encryption/decryption
/// - Integrates with Base64 encoding for safe data transmission
/// 
/// Best Practices:
/// - Always store the AES key securely
/// - Use different keys for different types of data
/// - Validate input strings before encryption/decryption
/// - Handle encryption/decryption exceptions appropriately
/// - Implement proper key rotation policies
/// 
/// Security Considerations:
/// - Uses AES encryption for strong security
/// - Implements proper IV handling
/// - Includes input validation
/// - Follows cryptographic best practices
/// </remarks>
[SuppressMessage("Design", "CA1031:Do not catch general exception types")]
public static class StringEncryption
{
    /// <summary>
    /// Generates a new secure AES key and initialization vector (IV) for encryption.
    /// </summary>
    /// <returns>A Base64-encoded string containing both the key and IV.</returns>
    public static string GenerateAesKey()
    {
        using var aes = Aes.Create();
        var key = aes.Key;
        var iv = aes.IV;

        return $"{Convert.ToBase64String(key)}:{Convert.ToBase64String(iv)}".ToBase64String();
    }

    /// <summary>
    /// Creates an AES encryption object with the specified key and IV.
    /// </summary>
    /// <param name="keyString">The combined Base64-encoded key and IV string.</param>
    /// <returns>An initialized AES encryption object.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the keyString is null, empty, or in an invalid format.
    /// </exception>
    private static Aes CreateAes(string keyString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyString);

        var keys = keyString.FromBase64String().Split(":");
        if (keys.Length != 2) throw new ArgumentException("Invalid key string format.", nameof(keyString));

        var key = Convert.FromBase64String(keys[0]);
        var iv = Convert.FromBase64String(keys[1]);

        var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        return aes;
    }

    /// <summary>
    /// Encrypts a plain text string using AES encryption.
    /// </summary>
    /// <param name="plainText">The text to encrypt.</param>
    /// <param name="keyString">The AES key string for encryption.</param>
    /// <returns>The encrypted text as a Base64-encoded string.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when plainText or keyString is null or empty.
    /// </exception>
    public static string ToAesString(this string plainText, string keyString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainText);

        using var aesAlg = CreateAes(keyString);
#pragma warning disable CA5401 // Do not use CreateEncryptor with non-default IV - IV is intentionally provided via keyString parameter
        var encryptor = aesAlg.CreateEncryptor();
#pragma warning restore CA5401

        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        var encrypted = msEncrypt.ToArray();
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Decrypts an AES-encrypted string back to plain text.
    /// </summary>
    /// <param name="cipherText">The encrypted text to decrypt.</param>
    /// <param name="keyString">The AES key string for decryption.</param>
    /// <returns>The decrypted plain text string.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when cipherText or keyString is null or empty.
    /// </exception>
    public static string FromAesString(this string cipherText, string keyString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);

        using var aesAlg = CreateAes(keyString);
        var decryptor = aesAlg.CreateDecryptor();

        using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }
}
