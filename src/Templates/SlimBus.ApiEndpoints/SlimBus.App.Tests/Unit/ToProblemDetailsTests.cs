using SlimBus.Api.Extensions;

namespace SlimBus.App.Tests.Unit;

public class ToProblemDetailsTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void ToProblemsNull()
    {
        var rs = Result.Ok("The id is invalid.").ToProblemDetails();
        rs.ShouldBeNull();
    }

    [Fact]
    public void ToProblems()
    {
        var rs = Result.Fail("The id is invalid.").ToProblemDetails();
        rs!.Status.ShouldBe((int)HttpStatusCode.BadRequest);
        rs.Title.ShouldBe("Error");

        testOutputHelper.WriteLine(JsonSerializer.Serialize(rs, JsonSerializerOptions.Default));
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
}