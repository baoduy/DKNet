// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: RateLimitConfig.cs
// Description: Turns on per-caller rate limiting for one host: an Add… registration plus a Use… step.

using System.Threading.RateLimiting;
using DKNet.AspCore.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DKNet.AspCore.Extensions.RateLimits;

/// <summary>
///     Registers and applies per-caller rate limiting: a fixed window of requests and a cap on requests in flight,
///     each counted per caller (signed-in user name, else client address, else one shared bucket) and refused
///     with <c>429</c> when exceeded.
/// </summary>
public static class RateLimitConfig
{
    private const string SectionName = "RateLimit";

    /// <summary>
    ///     Registers the rate limiter, binds <see cref="RateLimitOptions" /> from the <c>RateLimit</c> section of
    ///     <paramref name="configuration" />, and records the feature on this host.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The same <see cref="IServiceCollection" />, for chaining.</returns>
    public static IServiceCollection AddRateLimitConfig(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(configuration.GetSection(SectionName));

        // TryAdd: an app's own provider wins whether it is registered before or after this call.
        services.TryAddSingleton<IRateLimitOptionsProvider, RateLimitOptionsProvider>();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitKeyProvider.GetPartitionKey(ctx),
                    _ => Provider(ctx).GetRateLimiterOptions(ctx))),
                PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetConcurrencyLimiter(
                    RateLimitKeyProvider.GetPartitionKey(ctx),
                    _ => Provider(ctx).GetConcurrencyLimiterOptions(ctx))));
        });

        return services.MarkFeatureAdded(nameof(RateLimitConfig));
    }

    /// <summary>
    ///     Adds the rate limiter to the pipeline and logs one "enabled" line through the host's logger, when
    ///     <see cref="AddRateLimitConfig" /> ran on this host; otherwise returns <paramref name="app" /> unchanged.
    ///     Call it after authentication, so a signed-in caller is counted by user name, and behind a proxy after
    ///     <see cref="Hosting.ForwardedHeadersConfig.UseForwardedHeadersConfig" />, so an anonymous caller is counted
    ///     by its forwarded client address.
    /// </summary>
    /// <param name="app">The host's web application.</param>
    /// <returns>The same <see cref="WebApplication" />, for chaining.</returns>
    public static WebApplication UseRateLimitConfig(this WebApplication app)
    {
        if (!app.Services.IsFeatureAdded(nameof(RateLimitConfig)))
            return app;

        app.UseRateLimiter();
        app.Logger.LogInformation("{Feature} enabled", nameof(RateLimitConfig));
        return app;
    }

    private static IRateLimitOptionsProvider Provider(HttpContext context) =>
        context.RequestServices.GetRequiredService<IRateLimitOptionsProvider>();
}
