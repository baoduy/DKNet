using DKNet.AspCore.Extensions.Endpoints;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Exercises <see cref="CrudMapOptions" /> — the exclusion set a generated <c>Map{Entity}Crud</c>
///     extension consults to skip individual CRUD operations.
/// </summary>
public class CrudMapOptionsTests
{
    [Fact]
    public void IsExcluded_ByDefault_ReturnsFalseForEveryOperation()
    {
        var options = new CrudMapOptions();

        foreach (var op in Enum.GetValues<CrudOp>())
            options.IsExcluded(op).ShouldBeFalse();
    }

    [Fact]
    public void Exclude_ThenIsExcluded_RoundTripsForTheGivenOperations()
    {
        var options = new CrudMapOptions();

        options.Exclude(CrudOp.Delete, CrudOp.Create);

        options.IsExcluded(CrudOp.Delete).ShouldBeTrue();
        options.IsExcluded(CrudOp.Create).ShouldBeTrue();
        options.IsExcluded(CrudOp.GetById).ShouldBeFalse();
        options.IsExcluded(CrudOp.GetList).ShouldBeFalse();
        options.IsExcluded(CrudOp.Update).ShouldBeFalse();
    }

    [Fact]
    public void Exclude_ReturnsSameInstance_SoCallsCanChain()
    {
        var options = new CrudMapOptions();

        var returned = options.Exclude(CrudOp.Delete);

        returned.ShouldBeSameAs(options);
    }
}
