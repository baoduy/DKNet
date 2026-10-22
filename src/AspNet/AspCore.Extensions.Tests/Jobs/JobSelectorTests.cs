// DRK-2362 §5 acceptance test — job selection (DRK-2375). Fails today: JobSelector.Select is a stub @ the
// acceptance-tests commit and throws NotImplementedException.

using DKNet.AspCore.Extensions.Jobs;

namespace AspCore.Extensions.Tests.Jobs;

public class JobSelectorTests
{
    #region Methods

    // Scenario Outline: Job selection reads the requested job from process arguments
    [Theory]
    [InlineData("migration", "the job \"migration\"")]
    [InlineData("MIGRATION", "the job \"migration\"")]
    [InlineData("--environment Production migration", "the job \"migration\"")]
    [InlineData("--environment=Production migration", "the job \"migration\"")]
    [InlineData("--environment migration", "no job")]
    [InlineData("", "no job")]
    [InlineData("cleanup", "the unknown job \"cleanup\"")]
    public void Select_ProcessArguments_ReadsRequestedJob(string arguments, string selection)
    {
        string[] args = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var result = JobSelector.Select(args, ["migration"]);

        result.KnownJobNames.ShouldBe(["migration"]);
        switch (selection)
        {
            case "the job \"migration\"":
                result.HasJobName.ShouldBeTrue();
                result.IsRecognized.ShouldBeTrue();
                result.JobName.ShouldBe("migration");
                result.RequestedJobName.ShouldBe(args[^1]);
                break;
            case "no job":
                result.HasJobName.ShouldBeFalse();
                result.IsRecognized.ShouldBeFalse();
                result.JobName.ShouldBeNull();
                result.RequestedJobName.ShouldBeNull();
                break;
            case "the unknown job \"cleanup\"":
                result.HasJobName.ShouldBeTrue();
                result.IsRecognized.ShouldBeFalse();
                result.JobName.ShouldBeNull();
                result.RequestedJobName.ShouldBe("cleanup");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(selection), selection, "Not a row of the outline.");
        }
    }

    #endregion
}
