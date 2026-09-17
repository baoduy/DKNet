// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ResultResponseExtensionsTests.cs
// Description: Unit tests for the short-form Response() helpers. DRK-1484 row 9 replaced the failure branch with
// a deferred IResult that resolves ErrorResponseOptions from HttpContext.RequestServices at execution time —
// covered here via a real ExecuteAsync against a DefaultHttpContext, and end to end (options actually applied)
// by DRK-1484's Acceptance tests over LedgerTestHost.

using System.Net;
using System.Text.Json;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Responses;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AspCore.Extensions.Tests.Responses;

public class ResultResponseExtensionsTests
{
    #region Methods

    private static DefaultHttpContext CreateHttpContext(ErrorResponseOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (options is not null) services.AddSingleton(options);

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
    }

    private static async Task<JsonElement> ExecuteAndReadBodyAsync(IResult result, HttpContext httpContext)
    {
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Position = 0;
        return await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
    }

    [Fact]
    public async Task Response_Failure_NoRegisteredOptions_ExecutesAsStandardProblemAt400()
    {
        var result = Result.Fail("error");
        var response = result.Response();

        var httpContext = CreateHttpContext();
        var body = await ExecuteAndReadBodyAsync(response, httpContext);

        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        body.GetProperty("title").GetString().ShouldBe("Error");
    }

    [Fact]
    public async Task Response_Failure_RegisteredOptions_AppliedWithoutBeingNamedByTheCaller()
    {
        var options = new ErrorResponseOptions { StatusCode = _ => 422 };
        var result = Result.Fail("error");

        var httpContext = CreateHttpContext(options);
        await result.Response().ExecuteAsync(httpContext);

        httpContext.Response.StatusCode.ShouldBe(422);
    }

    [Fact]
    public void Response_Success_IsCreatedFalse_ReturnsOk()
    {
        var result = Result.Ok();
        var response = result.Response();
        response.ShouldBeOfType<Ok>();
    }

    [Fact]
    public void Response_Success_IsCreatedTrue_ReturnsCreated()
    {
        var result = Result.Ok();
        var response = result.Response(true);
        response.ShouldBeOfType<Created>();
    }

    [Fact]
    public async Task ResponseT_Failure_NoRegisteredOptions_ExecutesAsStandardProblemAt400()
    {
        var result = Result.Fail<string>("error");
        var response = result.Response();

        var httpContext = CreateHttpContext();
        var body = await ExecuteAndReadBodyAsync(response, httpContext);

        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        body.GetProperty("title").GetString().ShouldBe("Error");
    }

    [Fact]
    public void ResponseT_Success_IsCreatedFalse_ValueNotNull_ReturnsJson()
    {
        var result = Result.Ok("value");
        var response = result.Response();
        response.ShouldBeOfType<JsonHttpResult<string>>();
    }

    [Fact]
    public void ResponseT_Success_IsCreatedFalse_ValueNull_ReturnsOk()
    {
        var result = Result.Ok<string>(default!);
        var response = result.Response();
        response.ShouldBeOfType<Ok>();
    }

    [Fact]
    public void ResponseT_Success_IsCreatedTrue_ReturnsCreated()
    {
        var result = Result.Ok("value");
        var response = result.Response(true);
        response.ShouldBeOfType<Created<string>>();
    }

    [Fact]
    public void Response_NullResult_ThrowsArgumentNullException()
    {
        IResultBase result = null!;

        Should.Throw<ArgumentNullException>(() => result.Response());
    }

    #endregion
}
