#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: S3Setup.cs
// Description: Dependency-injection setup helpers for the S3 (AWS S3 / S3-compatible) blob provider.

using DKNet.Svc.BlobStorage.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.Svc.BlobStorage.AwsS3;

/// <summary>
///     Provides helper methods to register the S3 blob storage provider into the application's DI container.
/// </summary>
public static class S3Setup
{
    #region Methods

    /// <summary>
    ///     Registers the S3 blob provider and binds <see cref="S3Options" /> from the provided configuration.
    /// </summary>
    /// <param name="services">The service collection used to register services.</param>
    /// <param name="configuration">Application configuration used to bind provider options.</param>
    /// <returns>The updated <see cref="IServiceCollection" /> for chaining.</returns>
    public static IServiceCollection AddS3BlobService(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .Configure<S3Options>(o => configuration.GetSection(S3Options.Name).Bind(o))
            .AddScoped<IBlobService, S3BlobService>();
    }

    #endregion
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member