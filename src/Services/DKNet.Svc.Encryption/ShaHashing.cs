using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption;

public enum HashAlgorithmKind
{
    Sha256,
    Sha512
}

public interface IShaHashing : IDisposable // now disposable so we can release cached algorithms
{
    string ComputeHash(string input, HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256, bool upperCase = false);

    bool VerifyHash(string input, string expectedHex, HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
        bool ignoreCase = true);
}

public sealed class ShaHashing : IShaHashing
{
    private readonly Dictionary<HashAlgorithmKind, HashAlgorithm> _algorithms = [];
    private readonly Lock _sync = new(); // corrected from Lock to object
    private bool _disposed;

    public string ComputeHash(string input, HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
        bool upperCase = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ShaHashing));
        ArgumentNullException.ThrowIfNull(input);
        var hashAlgo = GetOrCreate(algorithm);
        byte[] hash;
        lock (hashAlgo) // HashAlgorithm instances are not thread-safe
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            hash = hashAlgo.ComputeHash(bytes);
        }

        var hexUpper = Convert.ToHexString(hash);
        return upperCase ? hexUpper : hexUpper.ToUpperInvariant();
    }

    public bool VerifyHash(string input, string expectedHex, HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
        bool ignoreCase = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ShaHashing));
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHex);
        var actual = ComputeHash(input, algorithm, !ignoreCase);
        return ignoreCase
            ? string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase)
            : actual == expectedHex;
    }

    private HashAlgorithm GetOrCreate(HashAlgorithmKind kind)
    {
        lock (_sync)
        {
            if (_algorithms.TryGetValue(kind, out var existing)) return existing;
            var created = CreateAlgorithm(kind);
            _algorithms[kind] = created;
            return created;
        }
    }

    private static HashAlgorithm CreateAlgorithm(HashAlgorithmKind kind) => kind switch
    {
        HashAlgorithmKind.Sha256 => SHA256.Create(),
        HashAlgorithmKind.Sha512 => SHA512.Create(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public void Dispose()
    {
        if (_disposed) return;
        lock (_sync)
        {
            foreach (var kv in _algorithms) kv.Value.Dispose();
            _algorithms.Clear();
            _disposed = true;
        }
    }
}