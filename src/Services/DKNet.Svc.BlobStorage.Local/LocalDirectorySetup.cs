// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: LocalDirectorySetup.cs
// Description: DI setup and small helper extensions for the local directory blob provider.

using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.Local;
using Microsoft.Extensions.Configuration;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides extension methods to register the local directory blob storage provider and a small file-system helper.
/// </summary>
public static class LocalDirectorySetup
{
    #region Methods

    /// <summary>
    ///     Registers the local directory blob storage implementation and binds <see cref="LocalDirectoryOptions" /> from
    ///     the supplied <paramref name="configuration" /> section.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration used to bind options for the local provider.</param>
    /// <returns>The updated <see cref="IServiceCollection" /> for chaining.</returns>
    public static IServiceCollection AddLocalDirectoryBlobService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .Configure<LocalDirectoryOptions>(o => configuration.GetSection(LocalDirectoryOptions.Name).Bind(o))
            .AddScoped<IBlobService, LocalBlobService>();
    }

    /// <summary>
    ///     Determines whether the specified path exists and is a directory.
    /// </summary>
    /// <param name="path">The file-system path to evaluate.</param>
    /// <returns><c>true</c> when <paramref name="path" /> exists and is a directory; otherwise <c>false</c>.</returns>
    public static bool IsDirectory(this string path)
    {
        try
        {
            var attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Directory);
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    #endregion
}