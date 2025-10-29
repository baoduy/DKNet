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
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key), "Encryption key cannot be null.");
        }

        if (key.Length != 16 && key.Length != 24 && key.Length != 32)
        {
            throw new ArgumentException("Key length must be 16, 24, or 32 bytes", nameof(key));
        }

        this._key = key;
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
        if (string.IsNullOrEmpty(ciphertext))
        {
            return ciphertext;
        }

        var cipherData = Convert.FromBase64String(ciphertext);

        if (cipherData.Length < IvSize + TagSize)
        {
            throw new ArgumentException("Invalid ciphertext format");
        }

        var iv = new byte[IvSize];
        var tag = new byte[TagSize];
        var ciphertextLength = cipherData.Length - IvSize - TagSize;
        var actualCipherText = new byte[ciphertextLength];

        Buffer.BlockCopy(cipherData, 0, iv, 0, IvSize);
        Buffer.BlockCopy(cipherData, IvSize, tag, 0, TagSize);
        Buffer.BlockCopy(cipherData, IvSize + TagSize, actualCipherText, 0, ciphertextLength);

        var plaintextBytes = new byte[ciphertextLength];
        try
        {
            using var aesGcm = new AesGcm(this._key, TagSize);
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
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        var iv = new byte[IvSize];
        RandomNumberGenerator.Fill(iv);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using (var aesGcm = new AesGcm(this._key, TagSize))
        {
            aesGcm.Encrypt(iv, plaintextBytes, ciphertext, tag);
        }

        var result = new byte[iv.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(tag, 0, result, iv.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, iv.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    #endregion

    private const int IvSize = 12;
    private const int TagSize = 16;
}