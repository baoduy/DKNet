using Shouldly;

namespace DKNet.Svc.Encryption.Tests;

public class Base64Tests
{
    [Theory]
    [InlineData("test", true)] // valid base64 (decodes to char sequences) per current IsBase64String logic
    [InlineData("SGVsbG8gd29ybGQ=", true)]
    [InlineData("true", false)]
    [InlineData("false", false)]
    [InlineData("12345", false)]
    [InlineData("dGVzdA==", true)]
    [InlineData("SGVsbG8=", true)]
    [InlineData("Invalid Base64!", false)]
    [InlineData("", false)]
    [InlineData("abc===", false)]
    [InlineData("YWJj", true)]
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
}