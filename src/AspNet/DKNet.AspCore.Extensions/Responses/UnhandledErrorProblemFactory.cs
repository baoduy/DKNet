// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: UnhandledErrorProblemFactory.cs
// Description: DRK-1484 — builds the standard ProblemDetails body for an unhandled exception. Shared by
// ErrorResponseExceptionHandler (the IExceptionHandler AddErrorResponses registers, for exceptions the mapped
// endpoints below don't catch themselves) and FluentsEndpointMapperExtensions (which catches an exception
// raised while dispatching a command directly, since ASP.NET Core's own Development-only exception page sits
// closer to the endpoint than any IExceptionHandler and would otherwise answer first — see row 11/13 notes).

using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Extensions.Responses;

/// <summary>
///     Builds the standard <see cref="ProblemDetails" /> body for an unhandled exception (<see cref="ErrorSource.Unhandled" />),
///     honouring <see cref="ErrorResponseOptions.UnhandledError" /> when the host supplied one.
/// </summary>
internal static class UnhandledErrorProblemFactory
{
    /// <summary>
    ///     Builds the body for <paramref name="exception" />. R4/R5: outside <c>Development</c> the body never
    ///     repeats anything the exception carried; in <c>Development</c> the single entry may carry the
    ///     exception's own message.
    /// </summary>
    /// <param name="httpContext">The context of the request that raised <paramref name="exception" />.</param>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="options">The error-response setting registered via <c>AddErrorResponses</c>, if any.</param>
    /// <returns>The <see cref="ProblemDetails" /> to write to the response.</returns>
    public static ProblemDetails Create(HttpContext httpContext, Exception exception, ErrorResponseOptions? options)
    {
        // IWebHostEnvironment, not IHostEnvironment: it is the type builder.Environment.EnvironmentName sets on
        // WebApplicationBuilder, and the type ASP.NET Core's own developer-exception-page decision reads — the
        // very check this design works around (DRK-1490 round 2). Fully qualified: Microsoft.AspNetCore.Hosting
        // also declares an obsolete IHostingEnvironment.IsDevelopment() extension that would otherwise win
        // overload resolution over Microsoft.Extensions.Hosting's IHostEnvironment.IsDevelopment().
        var env = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var isDevelopment = Microsoft.Extensions.Hosting.HostEnvironmentEnvExtensions.IsDevelopment(env);

        var message = isDevelopment
            ? exception.Message
            : "An unexpected error occurred. Quote the trace-id when reporting this.";

        var context = new ErrorResponseContext
        {
            Source = ErrorSource.Unhandled,
            Errors = [new ErrorItem(message)],
            Exception = exception
        };

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var pd = options?.UnhandledError?.Invoke(context)
                 ?? new ProblemDetails
                 {
                     Title = "Error",
                     Status = StatusCodes.Status500InternalServerError,
                     Type = HttpStatusCode.InternalServerError.ToString(),
                     Extensions = { ["errors"] = context.Errors }
                 };

        pd.Status ??= StatusCodes.Status500InternalServerError;

        return ErrorProblemFactory.Finalize(pd, context, traceId, options);
    }
}
