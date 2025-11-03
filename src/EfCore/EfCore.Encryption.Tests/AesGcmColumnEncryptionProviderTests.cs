using System.Security.Cryptography;
using DKNet.EfCore.Encryption.Encryption;
using Shouldly;

namespace EfCore.Encryption.Tests;

public class AesGcmColumnEncryptionProviderTests
{
    #region Fields

    private readonly byte[] _validKey16 = new byte[16]; // 128-bit key
    private readonly byte[] _validKey24 = new byte[24]; // 192-bit key
    private readonly byte[] _validKey32 = new byte[32]; // 256-bit key

    #endregion

    #region Constructors

    public AesGcmColumnEncryptionProviderTests()
    {
        // Initialize with random bytes for testing
        RandomNumberGenerator.Fill(_validKey16);
        RandomNumberGenerator.Fill(_validKey24);
        RandomNumberGenerator.Fill(_validKey32);
    }

    #endregion

    #region Methods

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(23)]
    [InlineData(25)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(8)]
    [InlineData(64)]
    public void Constructor_WithInvalidKeyLength_ShouldThrowArgumentException(int keyLength)
    {
        // Arrange
        var invalidKey = new byte[keyLength];

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => new AesGcmColumnEncryptionProvider(invalidKey));
        exception.ParamName.ShouldBe("key");
        exception.Message.ShouldContain("Key length must be 16, 24, or 32 bytes");
    }

    [Fact]
    public void Constructor_WithNullKey_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new AesGcmColumnEncryptionProvider(null!))
            .ParamName.ShouldBe("key");
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void Constructor_WithValidKeyLength_ShouldSucceed(int keyLength)
    {
        // Arrange
        var validKey = new byte[keyLength];
        RandomNumberGenerator.Fill(validKey);

        // Act
        var provider = new AesGcmColumnEncryptionProvider(validKey);

        // Assert
        provider.ShouldNotBeNull();
    }

    [Fact]
    public void Decrypt_WithCorruptedCiphertext_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);
        const string plaintext = "Test message";
        var encrypted = provider.Encrypt(plaintext);

        // Corrupt the ciphertext
        var cipherBytes = Convert.FromBase64String(encrypted!);
        cipherBytes[20] ^= 0xFF; // Flip some bits
        var corruptedCiphertext = Convert.ToBase64String(cipherBytes);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => provider.Decrypt(corruptedCiphertext));
        exception.Message.ShouldContain("Decryption failed");
    }

    [Fact]
    public void Decrypt_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);

        // Act
        var result = provider.Decrypt(string.Empty);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ShouldThrowException()
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);
        const string invalidBase64 = "Not a valid base64 string!";

        // Act & Assert
        Should.Throw<FormatException>(() => provider.Decrypt(invalidBase64));
    }

    [Fact]
    public void Decrypt_WithNull_ShouldReturnNull()
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);

        // Act
        var result = provider.Decrypt(null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Decrypt_WithTooShortCiphertext_ShouldThrowArgumentException()
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);
        var shortData = new byte[10]; // Less than IV (12) + Tag (16) = 28 bytes
        var shortCiphertext = Convert.ToBase64String(shortData);

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => provider.Decrypt(shortCiphertext));
        exception.Message.ShouldContain("Invalid ciphertext format");
    }

    [Fact]
    public void Decrypt_WithWrongKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var key1 = new byte[32];
        var key2 = new byte[32];
        RandomNumberGenerator.Fill(key1);
        RandomNumberGenerator.Fill(key2);

        var provider1 = new AesGcmColumnEncryptionProvider(key1);
        var provider2 = new AesGcmColumnEncryptionProvider(key2);

        const string plaintext = "Secret message";

        // Act
        var encrypted = provider1.Encrypt(plaintext);

        // Assert
        var exception = Should.Throw<InvalidOperationException>(() => provider2.Decrypt(encrypted));
        exception.Message.ShouldContain("Decryption failed");
        exception.Message.ShouldContain("data may be corrupted or the key is incorrect");
    }

    [Fact]
    public void Encrypt_SamePlaintext_ShouldProduceDifferentCiphertext()
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);
        const string plaintext = "Test message";

        // Act
        var encrypted1 = provider.Encrypt(plaintext);
        var encrypted2 = provider.Encrypt(plaintext);

        // Assert
        encrypted1.ShouldNotBeNull();
        encrypted2.ShouldNotBeNull();
        encrypted1.ShouldNotBe(encrypted2); // Due to random IV, each encryption is unique
    }

    [Theory]
    [InlineData("Hello, World!")]
    [InlineData("Test123")]
    [InlineData("Special chars: !@#$%^&*()")]
    [InlineData("Unicode: 你好世界 🌍")]
    [InlineData(
        "A very long string that contains many characters to test encryption and decryption with larger text content")]
    public void Encrypt_ThenDecrypt_ShouldReturnOriginalPlaintext(string plaintext)
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);

        // Act
        var encrypted = provider.Encrypt(plaintext);
        var decrypted = provider.Decrypt(encrypted);

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void Encrypt_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);

        // Act
        var result = provider.Encrypt(string.Empty);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Encrypt_WithNewlineCharacters_ShouldEncryptAndDecryptCorrectly()
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);
        const string plaintext = "Line1\nLine2\r\nLine3";

        // Act
        var encrypted = provider.Encrypt(plaintext);
        var decrypted = provider.Decrypt(encrypted);

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void Encrypt_WithNull_ShouldReturnNull()
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);

        // Act
        var result = provider.Encrypt(null);

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("Hello, World!")]
    [InlineData("Test123")]
    [InlineData("Special chars: !@#$%^&*()")]
    [InlineData("Unicode: 你好世界 🌍")]
    public void Encrypt_WithPlaintext_ShouldReturnBase64String(string plaintext)
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);

        // Act
        var encrypted = provider.Encrypt(plaintext);

        // Assert
        encrypted.ShouldNotBeNull();
        encrypted.ShouldNotBe(plaintext);
        encrypted.Length.ShouldBeGreaterThan(0);

        // Verify it's valid base64
        Should.NotThrow(() => Convert.FromBase64String(encrypted));
    }

    [Fact]
    public void Encrypt_WithWhitespaceString_ShouldEncryptAndDecryptCorrectly()
    {
        // Arrange
        var provider = new AesGcmColumnEncryptionProvider(_validKey32);
        const string plaintext = "   ";

        // Act
        var encrypted = provider.Encrypt(plaintext);
        var decrypted = provider.Decrypt(encrypted);

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void EncryptDecrypt_WithDifferentKeyLengths_ShouldWork(int keyLength)
    {
        // Arrange
        var key = new byte[keyLength];
        RandomNumberGenerator.Fill(key);
        var provider = new AesGcmColumnEncryptionProvider(key);
        const string plaintext = "Test with different key lengths";

        // Act
        var encrypted = provider.Encrypt(plaintext);
        var decrypted = provider.Decrypt(encrypted);

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    #endregion
}