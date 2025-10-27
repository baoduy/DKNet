using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DKNet.Svc.Encryption.Tests;

public class EdgeCaseTests
{
    #region Methods

    // PasswordAesEncryption edges
    // [Fact]
    // public void PasswordAesEncryption_InvalidPassword_Throws()
    // {
    //     IPasswordAesEncryption enc = new PasswordAesEncryption();
    //     Should.Throw<ArgumentException>(() => enc.Encrypt("data", " "));
    // }
    //
    // [Fact]
    // public void PasswordAesEncryption_NullPlain_Throws()
    // {
    //     IPasswordAesEncryption enc = new PasswordAesEncryption();
    //     Should.Throw<ArgumentNullException>(() => enc.Encrypt(null!, "pass"));
    // }
    //
    // [Fact]
    // public void PasswordAesEncryption_InvalidPackageFormat_Throws()
    // {
    //     IPasswordAesEncryption enc = new PasswordAesEncryption();
    //     var bad = "only-two-parts".ToBase64String(); // decodes to string with no ':'
    //     Should.Throw<ArgumentException>(() => enc.Decrypt(bad, "pass"));
    // }

    // AesEncryption edges
    [Fact]
    public void AesEncryption_Decrypt_Invalid_Base64_Should_Throw()
    {
        using var aes = new AesEncryption();
        Should.Throw<FormatException>(() => aes.DecryptString("not-base64"));
    }

    [Fact]
    public void AesEncryption_Invalid_Key_ExtraParts_Throws()
    {
        var composite =
            $"{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}:{Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))}:{Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))}"
                .ToBase64String();
        Should.Throw<ArgumentException>(() => new AesEncryption(composite));
    }

    [Fact]
    public void AesGcmEncryption_Decrypt_Invalid_Format_Throws()
    {
        using var gcm = new AesGcmEncryption();
        var bad = "a:b".ToBase64String(); // only two parts after decoding
        Should.Throw<ArgumentException>(() => gcm.DecryptString(bad));
    }

    [Fact]
    public void AesGcmEncryption_Decrypt_With_Wrong_Instance_Key_Fails()
    {
        using var g1 = new AesGcmEncryption();
        using var g2 = new AesGcmEncryption();
        var cipher = g1.EncryptString("payload");
        Should.Throw<CryptographicException>(() => g2.DecryptString(cipher));
    }

    [Fact]
    public void AesGcmEncryption_InvalidKeyLength_Throws()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(10));
        Should.Throw<ArgumentException>(() => new AesGcmEncryption(shortKey));
    }

    // AesGcmEncryption edges
    [Fact]
    public void AesGcmEncryption_KeyWithColon_Throws()
    {
        var badKey =
            "abcd:efgh".ToBase64String(); // Contains ':' after decode? Actually we need raw containing ':' not base64 decode
        Should.Throw<ArgumentException>(() => new AesGcmEncryption("abcd:efgh"));
    }

    // DI registration coverage
    [Fact]
    public void EncryptionSetup_Registers_Services()
    {
        var services = new ServiceCollection();
        services.AddEncryptionServices();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAesEncryption>().ShouldNotBeNull();
        provider.GetRequiredService<IAesGcmEncryption>().ShouldNotBeNull();
        provider.GetRequiredService<IShaHashing>().ShouldNotBeNull();
        provider.GetRequiredService<IHmacHashing>().ShouldNotBeNull();
        //provider.GetRequiredService<IPasswordAesEncryption>().ShouldNotBeNull();
        provider.GetRequiredService<IRsaEncryption>().ShouldNotBeNull();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromBase64String_NullOrWhitespace_ReturnsEmpty(string input)
    {
        input.FromBase64String().ShouldBe(string.Empty);
    }

    // HmacHashing edges
    [Fact]
    public void Hmac256Hashing_Hex_Output_And_CaseSensitivity()
    {
        using IHmacHashing hmac = new HmacHashing();
        var hex = hmac.ComputeSha256("msg", "secret", false);
        hex.ShouldMatch("^[0-9A-F]+$");
        hmac.VerifySha256("msg", "secret", hex, false).ShouldBeTrue();
        // Change one char
        var mutated = new string([.. hex.Select((c, i) => i == 0 ? c == 'a' ? 'b' : 'a' : c)]);
        hmac.VerifySha256("msg", "secret", mutated, false, false).ShouldBeFalse();
    }

    [Fact]
    public void Hmac512Hashing_Hex_Output_And_CaseSensitivity()
    {
        using IHmacHashing hmac = new HmacHashing();
        var hex = hmac.ComputeSha512("msg", "secret", false);
        hex.ShouldMatch("^[0-9A-F]+$");
        hmac.VerifySha512("msg", "secret", hex, false).ShouldBeTrue();
        // Change one char
        var mutated = new string([.. hex.Select((c, i) => i == 0 ? c == 'a' ? 'b' : 'a' : c)]);
        hmac.VerifySha512("msg", "secret", mutated, false, false).ShouldBeFalse();
    }

    // Base64 extension edge cases
    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("false")]
    [InlineData("FALSE")]
    public void IsBase64String_FalseTrueStrings_ReturnFalse(string value) => value.IsBase64String().ShouldBeFalse();

    [Fact]
    public void IsBase64String_InvalidCharacter_ReturnsFalse()
    {
        var s = "QUJD$EFG"; // '$' invalid
        s.IsBase64String().ShouldBeFalse();
    }

    [Fact]
    public void IsBase64String_InvalidEqualsInMiddle_ReturnsFalse()
    {
        var s = "QUJD=EFG"; // length 8, '=' in middle
        s.IsBase64String().ShouldBeFalse();
    }

    [Fact]
    public void RsaEncryption_Disposed_Operations_Throw()
    {
        var rsa = new RsaEncryption();
        var cipher = rsa.Encrypt("x");
        rsa.Dispose();
        Should.Throw<ObjectDisposedException>(() => rsa.Encrypt("y"));
        Should.Throw<ObjectDisposedException>(() => rsa.Decrypt(cipher));
        Should.Throw<ObjectDisposedException>(() => rsa.Sign("y"));
        Should.Throw<ObjectDisposedException>(() => rsa.Verify("x", cipher));
    }

    // RsaEncryption edges
    [Fact]
    public void RsaEncryption_Invalid_Private_Key_Throws()
    {
        var bad = "not-base64";
        Should.Throw<FormatException>(() => new RsaEncryption(bad));
    }

    [Fact]
    public void RsaEncryption_Tampered_Signature_Fails_Verify()
    {
        using var rsa = new RsaEncryption();
        var sig = rsa.Sign("hello");
        // mutate signature
        var bytes = Convert.FromBase64String(sig);
        bytes[0] ^= 0xFF;
        var tampered = Convert.ToBase64String(bytes);
        rsa.Verify("hello", tampered).ShouldBeFalse();
    }

    // ShaHashing edges
    [Fact]
    public void Sha256Hashing_UpperCaseAndVerifyCaseSensitive()
    {
        using var hg = new ShaHashing();
        var hashUpper = hg.ComputeSha256("abc", true);
        hashUpper.ShouldBe(hashUpper.ToUpperInvariant());
        hg.VerifySha256("abc", hashUpper, false).ShouldBeTrue();
        hg.VerifySha256("Abc", hashUpper, false).ShouldBeFalse();
    }

    [Fact]
    public void Sha256Hashing_Verify_Throws_On_EmptyExpected()
    {
        using var hg = new ShaHashing();
        Should.Throw<ArgumentException>(() => hg.VerifySha256("abc", " "));
    }

    [Fact]
    public void Sha512Hashing_UpperCaseAndVerifyCaseSensitive()
    {
        using var hg = new ShaHashing();
        var hashUpper = hg.ComputeSha512("abc", true);
        hashUpper.ShouldBe(hashUpper.ToUpperInvariant());
        hg.VerifySha512("abc", hashUpper, false).ShouldBeTrue();
        hg.VerifySha512("Abc", hashUpper, false).ShouldBeFalse();
    }

    [Fact]
    public void Sha512Hashing_Verify_Throws_On_EmptyExpected()
    {
        using var hg = new ShaHashing();
        Should.Throw<ArgumentException>(() => hg.VerifySha512("abc", " "));
    }

    #endregion
}