using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption.Hashing;

internal enum HmacAlgorithm
{
    Sha256,
    Sha512
}

/// <summary>
///     Interface for HMAC hashing operations.
/// </summary>
public interface IHmacHashing
{
    #region Methods

    /// <summary>
    ///     Computes the HMAC-SHA256 hash of the specified message using the provided secret key.
    /// </summary>
    /// <param name="message">The message to hash.</param>
    /// <param name="secretKey">The secret key to use for hashing.</param>
    /// <param name="asBase64">If <c>true</c>, returns the hash as a base64 string; otherwise, as a hexadecimal string.</param>
    /// <returns>The computed HMAC-SHA256 hash as a string.</returns>
    string ComputeSha256(string message, string secretKey, bool asBase64 = true);

    /// <summary>
    ///     Computes the HMAC-SHA512 hash of the specified message using the provided secret key.
    /// </summary>
    /// <param name="message">The message to hash.</param>
    /// <param name="secretKey">The secret key to use for hashing.</param>
    /// <param name="asBase64">If <c>true</c>, returns the hash as a base64 string; otherwise, as a hexadecimal string.</param>
    /// <returns>The computed HMAC-SHA512 hash as a string.</returns>
    string ComputeSha512(string message, string secretKey, bool asBase64 = true);

    /// <summary>
    ///     Verifies that the HMAC-SHA256 hash of the specified message and secret key matches the expected signature.
    /// </summary>
    /// <param name="message">The message to hash and verify.</param>
    /// <param name="secretKey">The secret key to use for hashing.</param>
    /// <param name="expectedSignature">The expected hash signature to compare against.</param>
    /// <param name="signatureIsBase64">If <c>true</c>, the signature is base64-encoded; otherwise, hexadecimal.</param>
    /// <returns><c>true</c> if the computed hash matches the expected signature; otherwise, <c>false</c>.</returns>
    bool VerifySha256(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true);

    /// <summary>
    ///     Verifies that the HMAC-SHA512 hash of the specified message and secret key matches the expected signature.
    /// </summary>
    /// <param name="message">The message to hash and verify.</param>
    /// <param name="secretKey">The secret key to use for hashing.</param>
    /// <param name="expectedSignature">The expected hash signature to compare against.</param>
    /// <param name="signatureIsBase64">If <c>true</c>, the signature is base64-encoded; otherwise, hexadecimal.</param>
    /// <returns><c>true</c> if the computed hash matches the expected signature; otherwise, <c>false</c>.</returns>
    bool VerifySha512(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true);

    #endregion
}

/// <summary>
///     Provides HMAC hashing functionality for SHA256 and SHA512 algorithms.
/// </summary>
public sealed class HmacHashing : IHmacHashing
{
    #region Methods

    private static byte[] ComputeBytes(
        string message,
        string secretKey,
        HmacAlgorithm algorithm = HmacAlgorithm.Sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var msgBytes = Encoding.UTF8.GetBytes(message);
        try
        {
            return algorithm == HmacAlgorithm.Sha512
                ? HMACSHA512.HashData(keyBytes, msgBytes)
                : HMACSHA256.HashData(keyBytes, msgBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    private static string Compute(
        string message,
        string secretKey,
        HmacAlgorithm algorithm = HmacAlgorithm.Sha256,
        bool asBase64 = true)
    {
        var hash = ComputeBytes(message, secretKey, algorithm);
        return asBase64 ? Convert.ToBase64String(hash) : Convert.ToHexString(hash);
    }

    /// <summary>
    ///     ComputeSha256 operation.
    /// </summary>
    public string ComputeSha256(string message, string secretKey, bool asBase64 = true)
        =>
            Compute(message, secretKey, HmacAlgorithm.Sha256, asBase64);

    /// <summary>
    ///     ComputeSha512 operation.
    /// </summary>
    public string ComputeSha512(string message, string secretKey, bool asBase64 = true)
        =>
            Compute(message, secretKey, HmacAlgorithm.Sha512, asBase64);

    private static bool Verify(
        string message,
        string secretKey,
        string expectedSignature,
        HmacAlgorithm algorithm = HmacAlgorithm.Sha256,
        bool signatureIsBase64 = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSignature);

        var actualBytes = ComputeBytes(message, secretKey, algorithm);
        try
        {
            var expectedBytes = signatureIsBase64
                ? Convert.FromBase64String(expectedSignature)
                : Convert.FromHexString(expectedSignature);
            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    ///     VerifySha256 operation.
    /// </summary>
    public bool VerifySha256(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true)
        =>
            Verify(message, secretKey, expectedSignature, HmacAlgorithm.Sha256, signatureIsBase64);

    /// <summary>
    ///     VerifySha512 operation.
    /// </summary>
    public bool VerifySha512(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true)
        =>
            Verify(message, secretKey, expectedSignature, HmacAlgorithm.Sha512, signatureIsBase64);

    #endregion
}