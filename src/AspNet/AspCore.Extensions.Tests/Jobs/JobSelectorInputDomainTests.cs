using DKNet.AspCore.Extensions.Jobs;

namespace AspCore.Extensions.Tests.Jobs;

/// <summary>
///     The DRK-2362 §6a input-domain rows of <see cref="JobSelector.Select" /> beyond the frozen outline in
///     <see cref="JobSelectorTests" />: option shapes (D1), the registered casing and an empty known list (D2),
///     and the null guards.
/// </summary>
public class JobSelectorInputDomainTests
{
    #region Methods

    [Theory]
    [InlineData("--environment=Production")]
    [InlineData("--environment=Production --verbose=true")]
    [InlineData("--environment")]
    [InlineData("-e Production")]
    public void Select_OnlyOptions_SelectsNoJob(string arguments)
    {
        var result = JobSelector.Select(arguments.Split(' '), ["migration"]);

        result.HasJobName.ShouldBeFalse();
        result.RequestedJobName.ShouldBeNull();
        result.JobName.ShouldBeNull();
        result.IsRecognized.ShouldBeFalse();
    }

    [Theory]
    [InlineData("-e Production migration")]
    [InlineData("--x=1 migration")]
    public void Select_JobAfterOption_SelectsJob(string arguments)
    {
        var result = JobSelector.Select(arguments.Split(' '), ["migration"]);

        result.RequestedJobName.ShouldBe("migration");
        result.JobName.ShouldBe("migration");
        result.IsRecognized.ShouldBeTrue();
    }

    [Fact]
    public void Select_FirstOfTwoJobNames_SelectsFirst()
    {
        var result = JobSelector.Select(["cleanup", "migration"], ["migration", "cleanup"]);

        result.RequestedJobName.ShouldBe("cleanup");
        result.JobName.ShouldBe("cleanup");
    }

    [Fact]
    public void Select_RequestedInOtherCase_ReturnsNameAsRegistered()
    {
        var result = JobSelector.Select(["migration"], ["Migration"]);

        result.RequestedJobName.ShouldBe("migration");
        result.JobName.ShouldBe("Migration");
        result.IsRecognized.ShouldBeTrue();
        result.KnownJobNames.ShouldBe(["Migration"]);
    }

    [Fact]
    public void Select_EmptyKnownList_ReportsUnknownJob()
    {
        var result = JobSelector.Select(["migration"], []);

        result.HasJobName.ShouldBeTrue();
        result.RequestedJobName.ShouldBe("migration");
        result.JobName.ShouldBeNull();
        result.IsRecognized.ShouldBeFalse();
        result.KnownJobNames.ShouldBeEmpty();
    }

    [Fact]
    public void Select_NullArgs_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JobSelector.Select(null!, ["migration"]))
            .ParamName.ShouldBe("args");

    [Fact]
    public void Select_NullKnownJobNames_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JobSelector.Select(["migration"], null!))
            .ParamName.ShouldBe("knownJobNames");

    #endregion
}
