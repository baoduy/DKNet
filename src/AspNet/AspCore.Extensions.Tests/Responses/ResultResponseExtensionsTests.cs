using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Responses;
using FluentResults;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;

namespace AspCore.Extensions.Tests.Responses;

public class ResultResponseExtensionsTests
{
    #region Methods

    [Fact]
    public void Response_Failure_ReturnsProblem()
    {
        var result = Result.Fail("error");
        var response = result.Response();
        response.ShouldBeOfType<ProblemHttpResult>();
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
    public void ResponseT_Failure_ReturnsProblem()
    {
        var result = Result.Fail<string>("error");
        var response = result.Response();
        response.ShouldBeOfType<ProblemHttpResult>();
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
    public void Response_WithOptions_Failure_NoConfiguredStatus_ReturnsProblemAt400()
    {
        var result = Result.Fail("error");
        var response = result.Response((ErrorResponseOptions?)null);
        var problem = response.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(400);
    }

    [Fact]
    public void Response_WithOptions_Failure_ConfiguredStatus_ReturnsProblemAtConfiguredStatus()
    {
        var options = new ErrorResponseOptions { StatusCode = _ => 422 };
        var result = Result.Fail("error");
        var response = result.Response(options);
        var problem = response.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(422);
    }

    [Fact]
    public void Response_WithOptions_Success_IsCreatedFalse_ReturnsOk()
    {
        var result = Result.Ok();
        var response = result.Response((ErrorResponseOptions?)null);
        response.ShouldBeOfType<Ok>();
    }

    [Fact]
    public void Response_WithOptions_Success_IsCreatedTrue_ReturnsCreated()
    {
        var result = Result.Ok();
        var response = result.Response(null, true);
        response.ShouldBeOfType<Created>();
    }

    [Fact]
    public void ResponseT_WithOptions_Failure_ConfiguredStatus_ReturnsProblemAtConfiguredStatus()
    {
        var options = new ErrorResponseOptions { StatusCode = _ => 422 };
        var result = Result.Fail<string>("error");
        var response = result.Response(options);
        var problem = response.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(422);
    }

    [Fact]
    public void ResponseT_WithOptions_Success_IsCreatedFalse_ValueNotNull_ReturnsJson()
    {
        var result = Result.Ok("value");
        var response = result.Response((ErrorResponseOptions?)null);
        response.ShouldBeOfType<JsonHttpResult<string>>();
    }

    [Fact]
    public void ResponseT_WithOptions_Success_IsCreatedFalse_ValueNull_ReturnsOk()
    {
        var result = Result.Ok<string>(default!);
        var response = result.Response((ErrorResponseOptions?)null);
        response.ShouldBeOfType<Ok>();
    }

    [Fact]
    public void ResponseT_WithOptions_Success_IsCreatedTrue_ReturnsCreated()
    {
        var result = Result.Ok("value");
        var response = result.Response(null, true);
        var created = response.ShouldBeOfType<Created<string>>();
        created.Location.ShouldBe("/");
    }

    [Fact]
    public void Response_WithOptions_NullResult_ThrowsArgumentNullException()
    {
        IResultBase result = null!;

        Should.Throw<ArgumentNullException>(() => result.Response((ErrorResponseOptions?)null));
    }

    #endregion
}