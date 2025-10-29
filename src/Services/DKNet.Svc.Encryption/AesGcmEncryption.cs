using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption;

/// <summary>
///     Interface for AES-GCM encryption and decryption operations.
/// </summary>
public interface IAesGcmEncryption : IDisposable
{
    #region Properties

    /// <summary>
    ///     Gets the base64-encoded key for this encryption instance.
    /// </summary>
    string Key { get; }

    #endregion

    #region Methods

    /// <summary>
    ///     Decrypts the specified cipher package using the provided base64 key and optional associated data.
    /// </summary>
    /// <param name="cipherPackage">The base64-encoded cipher package to decrypt.</param>
    /// <param name="base64Key">The base64-encoded key to use for decryption.</param>
    /// <param name="associatedData">Optional additional data to authenticate.</param>
    /// <returns>The decrypted plain text string.</returns>
    string Decrypt(string cipherPackage, string base64Key, byte[]? associatedData = null);

    /// <summary>
    ///     Decrypts a previously encrypted package using the instance key and optional associated data.
    /// </summary>
    /// <param name="cipherPackage">The base64-encoded cipher package to decrypt.</param>
    /// <param name="associatedData">Optional additional data to authenticate.</param>
    /// <returns>The decrypted plain text string.</returns>
    string DecryptString(string cipherPackage, byte[]? associatedData = null);

    /// <summary>
    ///     Encrypts the specified plain text using the provided base64 key and optional associated data.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <param name="base64Key">The base64-encoded key to use for encryption.</param>
    /// <param name="associatedData">Optional additional data to authenticate.</param>
    /// <returns>The encrypted cipher package as a base64-encoded string.</returns>
    string Encrypt(string plainText, string base64Key, byte[]? associatedData = null);

    /// <summary>
    ///     Encrypts the specified plain text using the instance key and optional associated data. Returns a base64-encoded
    ///     string containing nonce, tag, and cipher.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <param name="associatedData">Optional additional data to authenticate.</param>
    /// <returns>The encrypted cipher package as a base64-encoded string.</returns>
    string EncryptString(string plainText, byte[]? associatedData = null);

    #endregion
}

/// <summary>
///     Provides AES-GCM authenticated encryption and decryption functionality. Output layout:
///     base64(nonce):base64(tag):base64(cipher), all concatenated with ':' then base64-wrapped for safe transport. Caches
///     AesGcm instances per key to avoid repeated allocations when the same key is reused.
/// </summary>
public sealed class AesGcmEncryption : IAesGcmEncryption
{
    #region Fields

    private const int KeySize = 32; // 256-bit key
    private const int NonceSize = 12; // 96-bit nonce recommended for GCM
    private const int TagSize = 16; // 128-bit tag

    private readonly AesGcm _aesGcm;
    private bool _disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// </summary>
    /// <param name="key"></param>
    /// <exception cref="ArgumentException"></exception>
    public AesGcmEncryption(string? key = null)
    {
        byte[] keyBytes;
        if (string.IsNullOrWhiteSpace(key))
        {
            keyBytes = RandomNumberGenerator.GetBytes(KeySize);
            Key = Convert.ToBase64String(keyBytes);
        }
        else
        {
            // Expect just base64 key (no colon segmentation like AesEncryption uses key:iv)
            if (key.Contains(':', StringComparison.Ordinal))
                throw new ArgumentException("Invalid key format for AesGcm (':' not expected).", nameof(key));

            keyBytes = Convert.FromBase64String(key);
            if (keyBytes.Length is not (16 or 24 or 32))
                throw new ArgumentException("Key length must be 128/192/256 bits.", nameof(key));

            Key = key;
        }

        _aesGcm = new AesGcm(keyBytes, TagSize);
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets or sets Key.
    /// </summary>
    public string Key { get; }

    #endregion

    #region Methods

    /// <summary>
    ///     Decrypt operation.
    /// </summary>
    public string Decrypt(string cipherPackage, string base64Key, byte[]? associatedData = null)
    {
        if (!string.Equals(base64Key, Key, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Provided key does not match instance key. Create a new instance with the desired key or use DecryptString().");

        return DecryptString(cipherPackage, associatedData);
    }

    /// <summary>
    ///     DecryptString operation.
    /// </summary>
    public string DecryptString(string cipherPackage, byte[]? associatedData = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AesGcmEncryption));
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherPackage);
        var decoded = cipherPackage.FromBase64String();

        var parts = decoded.Split(':');
        if (parts.Length != 3) throw new ArgumentException("Invalid cipher package format", nameof(cipherPackage));

        var nonce = Convert.FromBase64String(parts[0]);
        var tag = Convert.FromBase64String(parts[1]);
        var cipher = Convert.FromBase64String(parts[2]);
        var plain = new byte[cipher.Length];

        lock (_aesGcm)
        {
            _aesGcm.Decrypt(nonce, cipher, tag, plain, associatedData);
        }

        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>
    ///     Dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _aesGcm.Dispose();
        _disposed = true;
    }

    // Backward-compatible wrappers (will ignore supplied key and use this instance key)
    /// <summary>
    ///     Encrypt operation.
    /// </summary>
    public string Encrypt(string plainText, string base64Key, byte[]? associatedData = null)
    {
        if (!string.Equals(base64Key, Key, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Provided key does not match instance key. Create a new instance with the desired key or use EncryptString().");

        return EncryptString(plainText, associatedData);
    }

    /// <summary>
    ///     EncryptString operation.
    /// </summary>
    public string EncryptString(string plainText, byte[]? associatedData = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AesGcmEncryption));
        ArgumentNullException.ThrowIfNull(plainText);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        lock (_aesGcm)
        {
            _aesGcm.Encrypt(nonce, plainBytes, cipher, tag, associatedData);
        }

        var packaged =
            $"{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(cipher)}";
        return packaged.ToBase64String();
    }

    #endregion
}