using System.Security.Cryptography;

namespace DKNet.Svc.Encryption;

public interface IAesEncryption
{
    #region Properties

    string Key { get; }

    #endregion

    #region Methods

    string DecryptString(string cipherText);
    string EncryptString(string plainText);

    #endregion
}

public sealed class AesEncryption : IAesEncryption, IDisposable
{
    #region Fields

    private readonly Aes _aes;
    private bool _disposed;
    private string? _keyString;

    #endregion

    #region Constructors

    public AesEncryption(string? keyString = null) =>
        _aes = string.IsNullOrEmpty(keyString) ? CreateAes() : CreateAesFromKey(keyString);

    #endregion

    #region Properties

    public string Key => _keyString!;

    #endregion

    #region Methods

    private Aes CreateAes()
    {
        var aes = Aes.Create();
        var key = aes.Key;
        var iv = aes.IV;

        _keyString = $"{Convert.ToBase64String(key)}:{Convert.ToBase64String(iv)}".ToBase64String();
        return aes;
    }

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

    public string DecryptString(string cipherText)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AesEncryption));
        var decryptor = _aes.CreateDecryptor();

        using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
            _aes.Dispose();
    }

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