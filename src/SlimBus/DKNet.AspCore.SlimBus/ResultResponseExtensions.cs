// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ResultResponseExtensions.cs
// Description: Extension helpers to convert FluentResults IResult/IResultBase into ASP.NET Core minimal API IResult responses.

using FluentResults;
using Microsoft.AspNetCore.Http;

namespace DKNet.AspCore.SlimBus;

/// <summary>
///     Helper extensions that convert FluentResults result objects into minimal-API <see cref="IResult" /> responses
///     (Created/Ok/Json/Problem) used across the project.
/// </summary>
public static class ResultResponseExtensions
{
    #region Methods

    /// <summary>
    ///     Converts a typed Fluent result into an <see cref="IResult" />. On failure a ProblemDetails response is returned.
    ///     On success the method returns:
    ///     - Created(location, value) when <paramref name="isCreated" /> is <c>true</c>.
    ///     - Ok() when the value is null and success.
    ///     - Json(value) when the value is non-null and success.
    /// </summary>
    /// <typeparam name="TObject">The result value type.</typeparam>
    /// <param name="result">The Fluent result to convert. Must not be null.</param>
    /// <param name="isCreated">Indicates whether a successful response should be a 201 Created.</param>
    /// <returns>An <see cref="IResult" /> representing the appropriate HTTP response.</returns>
    public static IResult Response<TObject>(this IResult<TObject> result, bool isCreated = false)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            var pd = result.ToProblemDetails();
            return pd is not null ? TypedResults.Problem(pd) : TypedResults.Problem();
        }

        if (isCreated)
            // Created requires a location; callers in this codebase usually provide "/" as placeholder.
            return TypedResults.Created("/", result.Value);

        return result.ValueOrDefault is null ? TypedResults.Ok() : TypedResults.Json(result.Value);
    }

    /// <summary>
    ///     Converts a non-generic Fluent result into an <see cref="IResult" />. On success this returns Ok (or Created when
    ///     <paramref name="isCreated" /> is true), and on failure a ProblemDetails response is returned.
    /// </summary>
    /// <param name="result">The Fluent result to convert. Must not be null.</param>
    /// <param name="isCreated">When <c>true</c> a successful response will be 201 Created.</param>
    /// <returns>An <see cref="IResult" /> representing the appropriate HTTP response.</returns>
    public static IResult Response(this IResultBase result, bool isCreated = false)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess) return isCreated ? TypedResults.Created() : TypedResults.Ok();

        var pd = result.ToProblemDetails();
        return pd is not null ? TypedResults.Problem(pd) : TypedResults.Problem();
    }

    #endregion
}