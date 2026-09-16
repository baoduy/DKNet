using DKNet.AspCore.Extensions.Endpoints;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Covers the by-name <see cref="CrudMapOptions.Exclude(string[])" /> overload's own state (DRK-1358 §3
///     rows 2-4), beyond the null-guard scenarios in <see cref="CrudMapOptionsTests" />: that a call actually
///     records the name, and that <see cref="CrudMapOptions.ValidateRouteNames" /> reports a name excluded by
///     name alone (never configured), not only one excluded via <c>Configure(string, ...)</c>.
/// </summary>
public class CrudMapOptionsNameExclusionTests
{
    [Fact]
    public void Exclude_ByName_ThenIsExcludedByName_RoundTrips()
    {
        var options = new CrudMapOptions();

        options.Exclude("Rename");

        options.IsExcluded("Rename").ShouldBeTrue();
        options.IsExcluded("UpdatePrice").ShouldBeFalse();
    }

    [Fact]
    public void ValidateRouteNames_WithAnExcludedUnknownName_ThrowsNamingIt()
    {
        var options = new CrudMapOptions();
        options.Exclude("Bogus");

        var exception = Should.Throw<ArgumentException>(
            () => options.ValidateRouteNames("Gadget", "GetById", "GetList", "Create", "Delete"));

        exception.Message.ShouldContain("Bogus");
    }
}
