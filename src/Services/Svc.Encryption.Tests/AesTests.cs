using DKNet.Svc.Encryption;
using Shouldly;

// for extension methods

namespace Svc.Encryption.Tests;

public class AesTests
{
    #region Methods

    [Fact]
    public void Different_PlainTexts_Produce_Different_Ciphers()
    {
        using var aes = new AesEncryption();
        var c1 = aes.EncryptString("AAAAAA");
        var c2 = aes.EncryptString("AAA A A");
        c1.ShouldNotBe(c2);
    }

    [Fact]
    public void Encrypt_And_Decrypt_Empty_String()
    {
        using var aes = new AesEncryption();
        var cipher = aes.EncryptString(string.Empty);
        cipher.ShouldNotBeNull();
        aes.DecryptString(cipher).ShouldBe(string.Empty);
    }

    [Fact]
    public void Encrypt_Large_Text()
    {
        using var aes = new AesEncryption();
        var large = new string('A', 5000);
        var cipher = aes.EncryptString(large);
        cipher.IsBase64String().ShouldBeTrue();
        aes.DecryptString(cipher).ShouldBe(large);
    }

    [Fact]
    public void Instance_With_Provided_Key_Can_Decrypt_Other_Instance_Cipher()
    {
        using var source = new AesEncryption();
        var key = source.Key;
        var plain = "Cross instance message";
        var cipher = source.EncryptString(plain);
        using var clone = new AesEncryption(key);
        clone.Key.ShouldBe(key);
        clone.DecryptString(cipher).ShouldBe(plain);
    }

    [Fact]
    public void Invalid_Base64_Key_Throws_FormatException()
    {
        var bad = "@@@"; // invalid base64 (length not multiple of 4)
        Should.Throw<FormatException>(() => new AesEncryption(bad));
    }

    [Fact]
    public void Key_Reconstruction_Produces_Same_Internal_Key_And_IV()
    {
        using var original = new AesEncryption();
        var key = original.Key;
        var sample = "Functional equivalence";
        var cipher = original.EncryptString(sample);
        using var reconstructed = new AesEncryption(key);
        reconstructed.DecryptString(cipher).ShouldBe(sample);
        reconstructed.EncryptString(sample).ShouldBe(cipher);
    }

    [Fact]
    public void Missing_Colon_In_Key_Throws_ArgumentException()
    {
        var keyMaterial = "test".ToBase64String();
        Should.Throw<ArgumentException>(() => new AesEncryption(keyMaterial))
            .Message.ShouldContain("Invalid key string format");
    }

    [Fact]
    public void New_Instance_Without_Key_Generates_Key_And_Encrypts_And_Decrypts()
    {
        using var aes = new AesEncryption();
        aes.Key.ShouldNotBeNullOrWhiteSpace();
        var keyDecoded = aes.Key.FromBase64String();
        keyDecoded.ShouldContain(":");
        var parts = keyDecoded.Split(':');
        parts.Length.ShouldBe(2);
        parts[0].IsBase64String().ShouldBeTrue();
        parts[1].IsBase64String().ShouldBeTrue();
        var plain = "Hello World! 123";
        var cipher = aes.EncryptString(plain);
        cipher.ShouldNotBeNullOrWhiteSpace();
        cipher.IsBase64String().ShouldBeTrue();
        aes.DecryptString(cipher).ShouldBe(plain);
    }

    [Fact]
    public void Operations_After_Dispose_Should_Throw()
    {
        var aes = new AesEncryption();
        aes.Dispose();
        Should.Throw<ObjectDisposedException>(() => aes.EncryptString("x"));
        Should.Throw<ObjectDisposedException>(() => aes.DecryptString("AAAA"));
    }

    [Fact]
    public void Repeated_Encryption_With_Same_Instance_And_PlainText_Produces_Same_CipherText_Due_To_Fixed_IV()
    {
        using var aes = new AesEncryption();
        var plain = "Deterministic sample";
        var c1 = aes.EncryptString(plain);
        var c2 = aes.EncryptString(plain);
        c1.ShouldBe(c2);
    }

    [Fact]
    public void Whitespace_Key_Throws_And_Null_Generates_New_Key()
    {
        // Whitespace should throw because CreateAesFromKey is used then validation fails
        Should.Throw<ArgumentException>(() => new AesEncryption(" "));

        // Null should generate a new key (no exception)
        using var aes = new AesEncryption();
        aes.Key.ShouldNotBeNullOrWhiteSpace();
    }

    #endregion
}