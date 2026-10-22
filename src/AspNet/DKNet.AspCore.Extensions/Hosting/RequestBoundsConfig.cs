// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: RequestBoundsConfig.cs
// Description: Turns on request bounds for one host — body size, request timeout, header timeout, no Server
// header — from the "RequestBounds" configuration section.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DKNet.AspCore.Extensions.Hosting;

/// <summary>
///     The request bounds a host applies, bound from the <c>RequestBounds</c> configuration section.
/// </summary>
public sealed class RequestBoundsOptions
{
    /// <summary>How long a request may run before it ends with <c>504</c>, in seconds. Defaults to 30.</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>The largest request body accepted, in bytes. Defaults to 1,048,576 (1 MiB).</summary>
    public long MaxRequestBodySizeBytes { get; set; } = 1_048_576;

    /// <summary>How long a connection may take to send its request headers, in seconds. Defaults to 10.</summary>
    public int RequestHeadersTimeoutSeconds { get; set; } = 10;
}

/// <summary>
///     Registers and applies request bounds: Kestrel's body-size and header-timeout limits, a request timeout
///     answered with <c>504</c>, and no <c>Server</c> response header.
/// </summary>
public static class RequestBoundsConfig
{
    private const string SectionName = "RequestBounds";

    /// <summary>
    ///     Applies <see cref="RequestBoundsOptions" /> from the <c>RequestBounds</c> section of
    ///     <paramref name="configuration" /> (defaults where a value is absent) and records the feature on this host.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The same <see cref="IServiceCollection" />, for chaining.</returns>
    public static IServiceCollection AddRequestBoundsConfig(this IServiceCollection services, IConfiguration configuration)
    {
        var bounds = configuration.GetSection(SectionName).Get<RequestBoundsOptions>() ?? new RequestBoundsOptions();

        services.Configure<KestrelServerOptions>(o =>
        {
            o.Limits.MaxRequestBodySize = bounds.MaxRequestBodySizeBytes;
            o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(bounds.RequestHeadersTimeoutSeconds);
            o.AddServerHeader = false;
        });

        services.AddRequestTimeouts(o => o.DefaultPolicy = new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(bounds.RequestTimeoutSeconds),
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout
        });

        return services.MarkFeatureAdded(nameof(RequestBoundsConfig));
    }

    /// <summary>
    ///     Adds the request-timeout middleware and logs one "enabled" line through the host's logger, when
    ///     <see cref="AddRequestBoundsConfig" /> ran on this host; otherwise returns <paramref name="app" /> unchanged.
    /// </summary>
    /// <param name="app">The host's web application.</param>
    /// <returns>The same <see cref="WebApplication" />, for chaining.</returns>
    public static WebApplication UseRequestBoundsConfig(this WebApplication app)
    {
        if (!app.Services.IsFeatureAdded(nameof(RequestBoundsConfig)))
            return app;

        app.UseRequestTimeouts();
        app.Logger.LogInformation("{Feature} enabled", nameof(RequestBoundsConfig));
        return app;
    }
}
