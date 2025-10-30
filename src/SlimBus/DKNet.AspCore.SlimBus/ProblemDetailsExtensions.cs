// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ProblemDetailsExtensions.cs
// Description: Helpers to convert FluentResults and ModelStateDictionary into ASP.NET Core ProblemDetails.

using System.Net;
using FluentResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DKNet.AspCore.SlimBus;

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
    /// <param name="statusCode">The HTTP status code to use when creating the ProblemDetails (default 400).</param>
    /// <returns>A <see cref="ProblemDetails" /> when the result is a failure; otherwise <c>null</c>.</returns>
    public static ProblemDetails? ToProblemDetails(
        this IResultBase result,
        HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess) return null;

        var errors = result.Errors.Select(e => e.Message).Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var firstMessage = errors.FirstOrDefault() ?? statusCode.ToString();

        return CreateProblemDetails(statusCode, firstMessage, errors);
    }

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