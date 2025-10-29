// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ShaHashing.cs
// Description: Provides interfaces and implementations for SHA-256 and SHA-512 hashing with verification helpers.

using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption;

internal enum HashAlgorithmKind
{
    Sha256,
    Sha512
}

/// <summary>
///     Defines SHA hashing operations (SHA-256 and SHA-512) with convenience verification helpers.
///     Implementations may cache algorithm instances for performance and are disposable.
/// </summary>
public interface IShaHashing : IDisposable // now disposable so we can release cached algorithms
{
    #region Methods

    /// <summary>
    ///     Computes the SHA-256 hash of the specified UTF-8 input string.
    /// </summary>
    /// <param name="input">The input text to hash.</param>
    /// <param name="upperCase">If <c>true</c> returns an upper-case hexadecimal string; otherwise lower-case.</param>
    /// <returns>The hexadecimal hash string.</returns>
    string ComputeSha256(string input, bool upperCase = false);

    /// <summary>
    ///     Computes the SHA-512 hash of the specified UTF-8 input string.
    /// </summary>
    /// <param name="input">The input text to hash.</param>
    /// <param name="upperCase">If <c>true</c> returns an upper-case hexadecimal string; otherwise lower-case.</param>
    /// <returns>The hexadecimal hash string.</returns>
    string ComputeSha512(string input, bool upperCase = false);

    /// <summary>
    ///     Verifies the SHA-256 hash of <paramref name="input" /> matches the expected hexadecimal value.
    /// </summary>
    /// <param name="input">The input text to hash and compare.</param>
    /// <param name="expectedHex">The expected hexadecimal hash string.</param>
    /// <param name="ignoreCase">If <c>true</c> performs a case-insensitive comparison.</param>
    /// <returns><c>true</c> if the computed hash equals <paramref name="expectedHex" />; otherwise <c>false</c>.</returns>
    bool VerifySha256(string input, string expectedHex, bool ignoreCase = true);

    /// <summary>
    ///     Verifies the SHA-512 hash of <paramref name="input" /> matches the expected hexadecimal value.
    /// </summary>
    /// <param name="input">The input text to hash and compare.</param>
    /// <param name="expectedHex">The expected hexadecimal hash string.</param>
    /// <param name="ignoreCase">If <c>true</c> performs a case-insensitive comparison.</param>
    /// <returns><c>true</c> if the computed hash equals <paramref name="expectedHex" />; otherwise <c>false</c>.</returns>
    bool VerifySha512(string input, string expectedHex, bool ignoreCase = true);

    #endregion
}

/// <summary>
///     Provides SHA-256 and SHA-512 hashing plus verification helpers. Caches algorithm instances for reuse.
/// </summary>
public sealed class ShaHashing : IShaHashing
{
    #region Fields

    private readonly Dictionary<HashAlgorithmKind, HashAlgorithm> _algorithms = [];
    private readonly object _sync = new(); // changed to object for locking
    private bool _disposed;

    #endregion

    #region Methods

    /// <summary>
    ///     Computes the hash for the given input using the requested algorithm.
    /// </summary>
    /// <param name="input">The UTF-8 input string.</param>
    /// <param name="algorithm">The hashing algorithm to apply.</param>
    /// <param name="upperCase">If <c>true</c> return upper-case hex; otherwise lower-case.</param>
    /// <returns>The hexadecimal hash string.</returns>
    private string ComputeHash(
        string input,
        HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
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
        return upperCase ? hexUpper : hexUpper.ToLowerInvariant();
    }

    /// <summary>
    ///     Computes a SHA-256 hash for the specified input.
    /// </summary>
    /// <inheritdoc cref="IShaHashing.ComputeSha256" />
    public string ComputeSha256(string input, bool upperCase = false)
        => ComputeHash(input, HashAlgorithmKind.Sha256, upperCase);

    /// <summary>
    ///     Computes a SHA-512 hash for the specified input.
    /// </summary>
    /// <inheritdoc cref="IShaHashing.ComputeSha512" />
    public string ComputeSha512(string input, bool upperCase = false)
        => ComputeHash(input, HashAlgorithmKind.Sha512, upperCase);

    /// <summary>
    ///     Creates a new hashing algorithm instance for the given kind.
    /// </summary>
    /// <param name="kind">The algorithm kind.</param>
    /// <returns>A new <see cref="HashAlgorithm" /> instance.</returns>
    private static HashAlgorithm CreateAlgorithm(HashAlgorithmKind kind) => kind switch
    {
        HashAlgorithmKind.Sha256 => SHA256.Create(),
        HashAlgorithmKind.Sha512 => SHA512.Create(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>
    ///     Releases resources held by cached algorithms.
    /// </summary>
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

    /// <summary>
    ///     Returns an existing cached algorithm or creates a new one for the requested kind.
    /// </summary>
    /// <param name="kind">The algorithm kind.</param>
    /// <returns>A cached or newly created <see cref="HashAlgorithm" /> instance.</returns>
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

    /// <summary>
    ///     Verifies a hash for the given input using the requested algorithm.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="expectedHex">The expected hexadecimal hash value.</param>
    /// <param name="algorithm">The algorithm to apply.</param>
    /// <param name="ignoreCase">If <c>true</c> performs a case-insensitive comparison.</param>
    /// <returns><c>true</c> if the computed hash matches <paramref name="expectedHex" />; otherwise <c>false</c>.</returns>
    private bool VerifyHash(
        string input,
        string expectedHex,
        HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
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

    /// <summary>
    ///     Verifies the SHA-256 hash for the specified input.
    /// </summary>
    /// <inheritdoc cref="IShaHashing.VerifySha256" />
    public bool VerifySha256(string input, string expectedHex, bool ignoreCase = true)
        => VerifyHash(input, expectedHex, HashAlgorithmKind.Sha256, ignoreCase);

    /// <summary>
    ///     Verifies the SHA-512 hash for the specified input.
    /// </summary>
    /// <inheritdoc cref="IShaHashing.VerifySha512" />
    public bool VerifySha512(string input, string expectedHex, bool ignoreCase = true)
        => VerifyHash(input, expectedHex, HashAlgorithmKind.Sha512, ignoreCase);

    #endregion
}