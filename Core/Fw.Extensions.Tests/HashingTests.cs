using DKNet.Fw.Extensions.Encryption;

namespace Fw.Extensions.Tests;

[TestClass]
public class HashingTests
{
    [TestMethod]
    public void ToCmd5ShouldReturnCorrectHmacHash()
    {
        // Arrange
        var input = "hello world";
        var key = "secret";
        var expectedHash = "734cc62f32841568f45715aeb9f4d7891324e6d948e4c6c60c0621cdac48623a";

        // Act
        var actualHash = input.ToCmd5(key);

        // Assert
        Assert.AreEqual(expectedHash, actualHash);
    }

    [TestMethod]
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
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ToCmd5ShouldHandleEmptyInput()
    {
        // Arrange
        var input = "";
        var key = "secret";

        input.ToCmd5(key);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void HashCmd5KeyNullThrowsArgumentNullException()
    {
        var value = "value";
        value.ToCmd5(key: null);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void HashCmd5ValueNullThrowsArgumentNullException()
    {
        var key = "key";
        ((string)null).ToCmd5(key);
    }
}