// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseExceptionHandler.cs
// Description: DRK-1484 — the IExceptionHandler AddErrorResponses registers so an unhandled exception answers
// with the same ErrorResponseOptions-shaped body as a failed command or refused input. Open for inheritance so
// a host can replace the handling step without replacing the abandoned-request guard (R6).

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DKNet.AspCore.Extensions.Responses;

/// <summary>
///     Converts an unhandled exception into the same <see cref="ProblemDetails" /> shape a failed command or
///     refused input answers with, honouring <see cref="ErrorResponseOptions.UnhandledError" /> when the host
///     supplied one. Registered by <see cref="ErrorResponseServiceCollectionExtensions.AddErrorResponses" />; not
///     sealed so a host can override <see cref="CreateProblemDetails" /> to replace the handling step while
///     keeping the abandoned-request guard.
/// </summary>
public class ErrorResponseExceptionHandler : IExceptionHandler
{
    private readonly ErrorResponseOptions _options;

    /// <param name="options">The error-response setting registered via <c>AddErrorResponses</c>.</param>
    public ErrorResponseExceptionHandler(ErrorResponseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // R6: an abandoned request is not answered and is not logged as an error. Never write once the
        // response has already started, regardless of the reason we were called.
        if (httpContext.Response.HasStarted) return true;

        if (httpContext.RequestAborted.IsCancellationRequested) return true;

        var pd = CreateProblemDetails(httpContext, exception);

        httpContext.Response.StatusCode = pd.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(pd, options: null, contentType: "application/problem+json", cancellationToken);

        return true;
    }

    /// <summary>
    ///     Builds the <see cref="ProblemDetails" /> body for <paramref name="exception" />. The replaceable
    ///     handling step: honours <see cref="ErrorResponseOptions.UnhandledError" /> first, falling back to the
    ///     library's own shape when the host supplied none or it returned <see langword="null" />.
    /// </summary>
    /// <param name="httpContext">The context of the request that raised <paramref name="exception" />.</param>
    /// <param name="exception">The unhandled exception.</param>
    /// <returns>The <see cref="ProblemDetails" /> to write to the response.</returns>
    protected virtual ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception) =>
        UnhandledErrorProblemFactory.Create(httpContext, exception, _options);
}
