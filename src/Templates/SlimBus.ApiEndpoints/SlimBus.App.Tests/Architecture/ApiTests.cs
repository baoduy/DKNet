using System.Diagnostics.CodeAnalysis;
using NetArchTest.Rules;

namespace SlimBus.App.Tests.Architecture;

public class ApiTests
{
    #region Methods

    [Fact]
    public void AllApiClassesShouldBeInternal()
    {
        // Adjust the assembly name if needed
        var types = Types.InAssembly(typeof(Api.Program).Assembly);

        var result = types
            .That()
            .AreClasses()
            .And().DoNotHaveName(nameof(Api.Program))
            .Should()
            .NotBePublic()
            .GetResult();

        result.ShouldNotBeNull("There is No classes found.");
        result.IsSuccessful.ShouldBeTrue(
            $"These classes should be internal: {string.Join(", ", (result.FailingTypes ?? []).Select(t => t.FullName))}");
    }

    [Fact]
    public void AllConfigsClassesShouldBeStaticAndExcludedFromCodeCoverage()
    {
        // Adjust the assembly name if needed
        var types = Types.InAssembly(typeof(Api.Program).Assembly);

        var result = types
            .That()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Config", StringComparison.OrdinalIgnoreCase)
            .Should()
            .BeStatic()
            .And().HaveCustomAttribute(typeof(ExcludeFromCodeCoverageAttribute))
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"These classes should be static and excluded from code coverage: {string.Join(", ", (result.FailingTypes ?? []).Select(t => t.FullName))}");
    }

    [Fact]
    public void AllEndPointClassesShouldBeInternalAndSealed()
    {
        // Adjust the assembly name if needed
        var types = Types.InAssembly(typeof(Api.Program).Assembly);

        var result = types
            .That()
            .AreClasses()
            .And()
            .HaveNameEndingWith("Endpoint", StringComparison.OrdinalIgnoreCase)
            .Should().NotBePublic()
            .And().BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"These classes should be sealed and internal: {string.Join(", ", (result.FailingTypes ?? []).Select(t => t.FullName))}");
    }

    #endregion
}