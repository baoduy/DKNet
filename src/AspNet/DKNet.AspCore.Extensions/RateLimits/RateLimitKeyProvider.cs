// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: RateLimitKeyProvider.cs
// Description: Picks the rate-limit bucket for one request: signed-in user name, else client address, else one
// shared bucket.

using Microsoft.AspNetCore.Http;

namespace DKNet.AspCore.Extensions.RateLimits;

/// <summary>
///     Picks the rate-limit bucket for the caller of one request. It reads only the signed-in identity and
///     <see cref="ConnectionInfo.RemoteIpAddress" /> as the forwarded-headers middleware left it — never
///     <c>X-Forwarded-For</c> or <c>Host</c>, which any caller can set to spend another caller's budget.
/// </summary>
internal static class RateLimitKeyProvider
{
    /// <summary>The one bucket every request with neither a user name nor a client address shares.</summary>
    internal const string SharedKey = "shared";

    /// <summary>
    ///     <c>user:&lt;name&gt;</c> for a signed-in caller with a non-empty name, else <c>ip:&lt;address&gt;</c>, else
    ///     <see cref="SharedKey" />. The prefixes keep a user named like an address out of that address's bucket.
    /// </summary>
    /// <param name="context">The request being limited.</param>
    /// <returns>The partition key for this request's caller.</returns>
    public static string GetPartitionKey(HttpContext context)
    {
        var name = context.User.Identity?.Name;
        if (!string.IsNullOrEmpty(name))
            return "user:" + name;

        var address = context.Connection.RemoteIpAddress;
        return address is null ? SharedKey : "ip:" + address.ToString();
    }
}
