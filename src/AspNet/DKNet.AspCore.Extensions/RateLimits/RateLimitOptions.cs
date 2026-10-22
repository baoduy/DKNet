// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: RateLimitOptions.cs
// Description: The host's rate-limit settings, bound from the "RateLimit" configuration section.

namespace DKNet.AspCore.Extensions.RateLimits;

/// <summary>
///     The per-caller limits rate limiting applies when the app supplies none of its own, bound from the
///     <c>RateLimit</c> configuration section.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>The number of requests one caller may send in one time window. Defaults to 2.</summary>
    public int DefaultRequestLimit { get; set; } = 2;

    /// <summary>The number of requests one caller may have in flight at once. Defaults to 2.</summary>
    public int DefaultConcurrentLimit { get; set; } = 2;

    /// <summary>The length of one fixed time window, in seconds. Defaults to 1.</summary>
    public int TimeWindowInSeconds { get; set; } = 1;
}
