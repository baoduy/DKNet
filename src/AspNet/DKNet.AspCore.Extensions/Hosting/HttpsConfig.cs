// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: HttpsConfig.cs
// Description: Turns on HTTPS redirection and HSTS for one host, with the max-age from "Https:HstsMaxAgeDays".

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Extensions.Hosting;

/// <summary>
///     Registers and applies HTTPS redirection and HSTS: max-age from <c>Https:HstsMaxAgeDays</c> (default 365
///     days), subdomains included, and preload asked for only when the max-age is 365 days or more.
/// </summary>
public static class HttpsConfig
{
    /// <summary>
    ///     Registers HSTS from <paramref name="configuration" />, or from <paramref name="configureOptions" /> when
    ///     given, and records the feature on this host.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <param name="configureOptions">Sets the HSTS options directly, in place of the configuration.</param>
    /// <returns>The same <see cref="IServiceCollection" />, for chaining.</returns>
    public static IServiceCollection AddHttpsConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<HstsOptions>? configureOptions = null) =>
        throw new NotImplementedException();

    /// <summary>
    ///     Adds HSTS and HTTPS redirection to the pipeline and logs one "enabled" line through the host's logger,
    ///     when <see cref="AddHttpsConfig" /> ran on this host; otherwise returns <paramref name="app" /> unchanged.
    /// </summary>
    /// <param name="app">The host's web application.</param>
    /// <returns>The same <see cref="WebApplication" />, for chaining.</returns>
    public static WebApplication UseHttpsConfig(this WebApplication app) =>
        throw new NotImplementedException();
}
