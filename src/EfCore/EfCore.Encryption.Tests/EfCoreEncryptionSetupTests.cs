using System.Security.Cryptography;
using DKNet.EfCore.Encryption;
using DKNet.EfCore.Encryption.Encryption;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EfCore.Encryption.Tests;

// Test implementation for dependency injection testing
internal class SimpleKeyProvider : IEncryptionKeyProvider
{
    private readonly byte[] _key;

    public SimpleKeyProvider()
    {
        _key = new byte[32];
        RandomNumberGenerator.Fill(_key);
    }

    public byte[] GetKey(Type entityType)
    {
        return _key;
    }
}

// Another test implementation
internal class ConfigurableKeyProvider : IEncryptionKeyProvider
{
    public byte[] Key { get; set; } = new byte[32];

    public byte[] GetKey(Type entityType)
    {
        return Key;
    }
}

public class EfCoreEncryptionSetupTests
{
    [Fact]
    public void AddEfCoreEncryption_ShouldRegisterKeyProviderAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEfCoreEncryption<SimpleKeyProvider>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var keyProvider = serviceProvider.GetService<IEncryptionKeyProvider>();
        keyProvider.ShouldNotBeNull();
        keyProvider.ShouldBeOfType<SimpleKeyProvider>();
    }

    [Fact]
    public void AddEfCoreEncryption_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddEfCoreEncryption<SimpleKeyProvider>();

        // Assert
        result.ShouldBe(services);
    }

    [Fact]
    public void AddEfCoreEncryption_MultipleCalls_ShouldUseLast()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEfCoreEncryption<SimpleKeyProvider>();
        services.AddEfCoreEncryption<ConfigurableKeyProvider>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var keyProviders = serviceProvider.GetServices<IEncryptionKeyProvider>().ToList();
        keyProviders.Count.ShouldBe(2); // Both registrations exist
        
        var lastProvider = serviceProvider.GetRequiredService<IEncryptionKeyProvider>();
        lastProvider.ShouldBeOfType<ConfigurableKeyProvider>();
    }

    [Fact]
    public void AddEfCoreEncryption_ShouldReturnSameSingletonInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEfCoreEncryption<SimpleKeyProvider>();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var instance1 = serviceProvider.GetService<IEncryptionKeyProvider>();
        var instance2 = serviceProvider.GetService<IEncryptionKeyProvider>();

        // Assert
        instance1.ShouldNotBeNull();
        instance2.ShouldNotBeNull();
        instance1.ShouldBeSameAs(instance2);
    }

    [Fact]
    public void AddEfCoreEncryption_WithAbstractClass_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEfCoreEncryption<SetupTestKeyProvider>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var keyProvider = serviceProvider.GetService<IEncryptionKeyProvider>();
        keyProvider.ShouldNotBeNull();
        keyProvider.ShouldBeOfType<SetupTestKeyProvider>();
    }

    [Fact]
    public void AddEfCoreEncryption_KeyProviderShouldBeUsable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEfCoreEncryption<SimpleKeyProvider>();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var keyProvider = serviceProvider.GetRequiredService<IEncryptionKeyProvider>();
        var key = keyProvider.GetKey(typeof(string));

        // Assert
        key.ShouldNotBeNull();
        key.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void EfCoreEncryptionSetup_ShouldBeStaticClass()
    {
        // Arrange & Act
        var setupType = typeof(EfCoreEncryptionSetup);

        // Assert
        setupType.IsAbstract.ShouldBeTrue();
        setupType.IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void AddEfCoreEncryption_ShouldSupportMultipleServiceDescriptors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<string>("Test");
        services.AddTransient<object>(_ => 42);

        // Act
        services.AddEfCoreEncryption<SimpleKeyProvider>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<string>().ShouldBe("Test");
        serviceProvider.GetService<object>().ShouldBe(42);
        serviceProvider.GetService<IEncryptionKeyProvider>().ShouldNotBeNull();
    }

    [Fact]
    public void AddEfCoreEncryption_ShouldRegisterAsInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEfCoreEncryption<SimpleKeyProvider>();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var keyProviderByInterface = serviceProvider.GetService<IEncryptionKeyProvider>();
        var keyProviderByImplementation = serviceProvider.GetService<SimpleKeyProvider>();

        // Assert
        keyProviderByInterface.ShouldNotBeNull();
        keyProviderByImplementation.ShouldBeNull(); // Not registered as implementation type
    }

    [Fact]
    public void AddEfCoreEncryption_AfterBuildingServiceProvider_ShouldReflectNewRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEfCoreEncryption<SimpleKeyProvider>();
        var serviceProvider1 = services.BuildServiceProvider();
        var provider1 = serviceProvider1.GetService<IEncryptionKeyProvider>();

        services.AddEfCoreEncryption<ConfigurableKeyProvider>();
        var serviceProvider2 = services.BuildServiceProvider();
        var provider2 = serviceProvider2.GetRequiredService<IEncryptionKeyProvider>();

        // Assert
        provider1.ShouldBeOfType<SimpleKeyProvider>();
        provider2.ShouldBeOfType<ConfigurableKeyProvider>();
    }

    [Fact]
    public void AddEfCoreEncryption_GenericConstraints_ShouldBeCorrect()
    {
        // Arrange
        var method = typeof(EfCoreEncryptionSetup).GetMethod(nameof(EfCoreEncryptionSetup.AddEfCoreEncryption));

        // Assert
        method.ShouldNotBeNull();
        method.IsGenericMethod.ShouldBeTrue();
        
        var genericArguments = method.GetGenericArguments();
        genericArguments.Length.ShouldBe(1);
        
        var constraints = genericArguments[0].GetGenericParameterConstraints();
        constraints.ShouldContain(c => c == typeof(IEncryptionKeyProvider));
    }
}

// Helper class for testing abstract implementation
internal class SetupTestKeyProvider : EncryptionKeyProvider
{
    private readonly byte[] _key = new byte[32];

    public SetupTestKeyProvider()
    {
        RandomNumberGenerator.Fill(_key);
    }

    public override byte[] GetKey(Type entityType)
    {
        return _key;
    }
}
