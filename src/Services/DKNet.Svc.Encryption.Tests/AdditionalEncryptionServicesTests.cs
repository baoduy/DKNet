using System.Security.Cryptography;
using System.Text;
using Shouldly;

namespace DKNet.Svc.Encryption.Tests;

public class AdditionalEncryptionServicesTests
{
    [Fact]
    public void ShaHashing_Computes_And_Verifies()
    {
        using IShaHashing hash = new ShaHashing();
        var text = "sample";
        var h1 = hash.ComputeHash(text, HashAlgorithmKind.Sha256);
        h1.ShouldNotBeNullOrWhiteSpace();
        hash.VerifyHash(text, h1).ShouldBeTrue();
        hash.VerifyHash(text + "x", h1).ShouldBeFalse();
    }

    [Fact]
    public void ShaHashing_Dispose_Blocks_Further_Use()
    {
        var hash = new ShaHashing();
        var sig = hash.ComputeHash("a");
        sig.ShouldNotBeNullOrWhiteSpace();
        hash.Dispose();
        Should.Throw<ObjectDisposedException>(() => hash.ComputeHash("b"));
        Should.Throw<ObjectDisposedException>(() => hash.VerifyHash("b", sig));
    }

    [Fact]
    public void HmacHashing_Computes_And_Verifies()
    {
        using IHmacHashing hmac = new HmacHashing();
        var sig = hmac.Compute("message", "secret");
        sig.ShouldNotBeNullOrWhiteSpace();
        hmac.Verify("message", "secret", sig).ShouldBeTrue();
        hmac.Verify("message2", "secret", sig).ShouldBeFalse();
    }

    [Fact]
    public void HmacHashing_Dispose_Blocks_Further_Use()
    {
        var hmac = new HmacHashing();
        var sig = hmac.Compute("m", "k");
        sig.ShouldNotBeNullOrWhiteSpace();
        hmac.Dispose();
        Should.Throw<ObjectDisposedException>(() => hmac.Compute("m2", "k"));
        Should.Throw<ObjectDisposedException>(() => hmac.Verify("m", "k", sig));
    }

    [Fact]
    public void RsaEncryption_EndToEnd_And_PublicKey_Verification()
    {
        using var rsa = new RsaEncryption();
        var plain = "Hello RSA";
        var cipher = rsa.Encrypt(plain);
        rsa.Decrypt(cipher).ShouldBe(plain);
        var signature = rsa.Sign(plain);
        signature.ShouldNotBeNullOrWhiteSpace();
        var publicOnly = RsaEncryption.FromPublicKey(rsa.PublicKey);
        publicOnly.Verify(plain, signature).ShouldBeTrue();
        // Public only cannot decrypt
        Should.Throw<InvalidOperationException>(() => publicOnly.Decrypt(cipher));
        // Public only cannot sign
        Should.Throw<InvalidOperationException>(() => publicOnly.Sign(plain));
    }
    //
    // [Fact]
    // public void PasswordAesEncryption_RoundTrip()
    // {
    //     IPasswordAesEncryption enc = new PasswordAesEncryption();
    //     var password = "StrongP@ssw0rd";
    //     var plain = new string('Z', 1024);
    //     var cipher = enc.Encrypt(plain, password);
    //     cipher.ShouldNotBeNullOrWhiteSpace();
    //     enc.Decrypt(cipher, password).ShouldBe(plain);
    //     Should.Throw<CryptographicException>(() => enc.Decrypt(cipher, password + "!"));
    // }

    [Fact]
    public void AesGcmEncryption_RoundTrip_And_Tamper()
    {
        using IAesGcmEncryption gcm = new AesGcmEncryption();
        var cipherPackage = gcm.EncryptString("GCM payload");
        gcm.DecryptString(cipherPackage).ShouldBe("GCM payload");
        var raw = cipherPackage.FromBase64String();
        var chars = raw.ToCharArray();
        chars[^1] = chars[^1] == 'A' ? 'B' : 'A';
        var tampered = new string(chars).ToBase64String();
        Should.Throw<CryptographicException>(() => gcm.DecryptString(tampered));
    }

    [Fact]
    public void AesGcmEncryption_Dispose_Blocks_Further_Use()
    {
        var gcm = new AesGcmEncryption();
        var cipher = gcm.EncryptString("abc");
        gcm.DecryptString(cipher).ShouldBe("abc");
        gcm.Dispose();
        Should.Throw<ObjectDisposedException>(() => gcm.EncryptString("x"));
        Should.Throw<ObjectDisposedException>(() => gcm.DecryptString(cipher));
    }

    [Fact]
    public void AesGcmEncryption_Can_Reconstruct_From_Key()
    {
        using var g1 = new AesGcmEncryption();
        var key = g1.Key;
        var cipher = g1.EncryptString("hello");
        using var g2 = new AesGcmEncryption(key);
        g2.DecryptString(cipher).ShouldBe("hello");
    }

    [Fact]
    public void AesGcmEncryption_Encrypt_With_Different_Key_Throws()
    {
        using var g1 = new AesGcmEncryption();
        using var g2 = new AesGcmEncryption();
        Should.Throw<InvalidOperationException>(() => g1.Encrypt("x", g2.Key));
    }

    [Fact]
    public void AesGcmEncryption_Wrapper_Encrypt_Decrypt_Works()
    {
        using var gcm = new AesGcmEncryption();
        var key = gcm.Key;
        var cipher = gcm.Encrypt("wrapper", key);
        gcm.Decrypt(cipher, key).ShouldBe("wrapper");
    }

    [Fact]
    public void AesGcmEncryption_AssociatedData_RoundTrip_And_Mismatch()
    {
        using var gcm = new AesGcmEncryption();
        var ad = Encoding.UTF8.GetBytes("meta");
        var cipher = gcm.EncryptString("data", ad);
        gcm.DecryptString(cipher, ad).ShouldBe("data");
        var badAd = Encoding.UTF8.GetBytes("META");
        Should.Throw<CryptographicException>(() => gcm.DecryptString(cipher, badAd));
    }

    [Fact]
    public void AesGcmEncryption_Invalid_Base64_Key_Throws()
    {
        Should.Throw<FormatException>(() => new AesGcmEncryption("***notbase64***"));
    }
}