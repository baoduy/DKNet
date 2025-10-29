using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption;

/// <summary>
///     Provides RSA public/private key encryption, decryption and signing utilities.
///     Keys are stored / exchanged as Base64 encoded PKCS#1 DER blobs for simplicity.
/// </summary>
public interface IRsaEncryption
{
    #region Properties

    /// <summary>Base64 encoded public key (PKCS#1 DER)</summary>
    string PublicKey { get; }

    /// <summary>Base64 encoded private key (PKCS#1 DER). Null if constructed only with a public key.</summary>
    string? PrivateKey { get; }

    #endregion

    #region Methods

    string Decrypt(string base64CipherText);

    string Encrypt(string plainText);

    string Sign(string data);

    bool Verify(string data, string base64Signature);

    #endregion
}

/// <summary>
///     RSA implementation helper. NOT intended for very large payloads – only short secrets / session keys.
/// </summary>
public sealed class RsaEncryption : IRsaEncryption, IDisposable
{
    #region Fields

    private bool _disposed;
    private readonly bool _hasPrivate;
    private readonly RSA _rsa;

    #endregion

    #region Constructors

    /// <summary>
    ///     Create new instance generating fresh 2048-bit key pair.
    /// </summary>
    public RsaEncryption(int keySize = 2048)
    {
        this._rsa = RSA.Create(keySize);
        this._hasPrivate = true;
    }

    /// <summary>
    ///     Construct from existing Base64 encoded PKCS#1 private key. Public key will be derived.
    /// </summary>
    public RsaEncryption(string privateKeyBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyBase64);
        this._rsa = RSA.Create();
        this._rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        this._hasPrivate = true;
    }

    private RsaEncryption(RSA rsa, bool hasPrivate)
    {
        this._rsa = rsa;
        this._hasPrivate = hasPrivate;
    }

    #endregion

    #region Properties

    public string PublicKey => Convert.ToBase64String(this._rsa.ExportRSAPublicKey());

    public string? PrivateKey => this._hasPrivate ? Convert.ToBase64String(this._rsa.ExportRSAPrivateKey()) : null;

    #endregion

    #region Methods

    public string Decrypt(string base64CipherText)
    {
        ObjectDisposedException.ThrowIf(this._disposed, nameof(RsaEncryption));
        if (!this._hasPrivate)
        {
            throw new InvalidOperationException("Private key not available for decryption.");
        }

        var cipher = Convert.FromBase64String(base64CipherText);
        var plain = this._rsa.Decrypt(cipher, RSAEncryptionPadding.OaepSHA256);
        return Encoding.UTF8.GetString(plain);
    }

    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        this._rsa.Dispose();
        this._disposed = true;
    }

    public string Encrypt(string plainText)
    {
        ObjectDisposedException.ThrowIf(this._disposed, nameof(RsaEncryption));
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = this._rsa.Encrypt(bytes, RSAEncryptionPadding.OaepSHA256);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    ///     Construct with only a public key (Base64 PKCS#1). Signing and decrypting will not be available.
    /// </summary>
    public static RsaEncryption FromPublicKey(string publicKeyBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyBase64);
        var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKeyBase64), out _);
        return new RsaEncryption(rsa, false);
    }

    public string Sign(string data)
    {
        ObjectDisposedException.ThrowIf(this._disposed, nameof(RsaEncryption));
        if (!this._hasPrivate)
        {
            throw new InvalidOperationException("Private key not available for signing.");
        }

        var bytes = Encoding.UTF8.GetBytes(data);
        var sig = this._rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(sig);
    }

    public bool Verify(string data, string base64Signature)
    {
        ObjectDisposedException.ThrowIf(this._disposed, nameof(RsaEncryption));
        var bytes = Encoding.UTF8.GetBytes(data);
        var sig = Convert.FromBase64String(base64Signature);
        return this._rsa.VerifyData(bytes, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    #endregion
}