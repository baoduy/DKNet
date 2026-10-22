// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: RateLimitConfig.cs
// Description: Turns on per-caller rate limiting for one host: an Add… registration plus a Use… step.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Extensions.RateLimits;

/// <summary>
///     Registers and applies per-caller rate limiting: a fixed window of requests and a cap on requests in flight,
///     each counted per caller (signed-in user name, else client address, else one shared bucket) and refused
///     with <c>429</c> when exceeded.
/// </summary>
public static class RateLimitConfig
{
    /// <summary>
    ///     Registers the rate limiter, binds <see cref="RateLimitOptions" /> from the <c>RateLimit</c> section of
    ///     <paramref name="configuration" />, and records the feature on this host.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The same <see cref="IServiceCollection" />, for chaining.</returns>
    public static IServiceCollection AddRateLimitConfig(this IServiceCollection services, IConfiguration configuration) =>
        throw new NotImplementedException();

    /// <summary>
    ///     Adds the rate limiter to the pipeline and logs one "enabled" line through the host's logger, when
    ///     <see cref="AddRateLimitConfig" /> ran on this host; otherwise returns <paramref name="app" /> unchanged.
    ///     Call it after authentication, so a signed-in caller is counted by user name.
    /// </summary>
    /// <param name="app">The host's web application.</param>
    /// <returns>The same <see cref="WebApplication" />, for chaining.</returns>
    public static WebApplication UseRateLimitConfig(this WebApplication app) =>
        throw new NotImplementedException();
}
