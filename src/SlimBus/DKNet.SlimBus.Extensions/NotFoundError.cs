// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: NotFoundError.cs
// Description: A FluentResults error marking a failed result as "not found", so ASP.NET Core response
// mapping (see DKNet.AspCore.Extensions.Responses.ProblemDetailsExtensions) can translate it to HTTP 404.

using FluentResults;

namespace DKNet.SlimBus.Extensions;

/// <summary>
///     A <see cref="FluentResults.Error" /> that signals the requested resource could not be found. Handlers should
///     return <c>Result.Fail(new NotFoundError(message))</c> when a lookup by id fails; the ASP.NET Core response
///     mapping in <c>DKNet.AspCore.Extensions</c> recognizes this type and produces an HTTP 404 response.
/// </summary>
public sealed class NotFoundError : Error
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NotFoundError" /> class.
    /// </summary>
    /// <param name="message">A message describing which resource was not found.</param>
    public NotFoundError(string message) : base(message)
    {
    }
}
