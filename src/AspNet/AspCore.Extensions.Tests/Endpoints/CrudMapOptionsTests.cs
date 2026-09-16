using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

    [Fact]
    public void Exclude_Action_ExcludesOnlyAction()
    {
        var options = new CrudMapOptions();

        options.Exclude(CrudOp.Action);

        options.IsExcluded(CrudOp.Action).ShouldBeTrue();
        options.IsExcluded(CrudOp.Update).ShouldBeFalse();
        options.IsExcluded(CrudOp.Create).ShouldBeFalse();
    }

    [Fact]
    public void CrudOp_Action_IsAppendedLast_PreservingExistingOrdinals()
    {
        // Action was appended as the LAST member specifically so every pre-existing member keeps its numeric
        // value; a consumer that persisted/serialized a CrudOp by its raw int must not silently shift meaning.
        ((int)CrudOp.GetById).ShouldBe(0);
        ((int)CrudOp.GetList).ShouldBe(1);
        ((int)CrudOp.Create).ShouldBe(2);
        ((int)CrudOp.Update).ShouldBe(3);
        ((int)CrudOp.Delete).ShouldBe(4);
        ((int)CrudOp.Action).ShouldBe(5);
    }

    private static RouteHandlerBuilder MapRoute() =>
        WebApplication.CreateBuilder().Build().MapGet("/", () => Results.Ok());

    [Fact]
    public void ValidateRouteNames_WithUnknownConfiguredName_ThrowsNamingItAndTheKnownNames()
    {
        var options = new CrudMapOptions();
        options.Configure("UpdatePrise", _ => { });

        var exception = Should.Throw<ArgumentException>(() =>
            options.ValidateRouteNames("Gadget", "GetById", "GetList", "Create", "Delete", "UpdatePrice"));

        exception.Message.ShouldBe(
            "Entity 'Gadget' has no route(s) named 'UpdatePrise'. " +
            "Known route names: 'GetById', 'GetList', 'Create', 'Delete', 'UpdatePrice'.");
    }

    [Fact]
    public void ValidateRouteNames_WithMultipleUnknownConfiguredNames_NamesAllOfThemOrdinallySorted()
    {
        // Configured in the opposite order from the expected message: the message is sorted ordinally, not
        // enumerated in Configure call order — Dictionary<string, ...> key order is not a contract (nit 2).
        var options = new CrudMapOptions();
        options.Configure("Foo", _ => { });
        options.Configure("Bar", _ => { });

        var exception = Should.Throw<ArgumentException>(() =>
            options.ValidateRouteNames("Gadget", "GetById"));

        exception.Message.ShouldBe("Entity 'Gadget' has no route(s) named 'Bar', 'Foo'. Known route names: 'GetById'.");
    }

    [Fact]
    public void ValidateRouteNames_WithOnlyKnownConfiguredNames_DoesNotThrow() =>
        Should.NotThrow(() =>
        {
            var options = new CrudMapOptions();
            options.Configure("GetById", _ => { });
            options.ValidateRouteNames("Gadget", "GetById", "GetList");
        });

    [Fact]
    public void ValidateRouteNames_IgnoresOpKindConfiguredNames()
    {
        // R4/scenario 6: the op-kind form never reaches name validation — only Configure(string, ...) does.
        var options = new CrudMapOptions();
        options.Configure(CrudOp.Delete, _ => { });

        Should.NotThrow(() => options.ValidateRouteNames("Gadget", "GetById"));
    }

    [Fact]
    public void Apply_RunsOpKindSettingsBeforeRouteNameSettings_BothForTheMatchingKeys()
    {
        var options = new CrudMapOptions();
        var order = new List<string>();
        options.Configure(CrudOp.Update, _ => order.Add("op"));
        options.Configure("UpdatePrice", _ => order.Add("route"));
        options.Configure("OtherRoute", _ => order.Add("other"));
        options.Configure(CrudOp.Delete, _ => order.Add("wrong-op"));

        options.Apply(CrudOp.Update, "UpdatePrice", MapRoute());

        order.ShouldBe(["op", "route"]);
    }

    [Fact]
    public void Apply_WithNoMatchingSettings_DoesNothing() =>
        Should.NotThrow(() => new CrudMapOptions().Apply(CrudOp.Update, "UpdatePrice", MapRoute()));

    [Fact]
    public void Configure_ByOpKind_RunsEveryRegisteredActionInCallOrder()
    {
        var options = new CrudMapOptions();
        var order = new List<int>();
        options.Configure(CrudOp.Update, _ => order.Add(1));
        options.Configure(CrudOp.Update, _ => order.Add(2));

        options.Apply(CrudOp.Update, "AnyRoute", MapRoute());

        order.ShouldBe([1, 2]);
    }

    [Fact]
    public void Configure_ByRouteName_RunsEveryRegisteredActionInCallOrder()
    {
        var options = new CrudMapOptions();
        var order = new List<int>();
        options.Configure("Rename", _ => order.Add(1));
        options.Configure("Rename", _ => order.Add(2));

        options.Apply(CrudOp.Update, "Rename", MapRoute());

        order.ShouldBe([1, 2]);
    }

    [Fact]
    public void Configure_ReturnsSameInstance_SoCallsCanChain()
    {
        var options = new CrudMapOptions();

        options.Configure(CrudOp.Update, _ => { })
            .Configure("Rename", _ => { })
            .ShouldBeSameAs(options);
    }

    [Fact]
    public void Configure_ByOpKind_WithNullAction_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new CrudMapOptions().Configure(CrudOp.Update, null!));

    [Fact]
    public void Configure_ByRouteName_WithNullAction_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new CrudMapOptions().Configure("Rename", null!));

    [Fact]
    public void Apply_WithNullBuilder_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new CrudMapOptions().Apply(CrudOp.Update, "Rename", null!));

    // DRK-1355 §5: per-member exclusion of generated update and action routes. §3 row 2's own proof column
    // names "a null-name unit test" as what a missing null guard on Exclude(string[]) turns red.

    [Fact]
    public void IsExcluded_ByName_ByDefault_ReturnsFalse() =>
        // R6: nothing is excluded by default — pinned at the name-based overload independently of the
        // pre-existing CrudOp-kind overload's own default test above.
        new CrudMapOptions().IsExcluded("Rename").ShouldBeFalse();

    [Fact]
    public void Exclude_ByName_WithNullArray_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new CrudMapOptions().Exclude((string[])null!));

    [Fact]
    public void Exclude_ByName_WithNullElement_ThrowsArgumentNullException() =>
        // A null name matches no route and would silently leave an endpoint reachable (§3 row 2) — refused
        // rather than silently ignored.
        Should.Throw<ArgumentNullException>(() => new CrudMapOptions().Exclude("Rename", null!));
}
