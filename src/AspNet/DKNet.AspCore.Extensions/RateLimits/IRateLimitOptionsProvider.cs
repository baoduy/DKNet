// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IRateLimitOptionsProvider.cs
// Description: Seam that lets an app supply its own rate limits for a caller in place of the host's settings.

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Extensions.RateLimits;

/// <summary>
///     Supplies the limits rate limiting applies to the caller of one request. The default implementation reads
///     <see cref="RateLimitOptions" />; an app that registers its own implementation replaces it, whether it
///     registers before or after <see cref="RateLimitConfig.AddRateLimitConfig" />.
/// </summary>
public interface IRateLimitOptionsProvider
{
    /// <summary>The fixed-window limit for the caller of <paramref name="httpContext" />.</summary>
    /// <param name="httpContext">The request being limited.</param>
    /// <returns>The fixed-window limiter options for this caller.</returns>
    FixedWindowRateLimiterOptions GetRateLimiterOptions(HttpContext httpContext);

    /// <summary>The cap on requests in flight for the caller of <paramref name="httpContext" />.</summary>
    /// <param name="httpContext">The request being limited.</param>
    /// <returns>The concurrency limiter options for this caller.</returns>
    ConcurrencyLimiterOptions GetConcurrencyLimiterOptions(HttpContext httpContext);
}

/// <summary>The host's default limits: the same <see cref="RateLimitOptions" /> for every caller, no queue.</summary>
internal sealed class RateLimitOptionsProvider(IOptions<RateLimitOptions> options) : IRateLimitOptionsProvider
{
    private readonly RateLimitOptions _options = options.Value;

    public FixedWindowRateLimiterOptions GetRateLimiterOptions(HttpContext httpContext) =>
        new()
        {
            AutoReplenishment = true,
            PermitLimit = _options.DefaultRequestLimit,
            QueueLimit = 0,
            Window = TimeSpan.FromSeconds(_options.TimeWindowInSeconds)
        };

    public ConcurrencyLimiterOptions GetConcurrencyLimiterOptions(HttpContext httpContext) =>
        new() { PermitLimit = _options.DefaultConcurrentLimit, QueueLimit = 0 };
}
