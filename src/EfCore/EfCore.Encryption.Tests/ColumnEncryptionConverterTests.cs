using System.Security.Cryptography;
using DKNet.EfCore.Encryption.Converters;
using DKNet.EfCore.Encryption.Encryption;
using DKNet.EfCore.Encryption.Interfaces;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shouldly;

namespace EfCore.Encryption.Tests;

public class ColumnEncryptionConverterTests
{
    #region Fields

    private readonly IColumnEncryptionProvider _encryptionProvider;

    #endregion

    #region Constructors

    public ColumnEncryptionConverterTests()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        this._encryptionProvider = new AesGcmColumnEncryptionProvider(key);
    }

    #endregion

    #region Methods

    [Fact]
    public void ColumnEncryptionConverter_ShouldInheritFromValueConverter()
    {
        // Arrange & Act
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);

        // Assert
        converter.ShouldBeAssignableTo<ValueConverter<string?, string?>>();
    }

    [Fact]
    public void Constructor_WithValidProvider_ShouldSucceed()
    {
        // Act
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);

        // Assert
        converter.ShouldNotBeNull();
    }

    [Fact]
    public void Converter_ShouldBeSealed()
    {
        // Arrange & Act
        var converterType = typeof(ColumnEncryptionConverter);

        // Assert
        converterType.IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void ConvertFromProvider_WithCiphertext_ShouldDecrypt()
    {
        // Arrange
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);
        const string plaintext = "Sensitive Data";
        var encrypted = this._encryptionProvider.Encrypt(plaintext);

        // Act
        var decrypted = converter.ConvertFromProvider(encrypted);

        // Assert
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void ConvertFromProvider_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);

        // Act
        var result = converter.ConvertFromProvider(string.Empty);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void ConvertFromProvider_WithNull_ShouldReturnNull()
    {
        // Arrange
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);

        // Act
        var result = converter.ConvertFromProvider(null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertToProvider_CalledMultipleTimes_ShouldProduceDifferentResults()
    {
        // Arrange
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);
        const string input = "Same input";

        // Act
        var encrypted1 = converter.ConvertToProvider(input);
        var encrypted2 = converter.ConvertToProvider(input);

        // Assert
        encrypted1.ShouldNotBeNull();
        encrypted2.ShouldNotBeNull();
        encrypted1.ShouldNotBe(encrypted2); // Due to random IV in encryption
    }

    [Fact]
    public void ConvertToProvider_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);

        // Act
        var result = converter.ConvertToProvider(string.Empty);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void ConvertToProvider_WithNull_ShouldReturnNull()
    {
        // Arrange
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);

        // Act
        var result = converter.ConvertToProvider(null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertToProvider_WithPlaintext_ShouldEncrypt()
    {
        // Arrange
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);
        const string plaintext = "Sensitive Data";

        // Act
        var encrypted = converter.ConvertToProvider(plaintext);

        // Assert
        encrypted.ShouldNotBeNull();
        encrypted.ShouldNotBe(plaintext);
    }

    [Fact]
    public void ConvertToProviderThenFromProvider_ShouldReturnOriginal()
    {
        // Arrange
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);
        const string original = "Test Message";

        // Act
        var encrypted = converter.ConvertToProvider(original);
        var decrypted = converter.ConvertFromProvider(encrypted);

        // Assert
        decrypted.ShouldBe(original);
    }

    [Theory]
    [InlineData("Hello World")]
    [InlineData("Special chars: !@#$%^&*()")]
    [InlineData("Unicode: 你好世界 🌍")]
    [InlineData("123456789")]
    public void RoundTrip_WithVariousInputs_ShouldPreserveData(string input)
    {
        // Arrange
        var converter = new ColumnEncryptionConverter(this._encryptionProvider);

        // Act
        var encrypted = converter.ConvertToProvider(input);
        var decrypted = converter.ConvertFromProvider(encrypted);

        // Assert
        decrypted.ShouldBe(input);
    }

    #endregion
}