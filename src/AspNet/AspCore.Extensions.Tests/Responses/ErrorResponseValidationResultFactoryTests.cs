// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseValidationResultFactoryTests.cs
// Description: Direct unit tests for ErrorResponseValidationResultFactory (Build-stage addition, not in the
// frozen AT set) — the default 400 status with no StatusCode configured, and that an unset ErrorCode reaches
// ErrorResponseOptions callbacks as null rather than an empty string.

using DKNet.AspCore.Extensions.Responses;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AspCore.Extensions.Tests.Responses;

public class ErrorResponseValidationResultFactoryTests
{
    #region Methods

    [Fact]
    public void CreateResult_NoStatusCodeConfigured_DefaultsTo400()
    {
        var factory = new ErrorResponseValidationResultFactory(new ErrorResponseOptions());
        var validationResult = new ValidationResult([new ValidationFailure("Field", "message")]);
        var context = EndpointFilterInvocationContext.Create(new DefaultHttpContext());

        var result = factory.CreateResult(context, validationResult);

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(400);
    }

    [Fact]
    public void CreateResult_FailureWithoutErrorCode_ContextCodeIsNullNotEmpty()
    {
        string? capturedCode = "not set";
        var options = new ErrorResponseOptions
        {
            Customize = (_, ctx) => capturedCode = ctx.Errors[0].Code
        };
        var factory = new ErrorResponseValidationResultFactory(options);
        var failure = new ValidationFailure("Field", "message") { ErrorCode = "" };
        var validationResult = new ValidationResult([failure]);
        var context = EndpointFilterInvocationContext.Create(new DefaultHttpContext());

        factory.CreateResult(context, validationResult);

        capturedCode.ShouldBeNull();
    }

    #endregion
}
