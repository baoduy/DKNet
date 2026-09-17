// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseExceptionHandlerTests.cs
// Description: Direct unit tests for ErrorResponseExceptionHandler (Build-stage addition, not in the frozen AT
// set) — R6's abandoned-request guard and the standard body it writes when it does handle an exception. The
// end-to-end "an unhandled exception answers with the unified body" scenarios live in
// UnifiedErrorResponseEndpointTests, dispatched through FluentsEndpointMapperExtensions' own catch (this
// handler is registered as defense in depth for exceptions raised outside a mapped command endpoint).

using System.Text.Json;
using DKNet.AspCore.Extensions.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AspCore.Extensions.Tests.Responses;

public class ErrorResponseExceptionHandlerTests
{
    #region Methods

    private static DefaultHttpContext CreateHttpContext(string environmentName)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new StubHostEnvironment { EnvironmentName = environmentName });

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
    }

    [Fact]
    public async Task TryHandleAsync_ResponseAlreadyStarted_ReturnsTrueWithoutWriting()
    {
        var handler = new ErrorResponseExceptionHandler(new ErrorResponseOptions());
        var httpContext = CreateHttpContext("Production");
        httpContext.Features.Set<IHttpResponseFeature>(new AlreadyStartedResponseFeature());

        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.Body.Length.ShouldBe(0);
    }

    [Fact]
    public async Task TryHandleAsync_RequestAborted_ReturnsTrueWithoutWritingOrThrowing()
    {
        var handler = new ErrorResponseExceptionHandler(new ErrorResponseOptions());
        var httpContext = CreateHttpContext("Production");
        httpContext.RequestAborted = new CancellationToken(true);

        var handled = await handler.TryHandleAsync(httpContext, new OperationCanceledException(), CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.Body.Length.ShouldBe(0);
    }

    [Fact]
    public async Task TryHandleAsync_Production_WritesStandardBodyWithoutExceptionMessage()
    {
        var handler = new ErrorResponseExceptionHandler(new ErrorResponseOptions());
        var httpContext = CreateHttpContext("Production");

        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("connection string: secret"), CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        httpContext.Response.ContentType.ShouldBe("application/problem+json");

        httpContext.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
        body.GetProperty("title").GetString().ShouldBe("Error");
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.ShouldHaveSingleItem();
        errors[0].GetProperty("message").GetString()!.ShouldNotContain("secret");
    }

    [Fact]
    public async Task TryHandleAsync_Development_WritesExceptionMessage()
    {
        var handler = new ErrorResponseExceptionHandler(new ErrorResponseOptions());
        var httpContext = CreateHttpContext("Development");

        await handler.TryHandleAsync(httpContext, new InvalidOperationException("dev-detail"), CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors[0].GetProperty("message").GetString().ShouldBe("dev-detail");
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "AspCore.Extensions.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class AlreadyStartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }

    #endregion
}
