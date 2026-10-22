// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: HostFeatureMarker.cs
// Description: Per-host record of which Add…Config setups ran, so a Use…Config call can tell whether its Add…Config
// ran on THIS host — never a static flag shared by every host in the process.

using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Extensions.Hosting;

/// <summary>
///     Records that a named feature was set up on one host, using a keyed singleton held by that host's own
///     <see cref="IServiceCollection" /> / <see cref="IServiceProvider" />. Two hosts built in the same process
///     (for example two test hosts) never see each other's records.
/// </summary>
public static class HostFeatureMarker
{
    /// <summary>Records that the feature <paramref name="name" /> was set up on this host's services.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="name">The feature name, conventionally <c>nameof</c> the config class that set it up.</param>
    /// <returns>The same <see cref="IServiceCollection" />, for chaining.</returns>
    public static IServiceCollection MarkFeatureAdded(this IServiceCollection services, string name)
    {
        services.AddKeyedSingleton(name, new object());
        return services;
    }

    /// <summary>Checks whether the feature <paramref name="name" /> was recorded on this host's services.</summary>
    /// <param name="services">The host's built service provider.</param>
    /// <param name="name">The feature name passed to <see cref="MarkFeatureAdded" />.</param>
    /// <returns><see langword="true" /> when <see cref="MarkFeatureAdded" /> was called with this name on this host.</returns>
    public static bool IsFeatureAdded(this IServiceProvider services, string name) =>
        services.GetKeyedService<object>(name) is not null;
}
