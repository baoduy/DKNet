// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: CorsConfig.cs
// Description: Turns on CORS for one host from "Cors:AllowedOrigins", "Cors:AllowedMethods" and
// "Cors:AllowedHeaders"; never allows credentials.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DKNet.AspCore.Extensions.Hosting;

/// <summary>
///     Registers and applies a default CORS policy: only the configured allowed origins; the configured methods
///     (default GET, POST, PUT, PATCH) and headers (default Authorization, Content-Type, Accept,
///     X-Idempotency-Key), an empty list included; credentials never allowed. With no allowed origin configured,
///     CORS stays off.
/// </summary>
public static class CorsConfig
{
    // DELETE deliberately left out.
    private static readonly string[] DefaultAllowedMethods = ["GET", "POST", "PUT", "PATCH"];

    // "X-Idempotency-Key" is DKNet.AspCore.Idempotency's default idempotency header.
    private static readonly string[] DefaultAllowedHeaders =
        ["Authorization", "Content-Type", "Accept", "X-Idempotency-Key"];

    /// <summary>
    ///     Registers the default CORS policy from the <c>Cors</c> section of <paramref name="configuration" /> and
    ///     records the feature on this host; registers nothing when no non-blank allowed origin is configured.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The same <see cref="IServiceCollection" />, for chaining.</returns>
    public static IServiceCollection AddCorsConfig(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToArray();
        if (origins.Length == 0)
            return services;

        // Get<string[]>() is null only when the key is absent; a key present as [] binds to an empty array, which
        // is honoured as "nothing allowed" rather than widened back to the default list.
        var methods = configuration.GetSection("Cors:AllowedMethods").Get<string[]>() ?? DefaultAllowedMethods;
        var headers = configuration.GetSection("Cors:AllowedHeaders").Get<string[]>() ?? DefaultAllowedHeaders;

        services.AddCors(c => c.AddDefaultPolicy(p => p.WithOrigins(origins).WithMethods(methods).WithHeaders(headers)));
        return services.MarkFeatureAdded(nameof(CorsConfig));
    }

    /// <summary>
    ///     Adds the CORS middleware and logs one "enabled" line through the host's logger, when
    ///     <see cref="AddCorsConfig" /> registered the policy on this host; otherwise returns <paramref name="app" />
    ///     unchanged.
    /// </summary>
    /// <param name="app">The host's web application.</param>
    /// <returns>The same <see cref="WebApplication" />, for chaining.</returns>
    public static WebApplication UseCorsConfig(this WebApplication app)
    {
        if (!app.Services.IsFeatureAdded(nameof(CorsConfig)))
            return app;

        app.UseCors();
        app.Logger.LogInformation("{Feature} enabled", nameof(CorsConfig));
        return app;
    }
}
