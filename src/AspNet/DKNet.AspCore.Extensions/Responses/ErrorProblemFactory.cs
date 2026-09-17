// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorProblemFactory.cs
// Description: DRK-1484 — the one ProblemDetails body builder every failure path (failed command, refused
// input, unhandled exception) uses, so all three answer with the same shape driven by one ErrorResponseOptions.

using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace DKNet.AspCore.Extensions.Responses;

/// <summary>
///     Builds the standard <see cref="ProblemDetails" /> body (title, status, type, trace identifier, error list)
///     every failure kind answers with, and applies an <see cref="ErrorResponseOptions" /> setting to it.
/// </summary>
internal static class ErrorProblemFactory
{
    /// <summary>
    ///     Builds the standard body for <paramref name="context" /> at <paramref name="statusCode" />, then applies
    ///     <paramref name="options" /> to it (see <see cref="Finalize" />).
    /// </summary>
    /// <param name="statusCode">The failure's default HTTP status code, before any <see cref="ErrorResponseOptions.StatusCode" /> override.</param>
    /// <param name="context">The failure's <see cref="ErrorResponseContext" />.</param>
    /// <param name="traceId">The trace identifier to carry on the body (R8).</param>
    /// <param name="options">The error-response setting to apply; <see langword="null" /> keeps the standard body untouched.</param>
    /// <returns>The finished <see cref="ProblemDetails" />.</returns>
    public static ProblemDetails Create(int statusCode, ErrorResponseContext context, string traceId, ErrorResponseOptions? options)
    {
        var pd = new ProblemDetails
        {
            Title = "Error",
            Status = statusCode,
            Type = ((HttpStatusCode)statusCode).ToString()
        };
        pd.Extensions["errors"] = context.Errors;

        return Finalize(pd, context, traceId, options);
    }

    /// <summary>
    ///     Applies the trace identifier (R8) and an <see cref="ErrorResponseOptions" /> setting to an already-built
    ///     <paramref name="pd" />: <see cref="ErrorResponseOptions.StatusCode" /> may override the status, after
    ///     which <see cref="ProblemDetails.Type" /> is recomputed from the FINAL status (R3), and
    ///     <see cref="ErrorResponseOptions.Customize" /> always runs last (R7).
    /// </summary>
    /// <param name="pd">The body to finish — built by <see cref="Create" /> or supplied by <see cref="ErrorResponseOptions.UnhandledError" />.</param>
    /// <param name="context">The failure's <see cref="ErrorResponseContext" />.</param>
    /// <param name="traceId">The trace identifier to carry on the body (R8).</param>
    /// <param name="options">The error-response setting to apply; <see langword="null" /> skips the callbacks.</param>
    /// <returns><paramref name="pd" />, for chaining.</returns>
    public static ProblemDetails Finalize(ProblemDetails pd, ErrorResponseContext context, string traceId, ErrorResponseOptions? options)
    {
        pd.Extensions["traceId"] = traceId;

        if (options is null) return pd;

        var configuredStatus = options.StatusCode?.Invoke(context);
        if (configuredStatus is not null)
        {
            pd.Status = configuredStatus;
            pd.Type = ((HttpStatusCode)configuredStatus.Value).ToString();
        }

        options.Customize?.Invoke(pd, context);

        return pd;
    }
}
