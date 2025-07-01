using DKNet.Fw.Extensions.Encryption;

namespace Fw.Extensions.Tests;

public class StringEncryptionTests
{
    [Theory]
    [InlineData("test", false)]
    [InlineData("SGVsbG8gd29ybGQ=", true)]
    [InlineData("true", false)]
    [InlineData("false", false)]
    [InlineData("12345", false)]
    [InlineData("dGVzdA==", true)]
    [InlineData("SGVsbG8=", true)]
    [InlineData("Invalid Base64!", false)]
    [InlineData("", false)]
    [InlineData("abc===", false)]
    public void IsBase64StringValidatesInputReturnsExpectedResult(string value, bool expectedResult)
    {
        // Arrange & Act
        var result = value.IsBase64String();

        // Assert
        result.ShouldBe(expectedResult, $"Failed for input: {value}");
    }

    [Theory]
    [InlineData("Hello World", "SGVsbG8gV29ybGQ=")]
    [InlineData("Test123!@#", "VGVzdDEyMyFAIw==")]
    [InlineData("", "")]
    public void ToBase64StringWithValidInputReturnsExpectedEncoding(string input, string expectedBase64)
    {
        // Arrange & Act
        var result = input.ToBase64String();

        // Assert
        result.ShouldBe(expectedBase64, $"Failed to encode: {input}");
    }

    [Theory]
    [InlineData("SGVsbG8gV29ybGQ=", "Hello World")]
    [InlineData("VGVzdDEyMyFAIw==", "Test123!@#")]
    [InlineData("", "")]
    public void FromBase64StringWithValidInputReturnsExpectedString(string base64Input, string expectedString)
    {
        // Arrange & Act
        var result = base64Input.FromBase64String();

        // Assert
        result.ShouldBe(expectedString, $"Failed to decode: {base64Input}");
    }

    [Fact]
    public void GenerateAesKeyWhenCalledReturnsValidKey()
    {
        // Arrange & Act
        var key = StringEncryption.GenerateAesKey();

        // Assert
        key.ShouldNotBeNull("Generated key should not be null");
        key.Length.ShouldBeGreaterThan(0, "Generated key should not be empty");
    }

    [Fact]
    public void AesEncryptionDecryptionWithValidInputPerformsRoundtripSuccessfully()
    {
        // Arrange
        var plainText = "This is a test message with special chars !@#$%^&*()";
        var key = StringEncryption.GenerateAesKey();

        // Act
        var encryptedText = plainText.ToAesString(key);
        var decryptedText = encryptedText.FromAesString(key);

        // Assert
        encryptedText.ShouldNotBe(plainText, "Encrypted text should differ from plain text");
        decryptedText.ShouldBe(plainText, "Decrypted text should match original plain text");
    }

    [Theory]
    [InlineData("This is a test message.", "")]
    [InlineData("", "InvalidKeyString")]
    public void ToAesStringWithInvalidInputThrowsArgumentException(string plainText, string key)
    {
        // Act & Assert
        var exception = Should.Throw<ArgumentException>(
            () => plainText.ToAesString(key),
            $"Should throw ArgumentException for plainText: '{plainText}', key: '{key}'"
        );
        exception.Message.Length.ShouldBeGreaterThan(0, "Exception should have a message");
    }

    [Theory]
    [InlineData("InvalidCipherText", "")]
    [InlineData("", "InvalidKeyString")]
    public void FromAesStringWithInvalidInputThrowsArgumentException(string cipherText, string key)
    {
        // Act & Assert
        var exception = Should.Throw<ArgumentException>(
            () => cipherText.FromAesString(key),
            $"Should throw ArgumentException for cipherText: '{cipherText}', key: '{key}'"
        );
        exception.Message.Length.ShouldBeGreaterThan(0, "Exception should have a message");
    }

    [Fact]
    public void FromAesStringWithInvalidKeyFormatThrowsArgumentException()
    {
        // Arrange
        var cipherText = "SGVsbG8gV29ybGQ=";
        var invalidKey = "InvalidKeyString";

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(
            () => cipherText.FromAesString(invalidKey),
            "Should throw ArgumentException for invalid key format"
        );
        exception.Message.Length.ShouldBeGreaterThan(0, "Exception should have a message");
    }

    [Fact]
    public void ToSha256WithValidInputReturnsExpectedHash()
    {
        // Arrange
        var input = "hello world";
        var expectedHash = "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9";

        // Act
        var actualHash = input.ToSha256();

        // Assert
        actualHash.ShouldBe(expectedHash, "Hash should match expected value");
    }

    [Fact]
    public void ToSha256WithEmptyStringThrowsArgumentException()
    {
        // Arrange
        var input = "";

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(
            () => input.ToSha256(),
            "Should throw ArgumentException for empty string"
        );
        exception.Message.Length.ShouldBeGreaterThan(0, "Exception should have a message");
    }

    [Fact]
    public void ToSha256WithSameInputReturnsSameHash()
    {
        // Arrange
        var input1 = "test";
        var input2 = "test";

        // Act
        var hash1 = input1.ToSha256();
        var hash2 = input2.ToSha256();

        // Assert
        hash1.ShouldBe(hash2, "Hash values should be identical for same input");
    }
}