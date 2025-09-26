using DKNet.EfCore.Events;
using FluentResults;

namespace EfCore.Events.Tests;

public class EventExceptionTests
{
    [Fact]
    public void ConstructorSetsStatusProperty()
    {
        var expectedStatus = Result.Fail("Test error");
        var exception = new EventException(expectedStatus);

        Assert.Same(expectedStatus, exception.Status);
    }

    [Fact]
    public void MessageMatchesStatusToString()
    {
        var status = Result.Fail("Validation error")
            .WithError("Field required");

        var exception = new EventException(status);

        Assert.Equal(status.Errors[0].Message, exception.Message);
    }

    [Theory]
    [InlineData("Single error")]
    [InlineData("Multiple\nerrors")]
    [InlineData("")]
    public void MessageAccuratelyRepresentsDifferentStatuses(string errorMessage)
    {
        var status = Result.Fail(errorMessage);
        var exception = new EventException(status);

        Assert.Equal(errorMessage, exception.Message);
    }
}