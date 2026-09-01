using DKNet.Svc.Encryption;
using DKNet.Svc.Encryption.Ciphers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Svc.Encryption.Tests;

#pragma warning disable CS0618
public class EncryptionSetupAesTests
{
    #region Methods

    [Fact]
    public void AddAesGcmEncryption_Registers_Singleton()
    {
        var services = new ServiceCollection();
        services.AddAesGcmEncryption(new AesGcmEncryption().Key);
        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IAesGcmEncryption>();
        var second = provider.GetRequiredService<IAesGcmEncryption>();

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    [Fact]
    public void AddAesGcmEncryption_TwoResolutions_RoundTrip()
    {
        var key = new AesGcmEncryption().Key;
        var services = new ServiceCollection();
        services.AddAesGcmEncryption(key);
        var provider = services.BuildServiceProvider();

        var encryptor = provider.GetRequiredService<IAesGcmEncryption>();
        var cipher = encryptor.EncryptString("hello world");

        var decryptor = provider.GetRequiredService<IAesGcmEncryption>();
        decryptor.DecryptString(cipher).ShouldBe("hello world");
    }

    [Fact]
    public void AddAesGcmEncryption_AcrossScopes_RoundTrip_And_NotDisposedByFirstScope()
    {
        var key = new AesGcmEncryption().Key;
        var services = new ServiceCollection();
        services.AddAesGcmEncryption(key);
        var provider = services.BuildServiceProvider();

        string cipher;
        using (var scope1 = provider.CreateScope())
        {
            cipher = scope1.ServiceProvider.GetRequiredService<IAesGcmEncryption>()
                .EncryptString("scoped payload");
        }

        using var scope2 = provider.CreateScope();
        scope2.ServiceProvider.GetRequiredService<IAesGcmEncryption>()
            .DecryptString(cipher).ShouldBe("scoped payload");
    }

    [Fact]
    public void AddAesGcmEncryption_CalledTwice_KeepsFirstRegisteredKey()
    {
        var first = new AesGcmEncryption().Key;
        var second = new AesGcmEncryption().Key;

        var services = new ServiceCollection();
        services.AddAesGcmEncryption(first);
        services.AddAesGcmEncryption(second);

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAesGcmEncryption>().Key.ShouldBe(first);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddAesGcmEncryption_NullOrWhitespaceKey_Throws(string key)
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentException>(() => services.AddAesGcmEncryption(key));
    }

    [Fact]
    public void AddAesEncryption_Registers_Singleton()
    {
        var services = new ServiceCollection();
        services.AddAesEncryption(new AesEncryption().Key);
        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IAesEncryption>();
        var second = provider.GetRequiredService<IAesEncryption>();

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    [Fact]
    public void AddAesEncryption_TwoResolutions_RoundTrip()
    {
        var key = new AesEncryption().Key;
        var services = new ServiceCollection();
        services.AddAesEncryption(key);
        var provider = services.BuildServiceProvider();

        var encryptor = provider.GetRequiredService<IAesEncryption>();
        var cipher = encryptor.EncryptString("hello world");

        var decryptor = provider.GetRequiredService<IAesEncryption>();
        decryptor.DecryptString(cipher).ShouldBe("hello world");
    }

    [Fact]
    public void AddAesEncryption_CalledTwice_KeepsFirstRegisteredKey()
    {
        var first = new AesEncryption().Key;
        var second = new AesEncryption().Key;

        var services = new ServiceCollection();
        services.AddAesEncryption(first);
        services.AddAesEncryption(second);

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAesEncryption>().Key.ShouldBe(first);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddAesEncryption_NullOrWhitespaceKey_Throws(string key)
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentException>(() => services.AddAesEncryption(key));
    }

    #endregion
}
#pragma warning restore CS0618
