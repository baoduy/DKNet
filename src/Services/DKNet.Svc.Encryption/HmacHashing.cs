using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption;

internal enum HmacAlgorithm
{
    Sha256,
    Sha512
}

public interface IHmacHashing : IDisposable // now disposable for cached instances
{
    #region Methods

    string ComputeSha256(string message, string secretKey, bool asBase64 = true);

    string ComputeSha512(string message, string secretKey, bool asBase64 = true);

    bool VerifySha256(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true,
        bool ignoreCase = true);

    bool VerifySha512(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true,
        bool ignoreCase = true);

    #endregion
}

public sealed class HmacHashing : IHmacHashing
{
    #region Fields

    private readonly Dictionary<(HmacAlgorithm alg, string key), HMAC> _cache = [];
    private bool _disposed;
    private readonly Lock _sync = new();

    #endregion

    #region Methods

    private string Compute(
        string message,
        string secretKey,
        HmacAlgorithm algorithm = HmacAlgorithm.Sha256,
        bool asBase64 = true)
    {
        ObjectDisposedException.ThrowIf(this._disposed, nameof(HmacHashing));
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        var hmac = this.GetOrCreate(algorithm, secretKey);
        byte[] hash;
        lock (hmac) // HMAC not thread-safe
        {
            hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        }

        return asBase64 ? Convert.ToBase64String(hash) : Convert.ToHexString(hash).ToUpperInvariant();
    }

    public string ComputeSha256(string message, string secretKey, bool asBase64 = true)
        =>
            this.Compute(message, secretKey, HmacAlgorithm.Sha256, asBase64);

    public string ComputeSha512(string message, string secretKey, bool asBase64 = true)
        =>
            this.Compute(message, secretKey, HmacAlgorithm.Sha512, asBase64);

    private static HMAC Create(HmacAlgorithm algorithm, byte[] key) => algorithm switch
    {
        HmacAlgorithm.Sha256 => new HMACSHA256(key),
        HmacAlgorithm.Sha512 => new HMACSHA512(key),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
    };

    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        lock (this._sync)
        {
            foreach (var kv in this._cache)
            {
                kv.Value.Dispose();
            }

            this._cache.Clear();
            this._disposed = true;
        }
    }

    private HMAC GetOrCreate(HmacAlgorithm algorithm, string secretKey)
    {
        var cacheKey = (algorithm, secretKey);
        lock (this._sync)
        {
            if (this._cache.TryGetValue(cacheKey, out var existing))
            {
                return existing;
            }

            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var created = Create(algorithm, keyBytes);
            this._cache[cacheKey] = created;
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
        ObjectDisposedException.ThrowIf(this._disposed, nameof(HmacHashing));
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSignature);

        var actual = this.Compute(message, secretKey, algorithm, signatureIsBase64);
        return ignoreCase
            ? string.Equals(actual, expectedSignature, StringComparison.OrdinalIgnoreCase)
            : actual == expectedSignature;
    }

    public bool VerifySha256(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true,
        bool ignoreCase = true)
        =>
            this.Verify(message, secretKey, expectedSignature, HmacAlgorithm.Sha256, signatureIsBase64, ignoreCase);

    public bool VerifySha512(
        string message,
        string secretKey,
        string expectedSignature,
        bool signatureIsBase64 = true,
        bool ignoreCase = true)
        =>
            this.Verify(message, secretKey, expectedSignature, HmacAlgorithm.Sha512, signatureIsBase64, ignoreCase);

    #endregion
}