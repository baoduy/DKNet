// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: AntiforgeryGuardOptions.cs
// Description: The cookie and header names the double-submit antiforgery guard uses; a host may set its own.

namespace DKNet.AspCore.Extensions.Antiforgery;

/// <summary>
///     The names the antiforgery guard registered by <see cref="AntiforgeryConfig.AddAntiforgeryConfig" /> uses
///     for its readable token cookie, the request header carrying the token back, and the HttpOnly secret cookie.
/// </summary>
public sealed class AntiforgeryGuardOptions
{
    /// <summary>
    ///     The name of the cookie, readable by page script, that carries the request token on every GET response.
    ///     Defaults to <c>XSRF-TOKEN</c>.
    /// </summary>
    public string TokenCookieName { get; set; } = "XSRF-TOKEN";

    /// <summary>
    ///     The request header a state-changing request must carry the token in. Defaults to <c>X-XSRF-TOKEN</c>.
    /// </summary>
    public string HeaderName { get; set; } = "X-XSRF-TOKEN";

    /// <summary>
    ///     The name of the HttpOnly secret cookie; <see langword="null" /> keeps the framework's default name.
    /// </summary>
    public string? SecretCookieName { get; set; }
}
