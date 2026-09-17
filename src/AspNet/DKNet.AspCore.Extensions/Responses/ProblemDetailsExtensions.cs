// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ProblemDetailsExtensions.cs
// Description: Converts a failed FluentResults IResultBase into the DRK-1484 standard ProblemDetails shape,
// choosing the status code from the failure's own errors (never the route) via ErrorResponseOptions.

using System.Diagnostics;
using System.Net;
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Mvc;

namespace DKNet.AspCore.Extensions.Responses;

/// <summary>
///     Extensions that produce the standard <see cref="ProblemDetails" /> body from a failed FluentResults result.
/// </summary>
public static class ProblemDetailsExtensions
{
    #region Methods

    /// <summary>
    ///     Converts a failed <see cref="IResultBase" /> into the standard <see cref="ProblemDetails" /> body
    ///     (DRK-1484 §3), choosing the status code from the failure's own errors via <paramref name="options" /> —
    ///     never from the route that produced it — and applying <see cref="ErrorResponseOptions.Customize" />
    ///     afterwards. Returns <see langword="null" /> for success results. Internal: a caller with an
    ///     <see cref="Microsoft.AspNetCore.Http.HttpContext" /> available must go through
    ///     <see cref="ResultResponseExtensions.Response(IResultBase, bool)" />/<c>Response&lt;T&gt;</c> instead —
    ///     the only public path — which resolves the registered <see cref="ErrorResponseOptions" /> from the
    ///     container on its own and repairs <c>traceId</c> from the request afterwards. A no-argument public
    ///     overload here would let a caller answer a failure while silently skipping the host's registration.
    /// </summary>
    /// <param name="result">The fluent result to convert.</param>
    /// <param name="options">
    ///     The error-response setting to apply. A <see langword="null" /> <see cref="ErrorResponseOptions.StatusCode" />
    ///     result, or a <see langword="null" /> <paramref name="options" /> itself, keeps today's status code.
    /// </param>
    /// <returns>A <see cref="ProblemDetails" /> when the result is a failure; otherwise <c>null</c>.</returns>
    internal static ProblemDetails? ToProblemDetails(this IResultBase result, ErrorResponseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess) return null;

        var statusCode = result.Errors.Any(e => e is NotFoundError)
            ? HttpStatusCode.NotFound
            : HttpStatusCode.BadRequest;

        var context = result.ToErrorResponseContext();
        var traceId = Activity.Current?.Id ?? string.Empty;

        return ErrorProblemFactory.Create((int)statusCode, context, traceId, options);
    }

    /// <summary>
    ///     Builds the <see cref="ErrorResponseContext" /> an <see cref="ErrorResponseOptions" /> callback sees for a
    ///     failed command: one <see cref="ErrorItem" /> per <see cref="IResultBase.Errors" /> entry, its
    ///     <see cref="ErrorItem.Code" /> taken from the error's <c>"Code"</c> metadata entry when present.
    ///     <see cref="ErrorItem.Field" /> is always <see langword="null" /> — commands do not name an input member.
    /// </summary>
    /// <param name="result">The failed fluent result to derive the context from.</param>
    /// <returns>An <see cref="ErrorResponseContext" /> with <see cref="ErrorSource.Command" />.</returns>
    internal static ErrorResponseContext ToErrorResponseContext(this IResultBase result) =>
        new()
        {
            Source = ErrorSource.Command,
            Errors = result.Errors
                .Select(e => new ErrorItem(
                    e.Message,
                    e.Metadata.TryGetValue("Code", out var code) ? code as string : null))
                .ToList()
        };

    #endregion
}
