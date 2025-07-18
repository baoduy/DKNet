﻿using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.AzureStorage;
using Microsoft.Extensions.Configuration;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class AzureStorageSetup
{
    public static IServiceCollection AddAzureStorageAdapter(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .Configure<AzureStorageOptions>(o => configuration.GetSection(AzureStorageOptions.Name).Bind(o))
            .AddScoped<IBlobService, AzureStorageBlobService>();
    }
}