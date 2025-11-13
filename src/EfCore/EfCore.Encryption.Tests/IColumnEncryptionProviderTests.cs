using DKNet.EfCore.Encryption.Interfaces;
using Shouldly;

namespace EfCore.Encryption.Tests;

// Mock implementation for testing
internal class MockColumnEncryptionProvider : IColumnEncryptionProvider
{
    #region Fields

    private readonly Dictionary<string, string> _storage = new();

    #endregion

    #region Methods

    public string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;

        return _storage.TryGetValue(ciphertext, out var plaintext)
            ? plaintext
            : throw new InvalidOperationException("Invalid ciphertext");
    }

    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;

        var encrypted = $"ENCRYPTED_{plaintext}";
        _storage[encrypted] = plaintext;
        return encrypted;
    }

    public bool WasEncrypted(string? value) => value != null && _storage.ContainsKey(value);

    #endregion
}

// Another mock that reverses strings
internal class ReverseEncryptionProvider : IColumnEncryptionProvider
{
    #region Methods

    public string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;

        return new string(ciphertext.Reverse().ToArray());
    }

    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;

        return new string(plaintext.Reverse().ToArray());
    }

    #endregion
}

public class IColumnEncryptionProviderTests
{
    #region Methods

    [Fact]
    public void IColumnEncryptionProvider_ShouldHaveDecryptMethod()
    {
        // Arrange
        var interfaceType = typeof(IColumnEncryptionProvider);

        // Act
        var method = interfaceType.GetMethod(nameof(IColumnEncryptionProvider.Decrypt));

        // Assert
        method.ShouldNotBeNull();
        method.ReturnType.ShouldBe(typeof(string));
        var parameters = method.GetParameters();
        parameters.Length.ShouldBe(1);
        parameters[0].ParameterType.ShouldBe(typeof(string));
    }

    [Fact]
    public void IColumnEncryptionProvider_ShouldHaveEncryptMethod()
    {
        // Arrange
        var interfaceType = typeof(IColumnEncryptionProvider);

        // Act
        var method = interfaceType.GetMethod(nameof(IColumnEncryptionProvider.Encrypt));

        // Assert
        method.ShouldNotBeNull();
        method.ReturnType.ShouldBe(typeof(string));
        var parameters = method.GetParameters();
        parameters.Length.ShouldBe(1);
        parameters[0].ParameterType.ShouldBe(typeof(string));
    }

    [Fact]
    public void IColumnEncryptionProvider_ShouldSupportMultipleImplementations()
    {
        // Arrange & Act
        IColumnEncryptionProvider provider1 = new MockColumnEncryptionProvider();
        IColumnEncryptionProvider provider2 = new ReverseEncryptionProvider();

        // Assert
        provider1.ShouldBeAssignableTo<IColumnEncryptionProvider>();
        provider2.ShouldBeAssignableTo<IColumnEncryptionProvider>();
    }

    [Fact]
    public void MockProvider_Decrypt_ShouldRestoreOriginal()
    {
        // Arrange
        var provider = new MockColumnEncryptionProvider();
        const string plaintext = "Secret";
        var encrypted = provider.Encrypt(plaintext);

        // Act
        var decrypted = provider.Decrypt(encrypted);

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void MockProvider_Decrypt_WithInvalidCiphertext_ShouldThrowException()
    {
        // Arrange
        var provider = new MockColumnEncryptionProvider();
        const string invalidCiphertext = "ENCRYPTED_NotInStorage";

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => provider.Decrypt(invalidCiphertext))
            .Message.ShouldContain("Invalid ciphertext");
    }

    [Fact]
    public void MockProvider_Encrypt_ShouldTransformPlaintext()
    {
        // Arrange
        var provider = new MockColumnEncryptionProvider();
        const string plaintext = "Secret";

        // Act
        var encrypted = provider.Encrypt(plaintext);

        // Assert
        encrypted.ShouldNotBeNull();
        encrypted.ShouldNotBe(plaintext);
        encrypted.ShouldStartWith("ENCRYPTED_");
    }

    [Fact]
    public void MockProvider_EncryptDecrypt_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var provider = new MockColumnEncryptionProvider();

        // Act
        var encrypted = provider.Encrypt(string.Empty);
        var decrypted = provider.Decrypt(string.Empty);

        // Assert
        encrypted.ShouldBe(string.Empty);
        decrypted.ShouldBe(string.Empty);
    }

    [Fact]
    public void MockProvider_EncryptDecrypt_WithNull_ShouldReturnNull()
    {
        // Arrange
        var provider = new MockColumnEncryptionProvider();

        // Act
        var encrypted = provider.Encrypt(null);
        var decrypted = provider.Decrypt(null);

        // Assert
        encrypted.ShouldBeNull();
        decrypted.ShouldBeNull();
    }

    [Theory]
    [InlineData("Hello World")]
    [InlineData("Test123")]
    [InlineData("!@#$%^&*()")]
    public void MockProvider_RoundTrip_ShouldPreserveData(string input)
    {
        // Arrange
        var provider = new MockColumnEncryptionProvider();

        // Act
        var encrypted = provider.Encrypt(input);
        var decrypted = provider.Decrypt(encrypted);

        // Assert
        decrypted.ShouldBe(input);
    }

    [Fact]
    public void ReverseProvider_Decrypt_ShouldReverseStringBack()
    {
        // Arrange
        var provider = new ReverseEncryptionProvider();
        const string plaintext = "Hello";

        // Act
        var encrypted = provider.Encrypt(plaintext);
        var decrypted = provider.Decrypt(encrypted);

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void ReverseProvider_Encrypt_ShouldReverseString()
    {
        // Arrange
        var provider = new ReverseEncryptionProvider();
        const string plaintext = "Hello";

        // Act
        var encrypted = provider.Encrypt(plaintext);

        // Assert
        encrypted.ShouldBe("olleH");
    }

    [Fact]
    public void ReverseProvider_EncryptTwice_ShouldReturnOriginal()
    {
        // Arrange
        var provider = new ReverseEncryptionProvider();
        const string plaintext = "Test";

        // Act
        var encrypted = provider.Encrypt(plaintext);
        var doubleEncrypted = provider.Encrypt(encrypted);

        // Assert
        doubleEncrypted.ShouldBe(plaintext);
    }

    [Theory]
    [InlineData("racecar")]
    [InlineData("level")]
    public void ReverseProvider_WithPalindromes_ShouldProduceSameResult(string palindrome)
    {
        // Arrange
        var provider = new ReverseEncryptionProvider();

        // Act
        var encrypted = provider.Encrypt(palindrome);

        // Assert
        encrypted.ShouldBe(palindrome);
    }

    #endregion
}