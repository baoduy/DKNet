// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseValidationResultFactory.cs
// Description: The FluentValidation endpoint result factory DRK-1328's AddErrorResponses registers in place of
// SharpGrip's default — reproduces today's field->messages 400 body, then applies the same
// ErrorResponseOptions.StatusCode and Customize the failed-command path applies.

using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Results;
using SharpGrip.FluentValidation.AutoValidation.Shared.Extensions;

namespace DKNet.AspCore.Extensions.Responses;

/// <summary>
///     Converts a refused <see cref="ValidationResult" /> into an <see cref="IResult" />, choosing the status code
///     via <see cref="ErrorResponseOptions.StatusCode" /> and applying <see cref="ErrorResponseOptions.Customize" />
///     afterwards — the FluentValidation-side counterpart of the <see cref="ProblemDetailsExtensions" />
///     overload that does the same for a failed command. Registered by
///     <see cref="ErrorResponseServiceCollectionExtensions.AddErrorResponses" />; never constructed directly by a
///     host.
/// </summary>
/// <param name="options">The error-response setting registered via <c>AddErrorResponses</c>.</param>
internal sealed class ErrorResponseValidationResultFactory(ErrorResponseOptions options)
    : IFluentValidationAutoValidationResultFactory
{
    /// <inheritdoc />
    public IResult CreateResult(EndpointFilterInvocationContext context, ValidationResult validationResult)
    {
        var pd = new HttpValidationProblemDetails(validationResult.ToValidationProblemErrors())
        {
            Status = StatusCodes.Status400BadRequest
        };

        var errorContext = validationResult.ToErrorResponseContext();

        var configuredStatus = options.StatusCode?.Invoke(errorContext);
        if (configuredStatus is not null)
            pd.Status = configuredStatus;

        options.Customize?.Invoke(pd, errorContext);

        return TypedResults.Problem(pd);
    }
}

/// <summary>
///     Builds the <see cref="ErrorResponseContext" /> an <see cref="ErrorResponseOptions" /> callback sees for
///     refused input: one <see cref="ErrorItem" /> per <see cref="ValidationResult.Errors" /> entry,
///     <see cref="ErrorItem.Code" /> from <see cref="ValidationFailure.ErrorCode" /> and
///     <see cref="ErrorItem.Field" /> from <see cref="ValidationFailure.PropertyName" />. File-scoped: only
///     <see cref="ErrorResponseValidationResultFactory" /> needs this mapping.
/// </summary>
file static class ValidationResultErrorContextExtensions
{
    public static ErrorResponseContext ToErrorResponseContext(this ValidationResult validationResult) =>
        new()
        {
            Source = ErrorSource.Validation,
            Errors = validationResult.Errors
                .Select(f => new ErrorItem(
                    f.ErrorMessage,
                    string.IsNullOrEmpty(f.ErrorCode) ? null : f.ErrorCode,
                    f.PropertyName))
                .ToList()
        };
}
