using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption;

internal enum HashAlgorithmKind
{
    Sha256,
    Sha512
}

public interface IShaHashing : IDisposable // now disposable so we can release cached algorithms
{
    #region Methods

    string ComputeSha256(string input, bool upperCase = false);

    string ComputeSha512(string input, bool upperCase = false);

    bool VerifySha256(string input, string expectedHex, bool ignoreCase = true);

    bool VerifySha512(string input, string expectedHex, bool ignoreCase = true);

    #endregion
}

public sealed class ShaHashing : IShaHashing
{
    #region Fields

    private readonly Dictionary<HashAlgorithmKind, HashAlgorithm> _algorithms = [];
    private bool _disposed;
    private readonly Lock _sync = new(); // corrected from Lock to object

    #endregion

    #region Methods

    private string ComputeHash(
        string input,
        HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
        bool upperCase = false)
    {
        ObjectDisposedException.ThrowIf(this._disposed, nameof(ShaHashing));
        ArgumentNullException.ThrowIfNull(input);
        var hashAlgo = this.GetOrCreate(algorithm);
        byte[] hash;
        lock (hashAlgo) // HashAlgorithm instances are not thread-safe
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            hash = hashAlgo.ComputeHash(bytes);
        }

        var hexUpper = Convert.ToHexString(hash);
        return upperCase ? hexUpper : hexUpper.ToUpperInvariant();
    }

    public string ComputeSha256(string input, bool upperCase = false)
        =>
            this.ComputeHash(input, HashAlgorithmKind.Sha256, upperCase);

    public string ComputeSha512(string input, bool upperCase = false)
        =>
            this.ComputeHash(input, HashAlgorithmKind.Sha512, upperCase);

    private static HashAlgorithm CreateAlgorithm(HashAlgorithmKind kind) => kind switch
    {
        HashAlgorithmKind.Sha256 => SHA256.Create(),
        HashAlgorithmKind.Sha512 => SHA512.Create(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        lock (this._sync)
        {
            foreach (var kv in this._algorithms)
            {
                kv.Value.Dispose();
            }

            this._algorithms.Clear();
            this._disposed = true;
        }
    }

    private HashAlgorithm GetOrCreate(HashAlgorithmKind kind)
    {
        lock (this._sync)
        {
            if (this._algorithms.TryGetValue(kind, out var existing))
            {
                return existing;
            }

            var created = CreateAlgorithm(kind);
            this._algorithms[kind] = created;
            return created;
        }
    }

    private bool VerifyHash(
        string input,
        string expectedHex,
        HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
        bool ignoreCase = true)
    {
        ObjectDisposedException.ThrowIf(this._disposed, nameof(ShaHashing));
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHex);
        var actual = this.ComputeHash(input, algorithm, !ignoreCase);
        return ignoreCase
            ? string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase)
            : actual == expectedHex;
    }

    public bool VerifySha256(string input, string expectedHex, bool ignoreCase = true)
        =>
            this.VerifyHash(input, expectedHex, HashAlgorithmKind.Sha256, ignoreCase);

    public bool VerifySha512(string input, string expectedHex, bool ignoreCase = true)
        =>
            this.VerifyHash(input, expectedHex, HashAlgorithmKind.Sha512, ignoreCase);

    #endregion
}