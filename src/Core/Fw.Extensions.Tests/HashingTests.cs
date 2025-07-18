using DKNet.Fw.Extensions.Encryption;

namespace Fw.Extensions.Tests;

public class HashingTests
{
    [Fact]
    public void ToCmd5ShouldReturnCorrectHmacHash()
    {
        // Arrange
        var input = "hello world";
        var key = "secret";
        var expectedHash = "734cc62f32841568f45715aeb9f4d7891324e6d948e4c6c60c0621cdac48623a";

        // Act
        var actualHash = input.ToCmd5(key);

        // Assert
        actualHash.ShouldBe(expectedHash);
    }

    [Fact]
    public void ToCmd5ShouldReturnDifferentHashesForDifferentKeys()
    {
        // Arrange
        var input = "hello world";
        var key1 = "secret1";
        var key2 = "secret2";

        // Act
        var hash1 = input.ToCmd5(key1);
        var hash2 = input.ToCmd5(key2);

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ToCmd5ShouldHandleEmptyInput()
    {
        // Arrange
        var input = "";
        var key = "secret";

        // Act & Assert
        Should.Throw<ArgumentException>(() => input.ToCmd5(key));
    }

    [Fact]
    public void HashCmd5KeyNullThrowsArgumentNullException()
    {
        var value = "value";
        Should.Throw<ArgumentNullException>(() => value.ToCmd5(null));
    }

    [Fact]
    public void HashCmd5ValueNullThrowsArgumentNullException()
    {
        var key = "key";
        Should.Throw<ArgumentNullException>(() => ((string)null).ToCmd5(key));
    }
}