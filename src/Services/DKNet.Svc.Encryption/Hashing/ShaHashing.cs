// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ShaHashing.cs
// Description: Provides interfaces and implementations for SHA-256 and SHA-512 hashing with verification helpers.

using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption.Hashing;

internal enum HashAlgorithmKind
{
    Sha256,
    Sha512
}

/// <summary>
///     Defines SHA hashing operations (SHA-256 and SHA-512) with convenience verification helpers.
///     Implementations are stateless, static-internally wrappers — no cached algorithm instances to
///     release.
/// </summary>
public interface IShaHashing
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
    /// <param name="ignoreCase">Has no effect on the result: hex decoding is case-insensitive, so the comparison result is unaffected by this flag.</param>
    /// <returns><c>true</c> if the computed hash equals <paramref name="expectedHex" />; otherwise <c>false</c>.</returns>
    bool VerifySha256(string input, string expectedHex, bool ignoreCase = true);

    /// <summary>
    ///     Verifies the SHA-512 hash of <paramref name="input" /> matches the expected hexadecimal value.
    /// </summary>
    /// <param name="input">The input text to hash and compare.</param>
    /// <param name="expectedHex">The expected hexadecimal hash string.</param>
    /// <param name="ignoreCase">Has no effect on the result: hex decoding is case-insensitive, so the comparison result is unaffected by this flag.</param>
    /// <returns><c>true</c> if the computed hash equals <paramref name="expectedHex" />; otherwise <c>false</c>.</returns>
    bool VerifySha512(string input, string expectedHex, bool ignoreCase = true);

    #endregion
}

/// <summary>
///     Provides SHA-256 and SHA-512 hashing plus verification helpers.
/// </summary>
public sealed class ShaHashing : IShaHashing
{
    #region Methods

    /// <summary>
    ///     Computes the hash for the given input using the requested algorithm.
    /// </summary>
    /// <param name="input">The UTF-8 input string.</param>
    /// <param name="algorithm">The hashing algorithm to apply.</param>
    /// <param name="upperCase">If <c>true</c> return upper-case hex; otherwise lower-case.</param>
    /// <returns>The hexadecimal hash string.</returns>
    private static string ComputeHash(
        string input,
        HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
        bool upperCase = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = algorithm == HashAlgorithmKind.Sha512 ? SHA512.HashData(bytes) : SHA256.HashData(bytes);

        return upperCase ? Convert.ToHexString(hash) : Convert.ToHexStringLower(hash);
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
    ///     Verifies a hash for the given input using the requested algorithm.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="expectedHex">The expected hexadecimal hash value.</param>
    /// <param name="algorithm">The algorithm to apply.</param>
    /// <param name="ignoreCase">If <c>true</c> performs a case-insensitive comparison.</param>
    /// <returns><c>true</c> if the computed hash matches <paramref name="expectedHex" />; otherwise <c>false</c>.</returns>
    private static bool VerifyHash(
        string input,
        string expectedHex,
        HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256,
        bool ignoreCase = true)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHex);
        var actual = ComputeHash(input, algorithm, !ignoreCase);
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actual),
                Convert.FromHexString(expectedHex));
        }
        catch (FormatException)
        {
            return false;
        }
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