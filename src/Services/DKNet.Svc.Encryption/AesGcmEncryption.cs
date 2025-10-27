using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption;

public interface IAesGcmEncryption : IDisposable // now disposable due to internal cache
{
    #region Properties

    string Key { get; } // added to mirror AesEncryption pattern

    #endregion

    #region Methods

    string Decrypt(string cipherPackage, string base64Key, byte[]? associatedData = null);

    /// <summary>Decrypt previously encrypted package.</summary>
    string DecryptString(string cipherPackage, byte[]? associatedData = null); // new primary API

    // Backward-compatible wrappers
    string Encrypt(string plainText, string base64Key, byte[]? associatedData = null);

    /// <summary>
    ///     Encrypt plainText with provided Base64 key. Returns Base64 of (nonce:tag:cipher), each part base64 then
    ///     wrapped again.
    /// </summary>
    string EncryptString(string plainText, byte[]? associatedData = null); // new primary API

    #endregion
}

/// <summary>
///     AES-GCM authenticated encryption (AEAD). Output layout: base64(nonce):base64(tag):base64(cipher) all concatenated
///     with ':' then Base64 wrapped again for safe transport.
///     Caches AesGcm instances per key to avoid repeated allocations when the same key is reused.
/// </summary>
public sealed class AesGcmEncryption : IAesGcmEncryption
{
    #region Fields

    private readonly AesGcm _aesGcm;
    private bool _disposed;

    #endregion

    #region Constructors

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

    public string Key { get; }

    #endregion

    #region Methods

    public string Decrypt(string cipherPackage, string base64Key, byte[]? associatedData = null)
    {
        if (!string.Equals(base64Key, Key, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Provided key does not match instance key. Create a new instance with the desired key or use DecryptString().");
        return DecryptString(cipherPackage, associatedData);
    }

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

    public void Dispose()
    {
        if (_disposed) return;
        _aesGcm.Dispose();
        _disposed = true;
    }

    // Backward-compatible wrappers (will ignore supplied key and use this instance key)
    public string Encrypt(string plainText, string base64Key, byte[]? associatedData = null)
    {
        if (!string.Equals(base64Key, Key, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Provided key does not match instance key. Create a new instance with the desired key or use EncryptString().");
        return EncryptString(plainText, associatedData);
    }

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

    private const int KeySize = 32; // 256-bit key
    private const int NonceSize = 12; // 96-bit nonce recommended for GCM
    private const int TagSize = 16; // 128-bit tag
}