using System.Security.Cryptography;
using System.Text;

namespace DKNet.Svc.Encryption;

/// <summary>
/// Provides RSA public/private key encryption, decryption and signing utilities.
/// Keys are stored / exchanged as Base64 encoded PKCS#1 DER blobs for simplicity.
/// </summary>
public interface IRsaEncryption
{
    /// <summary>Base64 encoded public key (PKCS#1 DER)</summary>
    string PublicKey { get; }

    /// <summary>Base64 encoded private key (PKCS#1 DER). Null if constructed only with a public key.</summary>
    string? PrivateKey { get; }

    string Encrypt(string plainText);
    string Decrypt(string base64CipherText);
    string Sign(string data);
    bool Verify(string data, string base64Signature);
}

/// <summary>
/// RSA implementation helper. NOT intended for very large payloads – only short secrets / session keys.
/// </summary>
public sealed class RsaEncryption : IRsaEncryption, IDisposable
{
    private readonly RSA _rsa;
    private readonly bool _hasPrivate;
    private bool _disposed;

    /// <summary>
    /// Create new instance generating fresh 2048-bit key pair.
    /// </summary>
    public RsaEncryption(int keySize = 2048)
    {
        _rsa = RSA.Create(keySize);
        _hasPrivate = true;
    }

    /// <summary>
    /// Construct from existing Base64 encoded PKCS#1 private key. Public key will be derived.
    /// </summary>
    public RsaEncryption(string privateKeyBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyBase64);
        _rsa = RSA.Create();
        _rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        _hasPrivate = true;
    }

    /// <summary>
    /// Construct with only a public key (Base64 PKCS#1). Signing and decrypting will not be available.
    /// </summary>
    public static RsaEncryption FromPublicKey(string publicKeyBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyBase64);
        var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKeyBase64), out _);
        return new RsaEncryption(rsa, false);
    }

    private RsaEncryption(RSA rsa, bool hasPrivate)
    {
        _rsa = rsa;
        _hasPrivate = hasPrivate;
    }

    public string PublicKey => Convert.ToBase64String(_rsa.ExportRSAPublicKey());
    public string? PrivateKey => _hasPrivate ? Convert.ToBase64String(_rsa.ExportRSAPrivateKey()) : null;

    public string Encrypt(string plainText)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RsaEncryption));
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = _rsa.Encrypt(bytes, RSAEncryptionPadding.OaepSHA256);
        return Convert.ToBase64String(encrypted);
    }

    public string Decrypt(string base64CipherText)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RsaEncryption));
        if (!_hasPrivate) throw new InvalidOperationException("Private key not available for decryption.");
        var cipher = Convert.FromBase64String(base64CipherText);
        var plain = _rsa.Decrypt(cipher, RSAEncryptionPadding.OaepSHA256);
        return Encoding.UTF8.GetString(plain);
    }

    public string Sign(string data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RsaEncryption));
        if (!_hasPrivate) throw new InvalidOperationException("Private key not available for signing.");
        var bytes = Encoding.UTF8.GetBytes(data);
        var sig = _rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(sig);
    }

    public bool Verify(string data, string base64Signature)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RsaEncryption));
        var bytes = Encoding.UTF8.GetBytes(data);
        var sig = Convert.FromBase64String(base64Signature);
        return _rsa.VerifyData(bytes, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _rsa.Dispose();
        _disposed = true;
    }
}