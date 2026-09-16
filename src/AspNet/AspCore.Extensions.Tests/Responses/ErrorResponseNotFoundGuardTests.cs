// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseNotFoundGuardTests.cs
// Description: DRK-1328 R2 guard (Build-stage addition, not in the frozen AT set): a NotFoundError failure
// still answers 404 with an ErrorResponseOptions registered whose StatusCode returns null for it.

using DKNet.AspCore.Extensions.Responses;
using DKNet.SlimBus.Extensions;
using FluentResults;

namespace AspCore.Extensions.Tests.Responses;

public class ErrorResponseNotFoundGuardTests
{
    #region Methods

    [Fact]
    public void ToProblemDetails_NotFoundError_SettingRegisteredReturnsNullStatus_Keeps404()
    {
        var options = new ErrorResponseOptions { StatusCode = _ => null };
        var result = Result.Fail(new NotFoundError("Product abc not found"));

        var pd = result.ToProblemDetails(options);

        pd.ShouldNotBeNull();
        pd.Status.ShouldBe(404);
    }

    [Fact]
    public void ToProblemDetails_NotFoundErrorAlongsideOtherError_Keeps404()
    {
        var options = new ErrorResponseOptions { StatusCode = _ => null };
        var result = Result.Fail(new List<IError>
        {
            new Error("A plain business error."),
            new NotFoundError("Product abc not found")
        });

        var pd = result.ToProblemDetails(options);

        pd.ShouldNotBeNull();
        pd.Status.ShouldBe(404);
    }

    #endregion
}
