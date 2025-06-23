using DKNet.Fw.Extensions.Encryption;

namespace Fw.Extensions.Tests;

[TestClass]
public class StringEncryptionTests
{
    [DataTestMethod]
    [DataRow("test", false, DisplayName = "Plain text should return false")]
    [DataRow("SGVsbG8gd29ybGQ=", true, DisplayName = "Valid Base64 'Hello world' should return true")]
    [DataRow("true", false, DisplayName = "Boolean text should return false")]
    [DataRow("false", false, DisplayName = "Boolean text should return false")]
    [DataRow("12345", false, DisplayName = "Numeric text should return false")]
    [DataRow("dGVzdA==", true, DisplayName = "Valid Base64 'test' should return true")]
    [DataRow("SGVsbG8=", true, DisplayName = "Valid Base64 'Hello' should return true")]
    [DataRow("Invalid Base64!", false, DisplayName = "String with special characters should return false")]
    [DataRow("", false, DisplayName = "Empty string should return false")]
    [DataRow("abc===", false, DisplayName = "Invalid padding should return false")]
    public void IsBase64StringValidatesInputReturnsExpectedResult(string value, bool expectedResult)
    {
        // Arrange & Act
        var result = value.IsBase64String();

        // Assert
        Assert.AreEqual(expectedResult, result, $"Failed for input: {value}");
    }

    [DataTestMethod]
    [DataRow("Hello World", "SGVsbG8gV29ybGQ=", DisplayName = "Convert 'Hello World' to Base64")]
    [DataRow("Test123!@#", "VGVzdDEyMyFAIw==", DisplayName = "Convert string with special characters to Base64")]
    [DataRow("", "", DisplayName = "Convert empty string to Base64")]
    public void ToBase64StringWithValidInputReturnsExpectedEncoding(string input, string expectedBase64)
    {
        // Arrange & Act
        var result = input.ToBase64String();

        // Assert
        Assert.AreEqual(expectedBase64, result, $"Failed to encode: {input}");
    }

    [DataTestMethod]
    [DataRow("SGVsbG8gV29ybGQ=", "Hello World", DisplayName = "Decode 'Hello World' from Base64")]
    [DataRow("VGVzdDEyMyFAIw==", "Test123!@#", DisplayName = "Decode string with special characters from Base64")]
    [DataRow("", "", DisplayName = "Decode empty string from Base64")]
    public void FromBase64StringWithValidInputReturnsExpectedString(string base64Input, string expectedString)
    {
        // Arrange & Act
        var result = base64Input.FromBase64String();

        // Assert
        Assert.AreEqual(expectedString, result, $"Failed to decode: {base64Input}");
    }

    [TestMethod]
    public void GenerateAesKeyWhenCalledReturnsValidKey()
    {
        // Arrange & Act
        var key = StringEncryption.GenerateAesKey();

        // Assert
        Assert.IsNotNull(key, "Generated key should not be null");
        Assert.IsTrue(key.Length > 0, "Generated key should not be empty");
    }

    [TestMethod]
    public void AesEncryptionDecryptionWithValidInputPerformsRoundtripSuccessfully()
    {
        // Arrange
        var plainText = "This is a test message with special chars !@#$%^&*()";
        var key = StringEncryption.GenerateAesKey();

        // Act
        var encryptedText = plainText.ToAesString(key);
        var decryptedText = encryptedText.FromAesString(key);

        // Assert
        Assert.AreNotEqual(plainText, encryptedText, "Encrypted text should differ from plain text");
        Assert.AreEqual(plainText, decryptedText, "Decrypted text should match original plain text");
    }

    [DataTestMethod]
    [DataRow("This is a test message.", "", DisplayName = "Empty key should throw exception")]
    [DataRow("", "InvalidKeyString", DisplayName = "Empty message with invalid key should throw exception")]
    public void ToAesStringWithInvalidInputThrowsArgumentException(string plainText, string key)
    {
        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentException>(
            () => plainText.ToAesString(key),
            $"Should throw ArgumentException for plainText: '{plainText}', key: '{key}'"
        );
        Assert.IsTrue(exception.Message.Length > 0, "Exception should have a message");
    }

    [DataTestMethod]
    [DataRow("InvalidCipherText", "", DisplayName = "Empty key should throw exception")]
    [DataRow("", "InvalidKeyString", DisplayName = "Empty cipher text with invalid key should throw exception")]
    public void FromAesStringWithInvalidInputThrowsArgumentException(string cipherText, string key)
    {
        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentException>(
            () => cipherText.FromAesString(key),
            $"Should throw ArgumentException for cipherText: '{cipherText}', key: '{key}'"
        );
        Assert.IsTrue(exception.Message.Length > 0, "Exception should have a message");
    }

    [TestMethod]
    public void FromAesStringWithInvalidKeyFormatThrowsArgumentException()
    {
        // Arrange
        var cipherText = "SGVsbG8gV29ybGQ=";
        var invalidKey = "InvalidKeyString";

        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentException>(
            () => cipherText.FromAesString(invalidKey),
            "Should throw ArgumentException for invalid key format"
        );
        Assert.IsTrue(exception.Message.Length > 0, "Exception should have a message");
    }

    [TestMethod]
    public void ToSha256WithValidInputReturnsExpectedHash()
    {
        // Arrange
        var input = "hello world";
        var expectedHash = "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9";

        // Act
        var actualHash = input.ToSha256();

        // Assert
        Assert.AreEqual(expectedHash, actualHash, "Hash should match expected value");
    }

    [TestMethod]
    public void ToSha256WithEmptyStringThrowsArgumentException()
    {
        // Arrange
        var input = "";

        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentException>(
            () => input.ToSha256(),
            "Should throw ArgumentException for empty string"
        );
        Assert.IsTrue(exception.Message.Length > 0, "Exception should have a message");
    }

    [TestMethod]
    public void ToSha256WithSameInputReturnsSameHash()
    {
        // Arrange
        var input1 = "test";
        var input2 = "test";

        // Act
        var hash1 = input1.ToSha256();
        var hash2 = input2.ToSha256();

        // Assert
        Assert.AreEqual(hash1, hash2, "Hash values should be identical for same input");
    }
}
