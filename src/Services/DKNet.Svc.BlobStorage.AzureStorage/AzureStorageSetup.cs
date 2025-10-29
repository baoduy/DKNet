﻿// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: AzureStorageSetup.cs
// Description: Dependency-injection setup helpers for Azure Blob Storage provider.

using DKNet.Svc.BlobStorage.AzureStorage;
using Microsoft.Extensions.Configuration;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods to register the Azure Blob Storage provider and bind its configuration.
/// </summary>
public static class AzureStorageSetup
{
    #region Methods

    /// <summary>
    ///     Registers the Azure storage adapter and binds <see cref="AzureStorageOptions" /> from configuration.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <param name="configuration">Application configuration used to bind provider options.</param>
    /// <returns>The updated <see cref="IServiceCollection" /> for chaining.</returns>
    public static IServiceCollection AddAzureStorageAdapter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .Configure<AzureStorageOptions>(o => configuration.GetSection(AzureStorageOptions.Name).Bind(o))
            .AddScoped<IBlobService, AzureStorageBlobService>();
    }

    #endregion
}