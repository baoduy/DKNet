using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption;

internal enum HmacAlgorithm
{
    Sha256,
    Sha512
}

/// <summary>
///     Interface for HMAC hashing operations.
/// </summary>
public interface IHmacHashing : IDisposable
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
    /// <param name="ignoreCase">If <c>true</c>, ignores case when comparing signatures.</param>
    /// <returns><c>true</c> if the computed hash matches the expected signature; otherwise, <c>false</c>.</returns>
    bool VerifySha256(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true,
        bool ignoreCase = true);

    /// <summary>
    ///     Verifies that the HMAC-SHA512 hash of the specified message and secret key matches the expected signature.
    /// </summary>
    /// <param name="message">The message to hash and verify.</param>
    /// <param name="secretKey">The secret key to use for hashing.</param>
    /// <param name="expectedSignature">The expected hash signature to compare against.</param>
    /// <param name="signatureIsBase64">If <c>true</c>, the signature is base64-encoded; otherwise, hexadecimal.</param>
    /// <param name="ignoreCase">If <c>true</c>, ignores case when comparing signatures.</param>
    /// <returns><c>true</c> if the computed hash matches the expected signature; otherwise, <c>false</c>.</returns>
    bool VerifySha512(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true,
        bool ignoreCase = true);

    #endregion
}

/// <summary>
///     Provides HMAC hashing functionality for SHA256 and SHA512 algorithms, with caching for performance.
/// </summary>
public sealed class HmacHashing : IHmacHashing
{
    #region Fields

    private readonly Dictionary<(HmacAlgorithm alg, string key), HMAC> _cache = [];
    private readonly Lock _sync = new();
    private bool _disposed;

    #endregion

    #region Methods

    private string Compute(
        string message,
        string secretKey,
        HmacAlgorithm algorithm = HmacAlgorithm.Sha256,
        bool asBase64 = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(HmacHashing));
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        var hmac = GetOrCreate(algorithm, secretKey);
        byte[] hash;
        lock (hmac) // HMAC not thread-safe
        {
            hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        }

        return asBase64 ? Convert.ToBase64String(hash) : Convert.ToHexString(hash).ToUpperInvariant();
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

    private static HMAC Create(HmacAlgorithm algorithm, byte[] key) => algorithm switch
    {
        HmacAlgorithm.Sha256 => new HMACSHA256(key),
        HmacAlgorithm.Sha512 => new HMACSHA512(key),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
    };

    /// <summary>
    ///     Dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_sync)
        {
            foreach (var kv in _cache) kv.Value.Dispose();

            _cache.Clear();
            _disposed = true;
        }
    }

    private HMAC GetOrCreate(HmacAlgorithm algorithm, string secretKey)
    {
        var cacheKey = (algorithm, secretKey);
        lock (_sync)
        {
            if (_cache.TryGetValue(cacheKey, out var existing)) return existing;

            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var created = Create(algorithm, keyBytes);
            _cache[cacheKey] = created;
            return created;
        }
    }

    private bool Verify(
        string message,
        string secretKey,
        string expectedSignature,
        HmacAlgorithm algorithm = HmacAlgorithm.Sha256,
        bool signatureIsBase64 = true,
        bool ignoreCase = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(HmacHashing));
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSignature);

        var actual = Compute(message, secretKey, algorithm, signatureIsBase64);
        return ignoreCase
            ? string.Equals(actual, expectedSignature, StringComparison.OrdinalIgnoreCase)
            : actual == expectedSignature;
    }

    /// <summary>
    ///     VerifySha256 operation.
    /// </summary>
    public bool VerifySha256(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true,
        bool ignoreCase = true)
        =>
            Verify(message, secretKey, expectedSignature, HmacAlgorithm.Sha256, signatureIsBase64, ignoreCase);

    /// <summary>
    ///     VerifySha512 operation.
    /// </summary>
    public bool VerifySha512(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true,
        bool ignoreCase = true)
        =>
            Verify(message, secretKey, expectedSignature, HmacAlgorithm.Sha512, signatureIsBase64, ignoreCase);

    #endregion
}