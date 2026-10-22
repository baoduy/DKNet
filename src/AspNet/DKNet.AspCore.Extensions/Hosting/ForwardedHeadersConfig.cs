// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ForwardedHeadersConfig.cs
// Description: Turns on forwarded-header handling for one host, trusted only from "Security:TrustedProxies"
// and "Security:TrustedNetworks".

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Extensions.Hosting;

/// <summary>
///     Registers and applies forwarded-header handling: the client address from <c>X-Forwarded-For</c> and the
///     scheme from <c>X-Forwarded-Proto</c> are taken only from a trusted proxy address or a trusted network; with
///     none configured, forwarded headers are ignored entirely.
/// </summary>
public static class ForwardedHeadersConfig
{
    /// <summary>
    ///     Registers the forwarded-header trust from the <c>Security:TrustedProxies</c> and
    ///     <c>Security:TrustedNetworks</c> string arrays of <paramref name="configuration" />, and records the
    ///     feature on this host.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The same <see cref="IServiceCollection" />, for chaining.</returns>
    public static IServiceCollection AddForwardedHeadersConfig(this IServiceCollection services, IConfiguration configuration) =>
        throw new NotImplementedException();

    /// <summary>
    ///     Adds the forwarded-headers middleware and logs one "enabled" line through the host's logger, when
    ///     <see cref="AddForwardedHeadersConfig" /> ran on this host; otherwise returns <paramref name="app" /> unchanged.
    /// </summary>
    /// <param name="app">The host's web application.</param>
    /// <returns>The same <see cref="WebApplication" />, for chaining.</returns>
    public static WebApplication UseForwardedHeadersConfig(this WebApplication app) =>
        throw new NotImplementedException();
}
