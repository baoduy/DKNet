using System.Net;
using System.Text.Json;
using DKNet.AspCore.SlimBus;
using FluentResults;
using Shouldly;
using Xunit.Abstractions;

namespace AspCore.SlimBus.Tests;

public class ToProblemDetailsTests(ITestOutputHelper testOutputHelper)
{
    #region Methods

    [Fact]
    public void ToProblems()
    {
        var rs = Result.Fail("The id is invalid.").ToProblemDetails();
        rs!.Status.ShouldBe((int)HttpStatusCode.BadRequest);
        rs.Title.ShouldBe("Error");

        testOutputHelper.WriteLine(JsonSerializer.Serialize(rs, JsonSerializerOptions.Default));
    }

    [Fact]
    public void ToProblemsNull()
    {
        var rs = Result.Ok("The id is invalid.").ToProblemDetails();
        rs.ShouldBeNull();
    }

    [Fact]
    public void ToProblemsWithDetails()
    {
        var rs = Result.Fail("The are many errors.")
            .WithError("bad code")
            .WithError("Stupid code")
            .ToProblemDetails();

        rs!.Status.ShouldBe((int)HttpStatusCode.BadRequest);
        testOutputHelper.WriteLine(JsonSerializer.Serialize(rs, JsonSerializerOptions.Default));
    }

    #endregion
}