// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: AntiforgeryConfig.cs
// Description: Turns on the double-submit antiforgery guard for one host: an Add… registration plus a Use… step.

using DKNet.AspCore.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DKNet.AspCore.Extensions.Antiforgery;

/// <summary>
///     Registers and applies DKNet's double-submit antiforgery guard: one stable HttpOnly secret cookie, a readable
///     token cookie on every GET response, and a check on every POST, PUT, PATCH and DELETE request that carries no
///     bearer token, refused with DKNet's standard 400 problem-details body.
/// </summary>
public static class AntiforgeryConfig
{
    /// <summary>
    ///     Registers the antiforgery services the guard needs and records the feature on this host.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configure">Sets the guard's cookie and header names; leave <see langword="null" /> to keep the defaults.</param>
    /// <returns>The same <see cref="IServiceCollection" />, for chaining.</returns>
    public static IServiceCollection AddAntiforgeryConfig(
        this IServiceCollection services,
        Action<AntiforgeryGuardOptions>? configure = null)
    {
        var options = new AntiforgeryGuardOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddAntiforgery(o =>
        {
            o.HeaderName = options.HeaderName;
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            if (options.SecretCookieName is not null) o.Cookie.Name = options.SecretCookieName;
        });

        return services.MarkFeatureAdded(nameof(AntiforgeryConfig));
    }

    /// <summary>
    ///     Adds the antiforgery guard to the pipeline and logs one "enabled" line through the host's logger, when
    ///     <see cref="AddAntiforgeryConfig" /> ran on this host; otherwise returns <paramref name="app" /> unchanged.
    ///     Call it after <c>UseRouting</c>, so the guard can see whether the endpoint turns the check off. It also
    ///     adds the framework's own antiforgery step (<c>UseAntiforgery</c>) ahead of the guard, so a host must not
    ///     call <c>UseAntiforgery</c> itself: a second copy after the guard would refuse the bearer requests the
    ///     guard exempts on form-bound endpoints.
    /// </summary>
    /// <param name="app">The host's web application.</param>
    /// <returns>The same <see cref="WebApplication" />, for chaining.</returns>
    public static WebApplication UseAntiforgeryConfig(this WebApplication app)
    {
        if (!app.Services.IsFeatureAdded(nameof(AntiforgeryConfig))) return app;

        // R7: the framework's own antiforgery step runs first, so a form-bound endpoint finds it ran (no 500);
        // the guard after it then overrides the framework's verdict for every request it exempts or accepts.
        app.UseAntiforgery();
        app.UseMiddleware<AntiforgeryGuardMiddleware>();
        app.Logger.LogInformation("{Feature} enabled", nameof(AntiforgeryConfig));
        return app;
    }
}
