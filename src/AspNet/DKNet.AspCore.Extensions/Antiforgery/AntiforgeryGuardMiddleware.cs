// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: AntiforgeryGuardMiddleware.cs
// Description: The double-submit antiforgery guard UseAntiforgeryConfig adds: issues the readable token cookie on
// every GET and checks every state-changing request that carries no bearer token.

using System.Diagnostics;
using DKNet.AspCore.Extensions.Responses;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace DKNet.AspCore.Extensions.Antiforgery;

/// <summary>
///     On a GET, stores the antiforgery tokens (keeping a valid secret cookie) and sends the request token in the
///     readable <see cref="AntiforgeryGuardOptions.TokenCookieName" /> cookie. On a POST, PUT, PATCH or DELETE that
///     has no bearer token and whose endpoint does not turn the check off, requires a valid token in the
///     <see cref="AntiforgeryGuardOptions.HeaderName" /> header, else answers DKNet's standard 400 problem body.
/// </summary>
/// <param name="next">The next step in the pipeline.</param>
/// <param name="antiforgery">The framework's antiforgery token service.</param>
/// <param name="options">The guard's cookie and header names.</param>
internal sealed class AntiforgeryGuardMiddleware(
    RequestDelegate next,
    IAntiforgery antiforgery,
    AntiforgeryGuardOptions options)
{
    private static readonly string[] CheckedMethods =
        [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];

    /// <summary>Runs the guard for one request.</summary>
    /// <param name="context">The request's context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method))
        {
            IssueTokenCookie(context);
        }
        else if (MustCheck(context) && !await IsAcceptedAsync(context))
        {
            await RefuseAsync(context);
            return;
        }

        // R7: every request reaching here was exempted, accepted or never checked, so the framework's own
        // verdict (from UseAntiforgery) must not refuse it on a form-bound endpoint.
        context.Features.Set<IAntiforgeryValidationFeature>(ValidFeature.Instance);
        await next(context);
    }

    private void IssueTokenCookie(HttpContext context)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(options.TokenCookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }

    private static bool MustCheck(HttpContext context) =>
        CheckedMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase)
        && !HasBearerToken(context.Request)
        && context.GetEndpoint()?.Metadata.GetMetadata<IAntiforgeryMetadata>() is not { RequiresValidation: false };

    // R3: header presence only — whether the token is valid is authentication's call.
    private static bool HasBearerToken(HttpRequest request)
    {
        var values = request.Headers.Authorization;
        if (values.Count != 1) return false;

        var value = values[0];
        return value is not null
               && value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrWhiteSpace(value["Bearer ".Length..]);
    }

    // R5: the header must be present, so a token sent only as a form field is refused.
    private async Task<bool> IsAcceptedAsync(HttpContext context) =>
        context.Request.Headers.ContainsKey(options.HeaderName)
        && await antiforgery.IsRequestValidAsync(context);

    private static Task RefuseAsync(HttpContext context)
    {
        var errorContext = new ErrorResponseContext
        {
            Source = ErrorSource.Validation,
            Errors = [new ErrorItem("The antiforgery token is missing or invalid.", "Antiforgery")]
        };
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var pd = ErrorProblemFactory.Create(StatusCodes.Status400BadRequest, errorContext, traceId, options: null);

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return context.Response.WriteAsJsonAsync(pd, options: null, contentType: "application/problem+json");
    }

    private sealed class ValidFeature : IAntiforgeryValidationFeature
    {
        public static readonly ValidFeature Instance = new();

        public bool IsValid => true;

        public Exception? Error => null;
    }
}
