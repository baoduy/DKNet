using System.Security.Cryptography;

namespace DKNet.Svc.Encryption;

/// <summary>
///     Interface for AES encryption operations.
/// </summary>
public interface IAesEncryption
{
    #region Properties

    /// <summary>
    ///     Gets the base64-encoded key and IV string for this encryption instance.
    /// </summary>
    string Key { get; }

    #endregion

    #region Methods

    /// <summary>
    ///     Decrypts the specified base64-encoded cipher text string.
    /// </summary>
    /// <param name="cipherText">The base64-encoded cipher text to decrypt.</param>
    /// <returns>The decrypted plain text string.</returns>
    string DecryptString(string cipherText);

    /// <summary>
    ///     Encrypts the specified plain text string.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <returns>The encrypted cipher text as a base64-encoded string.</returns>
    string EncryptString(string plainText);

    #endregion
}

/// <summary>
///     Provides AES encryption and decryption functionality.
/// </summary>
public sealed class AesEncryption : IAesEncryption, IDisposable
{
    #region Fields

    private readonly Aes _aes;
    private bool _disposed;
    private string? _keyString;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="AesEncryption" /> class.
    /// </summary>
    /// <param name="keyString">The base64-encoded key and IV string, or <c>null</c> to generate a new key and IV.</param>
    public AesEncryption(string? keyString = null) => _aes =
        string.IsNullOrEmpty(keyString) ? CreateAes() : CreateAesFromKey(keyString);

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the base64-encoded key and IV string for this encryption instance.
    /// </summary>
    public string Key => _keyString!;

    #endregion

    #region Methods

    /// <summary>
    ///     Creates a new AES instance and generates a new key and IV.
    /// </summary>
    /// <returns>A new <see cref="Aes" /> instance with a generated key and IV.</returns>
    private Aes CreateAes()
    {
        var aes = Aes.Create();
        var key = aes.Key;
        var iv = aes.IV;

        _keyString = $"{Convert.ToBase64String(key)}:{Convert.ToBase64String(iv)}".ToBase64String();
        return aes;
    }

    /// <summary>
    ///     Creates a new AES instance from the specified base64-encoded key and IV string.
    /// </summary>
    /// <param name="keyString">The base64-encoded key and IV string.</param>
    /// <returns>A new <see cref="Aes" /> instance with the specified key and IV.</returns>
    /// <exception cref="ArgumentException">Thrown if the key string is invalid or not in the correct format.</exception>
    private Aes CreateAesFromKey(string keyString)
    {
        _keyString = keyString;
        ArgumentException.ThrowIfNullOrWhiteSpace(keyString);

        var keys = keyString.FromBase64String().Split(":");
        if (keys.Length != 2) throw new ArgumentException("Invalid key string format.", nameof(keyString));

        var key = Convert.FromBase64String(keys[0]);
        var iv = Convert.FromBase64String(keys[1]);

        var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        return aes;
    }

    /// <summary>
    ///     Decrypts the specified base64-encoded cipher text string.
    /// </summary>
    /// <param name="cipherText">The base64-encoded cipher text to decrypt.</param>
    /// <returns>The decrypted plain text string.</returns>
    public string DecryptString(string cipherText)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AesEncryption));
        var decryptor = _aes.CreateDecryptor();

        using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }

    /// <summary>
    ///     Releases all resources used by the <see cref="AesEncryption" /> instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    /// <summary>
    ///     Releases the unmanaged resources used by the <see cref="AesEncryption" /> and optionally releases the managed
    ///     resources.
    /// </summary>
    /// <param name="disposing">
    ///     true to release both managed and unmanaged resources; false to release only unmanaged
    ///     resources.
    /// </param>
    private void Dispose(bool disposing)
    {
        if (disposing) _aes.Dispose();
    }

    /// <summary>
    ///     Encrypts the specified plain text string.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <returns>The encrypted cipher text as a base64-encoded string.</returns>
    public string EncryptString(string plainText)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AesEncryption));

        var encryptor = _aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        var encrypted = msEncrypt.ToArray();
        return Convert.ToBase64String(encrypted);
    }

    #endregion
}