// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseOptions.cs
// Description: One error-response setting shared by a failed command handler and a refused validation
// input (DRK-1328) — the status code and extra body members both failure paths answer with.

using Microsoft.AspNetCore.Mvc;

namespace DKNet.AspCore.Extensions.Responses;

/// <summary>
///     Which failure path produced an <see cref="ErrorResponseContext" />: a failed FluentResults command, or
///     input refused by FluentValidation.
/// </summary>
public enum ErrorSource
{
    /// <summary>A FluentResults command handler returned a failed <see cref="FluentResults.IResultBase" />.</summary>
    Command,

    /// <summary>FluentValidation refused the request input.</summary>
    Validation
}

/// <summary>
///     One error carried by a failed command or a refused input. <see cref="Code" /> is the machine-readable
///     discriminator the failure itself carries (a FluentResults error's <c>"Code"</c> metadata entry for
///     <see cref="ErrorSource.Command" />, a validation failure's <c>ErrorCode</c> for
///     <see cref="ErrorSource.Validation" />); <see cref="Field" /> is the input member a validation failure
///     names, and is always <see langword="null" /> for a command failure.
/// </summary>
/// <param name="Message">The failure's human-readable message.</param>
/// <param name="Code">The failure's machine-readable discriminator, when it carries one.</param>
/// <param name="Field">The input member a validation failure names, when it names one.</param>
public sealed record ErrorItem(string Message, string? Code = null, string? Field = null);

/// <summary>
///     What an <see cref="ErrorResponseOptions" /> callback sees: which path failed and the errors it carries.
///     Deliberately carries nothing route-derived (no <c>HttpContext</c>, path or HTTP method) — the status code
///     a service chooses must come from the failure, never from the route that produced it.
/// </summary>
public sealed class ErrorResponseContext
{
    /// <summary>Which failure path produced this context.</summary>
    public required ErrorSource Source { get; init; }

    /// <summary>The errors the failure carries.</summary>
    public required IReadOnlyList<ErrorItem> Errors { get; init; }
}

/// <summary>
///     One error-response setting for a host application, applied to both a failed command handler and refused
///     validation input. Registered once via
///     <see cref="ErrorResponseServiceCollectionExtensions.AddErrorResponses" />. Every default reproduces
///     today's behaviour unchanged: a host that registers no setting, or leaves a member <see langword="null" />,
///     keeps today's status code and today's body for both failure kinds.
/// </summary>
public sealed class ErrorResponseOptions
{
    /// <summary>
    ///     Chooses the HTTP status code for a failure from the information it carries. Returning
    ///     <see langword="null" />, or leaving this unset, keeps today's status code for that failure.
    /// </summary>
    public Func<ErrorResponseContext, int?>? StatusCode { get; set; }

    /// <summary>
    ///     Adds members to the <see cref="ProblemDetails" /> after its status is chosen. Runs for both
    ///     <see cref="ErrorSource.Command" /> and <see cref="ErrorSource.Validation" /> failures, so a member added
    ///     here never appears on one failure kind only.
    /// </summary>
    public Action<ProblemDetails, ErrorResponseContext>? Customize { get; set; }
}
