// <copyright file="IdempotencyKeyScopeResolver.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace DKNet.AspCore.Idempotency.Filtering;

/// <summary>
///     Resolves the caller scope for an idempotent request using a prioritized fallback chain.
/// </summary>
/// <remarks>
///     The resolver checks, in order:
///     <list type="number">
///         <item>
///             <description>An authenticated user's <see cref="ClaimTypes.NameIdentifier" />.</description>
///         </item>
///         <item>
///             <description>
///                 An HMAC-SHA256 digest of the <c>Authorization</c> header when a server-side secret is configured.
///             </description>
///         </item>
///         <item>
///             <description>The client's remote IP address when explicitly opted in.</description>
///         </item>
///     </list>
///     If none of the above produce a value, the scope is <see cref="string.Empty" />.
///     The raw <c>Authorization</c> header and the HMAC secret are never logged or emitted.
/// </remarks>
public static class IdempotencyKeyScopeResolver
{
    /// <summary>
    ///     Resolves the caller scope for the specified HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="options">The idempotency options, including the optional HMAC secret and IP opt-in.</param>
    /// <returns>
    ///     A caller scope such as <c>user:{nameId}</c>, <c>auth:{hmac}</c>, <c>ip:{address}</c>,
    ///     or <see cref="string.Empty" /> when the caller cannot be scoped.
    /// </returns>
    public static string Resolve(HttpContext context, IdempotencyOptions options)
    {
        // 1. Authenticated user identifier.
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var nameIdentifier = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(nameIdentifier))
                return $"user:{nameIdentifier}";
        }

        // 2. HMAC of the Authorization header, but only when a secret is configured.
        var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorizationHeader) &&
            !string.IsNullOrWhiteSpace(options.ScopeHmacSecret))
        {
            var hash = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(options.ScopeHmacSecret),
                Encoding.UTF8.GetBytes(authorizationHeader));

            return $"auth:{Convert.ToHexString(hash).ToLowerInvariant()}";
        }

        // 3. Client IP address when opted in.
        if (options.IncludeClientIpInScope)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(ipAddress))
                return $"ip:{ipAddress}";
        }

        return string.Empty;
    }
}
