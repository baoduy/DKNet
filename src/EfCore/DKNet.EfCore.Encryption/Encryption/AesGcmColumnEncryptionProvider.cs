// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using DKNet.EfCore.Encryption.Interfaces;

namespace DKNet.EfCore.Encryption.Encryption;

/// <summary>
///     Provides AES-GCM encryption and decryption for Entity Framework Core column data.
/// </summary>
public sealed class AesGcmColumnEncryptionProvider : IColumnEncryptionProvider
{
    #region Fields

    private const int IvSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="AesGcmColumnEncryptionProvider" /> class.
    /// </summary>
    /// <param name="key">The encryption key. Must be 16, 24, or 32 bytes in length.</param>
    /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key length is not 16, 24, or 32 bytes.</exception>
    public AesGcmColumnEncryptionProvider(byte[] key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key), "Encryption key cannot be null.");

        if (key.Length != 16 && key.Length != 24 && key.Length != 32)
            throw new ArgumentException("Key length must be 16, 24, or 32 bytes", nameof(key));

        _key = key;
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Decrypts an encrypted string value.
    /// </summary>
    /// <param name="ciphertext">The encrypted string to decrypt, encoded as Base64.</param>
    /// <returns>The decrypted plaintext, or null if the input is null or empty.</returns>
    /// <exception cref="ArgumentException">Thrown when ciphertext format is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when decryption fails.</exception>
    public string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;

        var cipherData = Convert.FromBase64String(ciphertext);

        if (cipherData.Length < IvSize + TagSize) throw new ArgumentException("Invalid ciphertext format");

        // Slice directly into the decoded buffer instead of copying iv/tag/ciphertext into three new arrays.
        var iv = cipherData.AsSpan(0, IvSize);
        var tag = cipherData.AsSpan(IvSize, TagSize);
        var actualCipherText = cipherData.AsSpan(IvSize + TagSize);

        var plaintextBytes = new byte[actualCipherText.Length];
        try
        {
            using var aesGcm = new AesGcm(_key, TagSize);
            aesGcm.Decrypt(iv, actualCipherText, tag, plaintextBytes);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                "Decryption failed. The data may be corrupted or the key is incorrect.");
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    /// <summary>
    ///     Encrypts a plaintext string value.
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt.</param>
    /// <returns>The encrypted ciphertext encoded as Base64, or null if the input is null or empty.</returns>
    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        // Write iv/tag/ciphertext straight into their final positions in one buffer instead of
        // allocating each separately and copying them together afterward.
        var result = new byte[IvSize + TagSize + plaintextBytes.Length];
        var iv = result.AsSpan(0, IvSize);
        var tag = result.AsSpan(IvSize, TagSize);
        var ciphertext = result.AsSpan(IvSize + TagSize);

        RandomNumberGenerator.Fill(iv);

        using (var aesGcm = new AesGcm(_key, TagSize))
        {
            aesGcm.Encrypt(iv, plaintextBytes, ciphertext, tag);
        }

        return Convert.ToBase64String(result);
    }

    #endregion
}