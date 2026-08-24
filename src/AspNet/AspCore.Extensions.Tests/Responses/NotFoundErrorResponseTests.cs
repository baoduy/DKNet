// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: NotFoundErrorResponseTests.cs
// Description: Tests that a failed result carrying a NotFoundError maps to an HTTP 404 ProblemDetails.

using DKNet.AspCore.Extensions.Responses;
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Http;

namespace AspCore.Extensions.Tests.Responses;

public class NotFoundErrorResponseTests
{
    #region Methods

    [Fact]
    public void ToProblemDetails_WithNotFoundError_Returns404Status()
    {
        var pd = Result.Fail(new NotFoundError("Product abc not found")).ToProblemDetails();

        pd.ShouldNotBeNull();
        pd.Status.ShouldBe(StatusCodes.Status404NotFound);
        pd.Detail.ShouldBe("Product abc not found");
    }

    [Fact]
    public void ToProblemDetails_WithPlainError_Returns400Status()
    {
        var pd = Result.Fail(new Error("boom")).ToProblemDetails();

        pd.ShouldNotBeNull();
        pd.Status.ShouldBe(StatusCodes.Status400BadRequest);
    }

    #endregion
}
