using Microsoft.Extensions.Configuration;
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AzureStorage;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class AzureStorageSetup
{
    public static IServiceCollection AddAzureStorageAdapter(this IServiceCollection services, IConfiguration configuration)
        => services
            .Configure<AzureStorageOptions>(o => configuration.GetSection(AzureStorageOptions.Name).Bind(o))
            .AddScoped<IBlobService, AzureStorageBlobService>();
}