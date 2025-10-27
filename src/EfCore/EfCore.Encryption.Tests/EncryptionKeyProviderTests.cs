using System.Security.Cryptography;
using DKNet.EfCore.Encryption.Encryption;
using Shouldly;

namespace EfCore.Encryption.Tests;

// Test implementation for abstract class
internal class TestEncryptionKeyProvider : EncryptionKeyProvider
{
    private readonly byte[] _key;

    public TestEncryptionKeyProvider(byte[] key)
    {
        _key = key;
    }

    public override byte[] GetKey(Type entityType)
    {
        return _key;
    }
}

// Another test implementation that returns different keys per type
internal class TypeSpecificKeyProvider : EncryptionKeyProvider
{
    private readonly Dictionary<Type, byte[]> _keys = new();

    public void AddKey(Type entityType, byte[] key)
    {
        _keys[entityType] = key;
    }

    public override byte[] GetKey(Type entityType)
    {
        return _keys.TryGetValue(entityType, out var key) ? key : throw new InvalidOperationException($"No key configured for {entityType.Name}");
    }
}

public class EncryptionKeyProviderTests
{
    [Fact]
    public void IEncryptionKeyProvider_ShouldHaveGetKeyMethod()
    {
        // Arrange
        var interfaceType = typeof(IEncryptionKeyProvider);

        // Act
        var method = interfaceType.GetMethod(nameof(IEncryptionKeyProvider.GetKey));

        // Assert
        method.ShouldNotBeNull();
        method.ReturnType.ShouldBe(typeof(byte[]));
        var parameters = method.GetParameters();
        parameters.Length.ShouldBe(1);
        parameters[0].ParameterType.ShouldBe(typeof(Type));
    }

    [Fact]
    public void EncryptionKeyProvider_ShouldBeAbstract()
    {
        // Arrange & Act
        var providerType = typeof(EncryptionKeyProvider);

        // Assert
        providerType.IsAbstract.ShouldBeTrue();
    }

    [Fact]
    public void EncryptionKeyProvider_ShouldImplementIEncryptionKeyProvider()
    {
        // Arrange & Act
        var providerType = typeof(EncryptionKeyProvider);

        // Assert
        typeof(IEncryptionKeyProvider).IsAssignableFrom(providerType).ShouldBeTrue();
    }

    [Fact]
    public void TestEncryptionKeyProvider_GetKey_ShouldReturnConfiguredKey()
    {
        // Arrange
        var expectedKey = new byte[32];
        RandomNumberGenerator.Fill(expectedKey);
        var provider = new TestEncryptionKeyProvider(expectedKey);

        // Act
        var actualKey = provider.GetKey(typeof(string));

        // Assert
        actualKey.ShouldBe(expectedKey);
    }

    [Fact]
    public void TestEncryptionKeyProvider_GetKey_ShouldReturnSameKeyForDifferentTypes()
    {
        // Arrange
        var expectedKey = new byte[32];
        RandomNumberGenerator.Fill(expectedKey);
        var provider = new TestEncryptionKeyProvider(expectedKey);

        // Act
        var key1 = provider.GetKey(typeof(string));
        var key2 = provider.GetKey(typeof(int));

        // Assert
        key1.ShouldBe(expectedKey);
        key2.ShouldBe(expectedKey);
    }

    [Fact]
    public void TypeSpecificKeyProvider_GetKey_ShouldReturnDifferentKeysForDifferentTypes()
    {
        // Arrange
        var key1 = new byte[32];
        var key2 = new byte[32];
        RandomNumberGenerator.Fill(key1);
        RandomNumberGenerator.Fill(key2);
        
        var provider = new TypeSpecificKeyProvider();
        provider.AddKey(typeof(string), key1);
        provider.AddKey(typeof(int), key2);

        // Act
        var actualKey1 = provider.GetKey(typeof(string));
        var actualKey2 = provider.GetKey(typeof(int));

        // Assert
        actualKey1.ShouldBe(key1);
        actualKey2.ShouldBe(key2);
        actualKey1.ShouldNotBe(actualKey2);
    }

    [Fact]
    public void TypeSpecificKeyProvider_GetKey_WithUnknownType_ShouldThrowException()
    {
        // Arrange
        var provider = new TypeSpecificKeyProvider();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => provider.GetKey(typeof(string)));
        exception.Message.ShouldContain("No key configured");
        exception.Message.ShouldContain("String");
    }

    [Fact]
    public void EncryptionKeyProvider_ShouldSupportMultipleImplementations()
    {
        // Arrange & Act
        IEncryptionKeyProvider provider1 = new TestEncryptionKeyProvider(new byte[32]);
        IEncryptionKeyProvider provider2 = new TypeSpecificKeyProvider();

        // Assert
        provider1.ShouldBeAssignableTo<IEncryptionKeyProvider>();
        provider2.ShouldBeAssignableTo<IEncryptionKeyProvider>();
    }
}
