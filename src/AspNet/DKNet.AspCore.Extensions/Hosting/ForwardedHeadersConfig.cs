// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ForwardedHeadersConfig.cs
// Description: Turns on forwarded-header handling for one host, trusted only from "Security:TrustedProxies"
// and "Security:TrustedNetworks".

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// <exception cref="FormatException">A trusted proxy or network entry is not a valid address or CIDR range.</exception>
    public static IServiceCollection AddForwardedHeadersConfig(this IServiceCollection services, IConfiguration configuration)
    {
        // Parsed here, not inside the options callback, so a malformed entry fails at startup registration.
        var proxies = (configuration.GetSection("Security:TrustedProxies").Get<string[]>() ?? [])
            .Select(IPAddress.Parse)
            .ToArray();
        var networks = (configuration.GetSection("Security:TrustedNetworks").Get<string[]>() ?? [])
            .Select(System.Net.IPNetwork.Parse)
            .ToArray();

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            // ASP.NET Core seeds both lists with loopback; clear them so only the configured entries are trusted.
            o.KnownProxies.Clear();
            o.KnownIPNetworks.Clear();

            // With both lists empty the middleware would trust the headers from ANY peer, so turn it off instead.
            if (proxies.Length == 0 && networks.Length == 0)
            {
                o.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in proxies)
                o.KnownProxies.Add(proxy);
            foreach (var network in networks)
                o.KnownIPNetworks.Add(network);
        });

        return services.MarkFeatureAdded(nameof(ForwardedHeadersConfig));
    }

    /// <summary>
    ///     Adds the forwarded-headers middleware and logs one "enabled" line through the host's logger, when
    ///     <see cref="AddForwardedHeadersConfig" /> ran on this host; otherwise returns <paramref name="app" /> unchanged.
    /// </summary>
    /// <param name="app">The host's web application.</param>
    /// <returns>The same <see cref="WebApplication" />, for chaining.</returns>
    public static WebApplication UseForwardedHeadersConfig(this WebApplication app)
    {
        if (!app.Services.IsFeatureAdded(nameof(ForwardedHeadersConfig)))
            return app;

        app.UseForwardedHeaders();
        app.Logger.LogInformation("{Feature} enabled", nameof(ForwardedHeadersConfig));
        return app;
    }
}
