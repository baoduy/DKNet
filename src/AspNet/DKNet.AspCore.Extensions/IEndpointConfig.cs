// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IEndpointConfig.cs
// Description: Contract for a discoverable endpoint group, mapped by EndpointConfigExtensions.UseEndpointConfigs.

using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace DKNet.AspCore.Extensions;

/// <summary>
///     Declares one versioned group of endpoints. Implementations are discovered and mapped automatically by
///     <see cref="EndpointConfigExtensions.UseEndpointConfigs" /> — including implementations defined in the
///     consuming application, not just this package.
/// </summary>
public interface IEndpointConfig
{
    /// <summary>
    ///     The authorization policy required for this group, or <see langword="null" /> to require plain
    ///     authentication (no specific policy) when authorization is enabled.
    /// </summary>
    string? AuthPolicy => null;

    /// <summary>
    ///     The route segment appended after the version prefix, for example <c>"/products"</c>.
    /// </summary>
    string GroupEndpoint { get; }

    /// <summary>
    ///     The OpenAPI grouping tag for this group. Defaults to <see cref="GroupEndpoint" /> with slashes replaced by
    ///     dashes.
    /// </summary>
    string Tag => GroupEndpoint.Replace("/", "-", StringComparison.OrdinalIgnoreCase).TrimStart('-');

    /// <summary>
    ///     The API version this group is mapped to. Defaults to <c>1</c> when not overridden.
    /// </summary>
    int Version => 1;

    /// <summary>
    ///     Maps this group's endpoints onto <paramref name="group" />.
    /// </summary>
    /// <param name="group">The route group created for this config by <see cref="EndpointConfigExtensions.UseEndpointConfigs" />.</param>
    void Map(RouteGroupBuilder group);
}
