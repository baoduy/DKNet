// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ProblemDetailsExtensions.cs
// Description: Helpers to convert FluentResults and ModelStateDictionary into ASP.NET Core ProblemDetails.

using System.Net;
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DKNet.AspCore.Extensions.Responses;

/// <summary>
///     Extensions that produce <see cref="ProblemDetails" /> from common error carriers used by this project
///     (FluentResults and ModelStateDictionary).
/// </summary>
public static class ProblemDetailsExtensions
{
    #region Methods

    /// <summary>
    ///     Creates a <see cref="ProblemDetails" /> instance with the provided status, detail and a collection of errors
    ///     stored in the <c>errors</c> extension property.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to set on the ProblemDetails.</param>
    /// <param name="detail">A short detail message describing the problem.</param>
    /// <param name="errors">A collection of error messages to attach to the ProblemDetails extensions.</param>
    /// <returns>A populated <see cref="ProblemDetails" /> instance.</returns>
    private static ProblemDetails CreateProblemDetails(
        HttpStatusCode statusCode,
        string detail,
        IEnumerable<string> errors) =>
        new()
        {
            Status = (int)statusCode,
            Type = statusCode.ToString(),
            Title = "Error",
            Detail = detail,
            Extensions =
            {
                ["errors"] = errors
            }
        };

    /// <summary>
    ///     Converts a <see cref="IResultBase" /> produced by FluentResults into a <see cref="ProblemDetails" /> instance
    ///     when the result represents a failure; returns <c>null</c> for success results.
    /// </summary>
    /// <param name="result">The fluent result to convert.</param>
    /// <param name="statusCode">
    ///     The HTTP status code to use when creating the ProblemDetails (default 400). Overridden to 404 when
    ///     <paramref name="result" /> carries a <see cref="NotFoundError" />.
    /// </param>
    /// <returns>A <see cref="ProblemDetails" /> when the result is a failure; otherwise <c>null</c>.</returns>
    public static ProblemDetails? ToProblemDetails(
        this IResultBase result,
        HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess) return null;

        if (result.Errors.Any(e => e is NotFoundError))
            statusCode = HttpStatusCode.NotFound;

        var errors = result.Errors.Select(e => e.Message)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var firstMessage = errors.FirstOrDefault() ?? statusCode.ToString();
        return CreateProblemDetails(statusCode, firstMessage, errors);
    }

    /// <summary>
    ///     Converts a failed <see cref="IResultBase" /> into a <see cref="ProblemDetails" /> instance, choosing the
    ///     status code from the failure's own errors via <paramref name="options" /> — never from the route that
    ///     produced it — and applying <see cref="ErrorResponseOptions.Customize" /> afterwards. Returns
    ///     <see langword="null" /> for success results.
    /// </summary>
    /// <param name="result">The fluent result to convert.</param>
    /// <param name="options">
    ///     The error-response setting to apply. A <see langword="null" /> <see cref="ErrorResponseOptions.StatusCode" />
    ///     result, or a <see langword="null" /> <paramref name="options" /> itself, keeps today's status code.
    /// </param>
    /// <returns>A <see cref="ProblemDetails" /> when the result is a failure; otherwise <c>null</c>.</returns>
    public static ProblemDetails? ToProblemDetails(this IResultBase result, ErrorResponseOptions? options)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess) return null;

        var statusCode = HttpStatusCode.BadRequest;
        if (result.Errors.Any(e => e is NotFoundError))
            statusCode = HttpStatusCode.NotFound;

        var errors = result.Errors.Select(e => e.Message)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var firstMessage = errors.FirstOrDefault() ?? statusCode.ToString();
        var pd = CreateProblemDetails(statusCode, firstMessage, errors);

        if (options is null) return pd;

        var context = result.ToErrorResponseContext();

        var configuredStatus = options.StatusCode?.Invoke(context);
        if (configuredStatus is not null)
            pd.Status = configuredStatus;

        options.Customize?.Invoke(pd, context);

        return pd;
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

    /// <summary>
    ///     Converts an ASP.NET Core <see cref="ModelStateDictionary" /> into a <see cref="ProblemDetails" /> instance
    ///     when the model state contains validation errors; returns <c>null</c> when the model state is valid.
    /// </summary>
    /// <param name="status">The ModelStateDictionary to convert.</param>
    /// <returns>A <see cref="ProblemDetails" /> when model state is invalid; otherwise <c>null</c>.</returns>
    public static ProblemDetails? ToProblemDetails(this ModelStateDictionary status)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (status.IsValid) return null;

        var errors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, value) in status)
            foreach (var err in value.Errors)
                if (!string.IsNullOrWhiteSpace(err.ErrorMessage))
                    errors.Add(err.ErrorMessage);

        var firstMessage = errors.FirstOrDefault() ?? nameof(HttpStatusCode.BadRequest);
        return CreateProblemDetails(HttpStatusCode.BadRequest, firstMessage, errors);
    }

    #endregion
}