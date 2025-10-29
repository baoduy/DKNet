using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption;

/// <summary>
///     Provides RSA public/private key encryption, decryption, signing and signature verification utilities.
///     Keys are stored and exchanged as Base64 encoded PKCS#1 DER blobs for simplicity. Intended for small payloads
///     (e.g. secrets / session keys); do not use directly for large data streams.
/// </summary>
public interface IRsaEncryption
{
    #region Properties

    /// <summary>
    ///     Gets the Base64 encoded public key (PKCS#1 DER format).
    /// </summary>
    string PublicKey { get; }

    /// <summary>
    ///     Gets the Base64 encoded private key (PKCS#1 DER format); <c>null</c> if constructed with only a public key.
    /// </summary>
    string? PrivateKey { get; }

    #endregion

    #region Methods

    /// <summary>
    ///     Decrypts the specified Base64 encoded cipher text using the private key.
    /// </summary>
    /// <param name="base64CipherText">The Base64 encoded cipher text produced by <see cref="Encrypt" />.</param>
    /// <returns>The decrypted UTF-8 plain text string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no private key is available for decryption.</exception>
    string Decrypt(string base64CipherText);

    /// <summary>
    ///     Encrypts the specified plain text using the public key (OAEP-SHA256 padding).
    /// </summary>
    /// <param name="plainText">The UTF-8 plain text to encrypt.</param>
    /// <returns>The encrypted data as a Base64 encoded cipher text string.</returns>
    string Encrypt(string plainText);

    /// <summary>
    ///     Signs the specified data using the private key and SHA256 with PKCS#1 padding.
    /// </summary>
    /// <param name="data">The UTF-8 text to sign.</param>
    /// <returns>The Base64 encoded signature.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no private key is available for signing.</exception>
    string Sign(string data);

    /// <summary>
    ///     Verifies that the Base64 encoded signature matches the specified data using SHA256 with PKCS#1 padding.
    /// </summary>
    /// <param name="data">The original UTF-8 text that was signed.</param>
    /// <param name="base64Signature">The Base64 encoded signature to verify.</param>
    /// <returns><c>true</c> if the signature is valid; otherwise, <c>false</c>.</returns>
    bool Verify(string data, string base64Signature);

    #endregion
}

/// <summary>
///     RSA implementation helper. Provides encryption/decryption and signing/verification over small text payloads.
///     Not intended for large bulk data; use hybrid (RSA + symmetric) approaches for large messages.
/// </summary>
public sealed class RsaEncryption : IRsaEncryption, IDisposable
{
    #region Fields

    private readonly bool _hasPrivate;
    private readonly RSA _rsa;

    private bool _disposed;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="RsaEncryption" /> class with a newly generated key pair.
    /// </summary>
    /// <param name="keySize">Key size in bits (default 2048). Common values: 2048, 3072, 4096.</param>
    public RsaEncryption(int keySize = 2048)
    {
        _rsa = RSA.Create(keySize);
        _hasPrivate = true;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RsaEncryption" /> class from an existing Base64 encoded PKCS#1 private
    ///     key.
    ///     Public key will be derived automatically.
    /// </summary>
    /// <param name="privateKeyBase64">The Base64 encoded PKCS#1 private key.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="privateKeyBase64" /> is null, empty, or whitespace.</exception>
    public RsaEncryption(string privateKeyBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyBase64);
        _rsa = RSA.Create();
        _rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        _hasPrivate = true;
    }

    private RsaEncryption(RSA rsa, bool hasPrivate)
    {
        _rsa = rsa;
        _hasPrivate = hasPrivate;
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the Base64 encoded private key (PKCS#1 DER) or <c>null</c> if this instance was created from only a public
    ///     key.
    /// </summary>
    public string? PrivateKey => _hasPrivate ? Convert.ToBase64String(_rsa.ExportRSAPrivateKey()) : null;

    /// <summary>
    ///     Gets the Base64 encoded public key (PKCS#1 DER).
    /// </summary>
    public string PublicKey => Convert.ToBase64String(_rsa.ExportRSAPublicKey());

    #endregion

    #region Methods

    /// <summary>
    ///     Decrypts a cipher text created by <see cref="Encrypt" /> using the private key (OAEP-SHA256).
    /// </summary>
    /// <param name="base64CipherText">The Base64 encoded cipher text.</param>
    /// <returns>The decrypted UTF-8 plain text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no private key is available.</exception>
    public string Decrypt(string base64CipherText)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RsaEncryption));
        if (!_hasPrivate) throw new InvalidOperationException("Private key not available for decryption.");

        var cipher = Convert.FromBase64String(base64CipherText);
        var plain = _rsa.Decrypt(cipher, RSAEncryptionPadding.OaepSHA256);
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>
    ///     Releases all resources used by the <see cref="RsaEncryption" /> instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _rsa.Dispose();
        _disposed = true;
    }

    /// <summary>
    ///     Encrypts the specified UTF-8 plain text using the public key (OAEP-SHA256).
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <returns>The encrypted data as a Base64 encoded string.</returns>
    public string Encrypt(string plainText)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RsaEncryption));
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = _rsa.Encrypt(bytes, RSAEncryptionPadding.OaepSHA256);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    ///     Creates a new <see cref="RsaEncryption" /> instance from a Base64 encoded PKCS#1 public key. Decrypt and Sign are
    ///     unavailable.
    /// </summary>
    /// <param name="publicKeyBase64">The Base64 encoded PKCS#1 public key.</param>
    /// <returns>A <see cref="RsaEncryption" /> instance containing only the public key.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="publicKeyBase64" /> is null, empty, or whitespace.</exception>
    public static RsaEncryption FromPublicKey(string publicKeyBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyBase64);
        var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKeyBase64), out _);
        return new RsaEncryption(rsa, false);
    }

    /// <summary>
    ///     Signs the specified UTF-8 data using the private key and SHA256 with PKCS#1 padding.
    /// </summary>
    /// <param name="data">The data to sign.</param>
    /// <returns>The Base64 encoded signature.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no private key is available.</exception>
    public string Sign(string data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RsaEncryption));
        if (!_hasPrivate) throw new InvalidOperationException("Private key not available for signing.");

        var bytes = Encoding.UTF8.GetBytes(data);
        var sig = _rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(sig);
    }

    /// <summary>
    ///     Verifies the supplied Base64 signature against the specified UTF-8 data using SHA256 with PKCS#1 padding.
    /// </summary>
    /// <param name="data">The original data that was signed.</param>
    /// <param name="base64Signature">The Base64 encoded signature to verify.</param>
    /// <returns><c>true</c> if the signature is valid; otherwise, <c>false</c>.</returns>
    public bool Verify(string data, string base64Signature)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RsaEncryption));
        var bytes = Encoding.UTF8.GetBytes(data);
        var sig = Convert.FromBase64String(base64Signature);
        return _rsa.VerifyData(bytes, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    #endregion
}