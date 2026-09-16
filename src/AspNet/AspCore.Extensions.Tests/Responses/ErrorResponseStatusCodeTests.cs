// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseStatusCodeTests.cs
// Description: DRK-1328 §5 @unit scenario — the configured status code comes from the failure's own errors,
// never from the route: calls ToProblemDetails(IResultBase, ErrorResponseOptions) directly, with no HTTP
// endpoint involved at all.

using DKNet.AspCore.Extensions.Responses;
using FluentResults;

namespace AspCore.Extensions.Tests.Responses;

public class ErrorResponseStatusCodeTests
{
    #region Methods

    [Fact]
    public void ToProblemDetails_IdempotencyConflictMappedTo409_StatusComesFromFailureNotRoute()
    {
        var options = new ErrorResponseOptions
        {
            StatusCode = ctx => ctx.Errors.Any(e => e.Code == "idempotency-conflict") ? 409 : null
        };
        var result = Result.Fail(new Error("Duplicate idempotency key.").WithMetadata("Code", "idempotency-conflict"));

        var pd = result.ToProblemDetails(options);

        pd.ShouldNotBeNull();
        pd.Status.ShouldBe(409);
    }

    #endregion
}
