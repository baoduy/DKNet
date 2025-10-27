using DKNet.Svc.BlobStorage.AzureStorage;
using Microsoft.Extensions.Configuration;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class AzureStorageSetup
{
    #region Methods

    public static IServiceCollection AddAzureStorageAdapter(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .Configure<AzureStorageOptions>(o => configuration.GetSection(AzureStorageOptions.Name).Bind(o))
            .AddScoped<IBlobService, AzureStorageBlobService>();
    }

    #endregion
}