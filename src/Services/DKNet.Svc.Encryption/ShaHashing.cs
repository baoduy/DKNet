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
    string ComputeSha256(string input, bool upperCase = false);

    bool VerifySha256(string input, string expectedHex, bool ignoreCase = true);

    string ComputeSha512(string input, bool upperCase = false);

    bool VerifySha512(string input, string expectedHex, bool ignoreCase = true);
}

public sealed class ShaHashing : IShaHashing
{
    private readonly Dictionary<HashAlgorithmKind, HashAlgorithm> _algorithms = [];
    private readonly Lock _sync = new(); // corrected from Lock to object
    private bool _disposed;

    private string ComputeHash(string input, HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
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

    private bool VerifyHash(string input, string expectedHex, HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
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

    public string ComputeSha256(string input, bool upperCase = false)
        => ComputeHash(input, HashAlgorithmKind.Sha256, upperCase);

    public bool VerifySha256(string input, string expectedHex, bool ignoreCase = true)
        => VerifyHash(input, expectedHex, HashAlgorithmKind.Sha256, ignoreCase);

    public string ComputeSha512(string input, bool upperCase = false)
        => ComputeHash(input, HashAlgorithmKind.Sha512, upperCase);

    public bool VerifySha512(string input, string expectedHex, bool ignoreCase = true)
        => VerifyHash(input, expectedHex, HashAlgorithmKind.Sha512, ignoreCase);
}