using DKNet.AspCore.SlimBus;
using FluentResults;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;

namespace AspCore.SlimBus.Tests;

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

    #endregion
}