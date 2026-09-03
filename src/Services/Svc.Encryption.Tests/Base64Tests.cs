using DKNet.Svc.Encryption;
using Shouldly;

namespace Svc.Encryption.Tests;

public class Base64Tests
{
    #region Methods

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

    [Theory]
    [InlineData("test", true)]
    [InlineData("SGVsbG8gd29ybGQ=", true)]
    // Pre-checks that special-cased the boolean words were dropped: validity is now purely
    // base64 alphabet + length. "true"/"TRUE" are 4 chars of valid alphabet -> valid.
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    // "false"/"FALSE" are 5 characters -> never valid base64 regardless of casing or the
    // removed pre-check, since base64 data length can never be 4n+1.
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("12345", false)]
    [InlineData("dGVzdA==", true)]
    [InlineData("SGVsbG8=", true)]
    [InlineData("Invalid Base64!", false)]
    [InlineData("", false)]
    [InlineData("abc===", false)]
    [InlineData("YWJj", true)]
    [InlineData("Ωμέγα", false)] // non-ASCII text, outside the base64 alphabet
    public void IsBase64StringValidatesInputReturnsExpectedResult(string value, bool expectedResult)
    {
        // Arrange & Act
        var result = value.IsBase64String();

        // Assert
        result.ShouldBe(expectedResult, $"Failed for input: {value}");
    }

    [Fact]
    public void IsBase64StringWithWhitespaceInsideEncodedValueIsAcceptedAndDecodes()
    {
        // Arrange
        const string original = "Acme Pte Ltd";
        var encoded = original.ToBase64String();
        var withSpace = encoded[..4] + " " + encoded[4..];

        // Act & Assert
        withSpace.IsBase64String().ShouldBeTrue();
        withSpace.FromBase64String().ShouldBe(original);
    }

    [Fact]
    public void IsBase64StringWithBooleanWordDecodesWithoutThrowing()
    {
        // Arrange
        const string value = "True";

        // Act & Assert
        value.IsBase64String().ShouldBeTrue();
        Should.NotThrow(() => value.FromBase64String());
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

    #endregion
}