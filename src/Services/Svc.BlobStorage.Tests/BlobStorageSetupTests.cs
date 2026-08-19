using DKNet.Svc.BlobStorage.AwsS3;
using DKNet.Svc.BlobStorage.AzureStorage;
using DKNet.Svc.BlobStorage.Local;

namespace Svc.BlobStorage.Tests;

/// <summary>
///     Tests for the S3/Azure/Local blob-provider DI setup extensions' duplicate-registration guards, and
///     for the multi-implementation contract (<see cref="IBlobService" />) coexisting across providers (DRK-466).
/// </summary>
public class BlobStorageSetupTests
{
    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

    #region S3

    [Fact]
    public void AddS3BlobService_CalledTwice_RegistersImplementationOnlyOnce()
    {
        var services = new ServiceCollection();

        services.AddS3BlobService(EmptyConfiguration());
        services.AddS3BlobService(EmptyConfiguration());

        services.Count(s => s.ServiceType == typeof(IBlobService) && s.ImplementationType == typeof(S3BlobService))
            .ShouldBe(1);
    }

    [Fact]
    public void AddS3BlobService_ReturnsSameServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddS3BlobService(EmptyConfiguration());

        result.ShouldBeSameAs(services);
    }

    #endregion

    #region Azure

    [Fact]
    public void AddAzureStorageAdapter_WithConfigAction_CalledTwice_RegistersImplementationOnlyOnce()
    {
        var services = new ServiceCollection();

        services.AddAzureStorageAdapter(o => o.ConnectionString = "UseDevelopmentStorage=true");
        services.AddAzureStorageAdapter(o => o.ConnectionString = "UseDevelopmentStorage=true");

        services.Count(s =>
                s.ServiceType == typeof(IBlobService) && s.ImplementationType == typeof(AzureStorageBlobService))
            .ShouldBe(1);
    }

    [Fact]
    public void AddAzureStorageAdapter_WithConfiguration_CalledTwice_RegistersImplementationOnlyOnce()
    {
        var services = new ServiceCollection();

        services.AddAzureStorageAdapter(EmptyConfiguration());
        services.AddAzureStorageAdapter(EmptyConfiguration());

        services.Count(s =>
                s.ServiceType == typeof(IBlobService) && s.ImplementationType == typeof(AzureStorageBlobService))
            .ShouldBe(1);
    }

    #endregion

    #region Local

    [Fact]
    public void AddLocalDirectoryBlobService_CalledTwice_RegistersImplementationOnlyOnce()
    {
        var services = new ServiceCollection();

        services.AddLocalDirectoryBlobService(EmptyConfiguration());
        services.AddLocalDirectoryBlobService(EmptyConfiguration());

        services.Count(s => s.ServiceType == typeof(IBlobService) && s.ImplementationType == typeof(LocalBlobService))
            .ShouldBe(1);
    }

    [Fact]
    public void AddLocalDirectoryBlobService_ReturnsSameServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddLocalDirectoryBlobService(EmptyConfiguration());

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void IsDirectory_ExistingDirectory_ReturnsTrue() => Path.GetTempPath().IsDirectory().ShouldBeTrue();

    [Fact]
    public void IsDirectory_PathWithMissingIntermediateDirectory_ReturnsFalse()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nested", "deeper");

        path.IsDirectory().ShouldBeFalse();
    }

    [Fact]
    public void IsDirectory_PathWithInvalidCharacters_ReturnsFalse() => "invalid\0path".IsDirectory().ShouldBeFalse();

    #endregion

    #region Multi-provider coexistence

    [Fact]
    public void AllThreeProviders_RegisteredTogether_CoexistAsDistinctImplementationsOfIBlobService()
    {
        // IBlobService is a multi-implementation contract (DRK-466 §5): distinct providers must coexist
        // side by side rather than the second/third guard evicting the first.
        var services = new ServiceCollection();

        services
            .AddS3BlobService(EmptyConfiguration())
            .AddAzureStorageAdapter(EmptyConfiguration())
            .AddLocalDirectoryBlobService(EmptyConfiguration());

        services.Count(s => s.ServiceType == typeof(IBlobService)).ShouldBe(3);
        services.ShouldContain(s => s.ImplementationType == typeof(S3BlobService));
        services.ShouldContain(s => s.ImplementationType == typeof(AzureStorageBlobService));
        services.ShouldContain(s => s.ImplementationType == typeof(LocalBlobService));
    }

    #endregion
}
