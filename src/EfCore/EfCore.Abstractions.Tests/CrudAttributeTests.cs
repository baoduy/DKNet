using System.Reflection;

namespace EfCore.Abstractions.Tests;

public class CrudAttributeTests
{
    [Fact]
    public void CrudCreateAttribute_Usage_AllowsConstructorAndMethodOnly()
    {
        var usage = typeof(CrudCreateAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        usage.ValidOn.ShouldBe(AttributeTargets.Constructor | AttributeTargets.Method);
        usage.AllowMultiple.ShouldBeFalse();
    }

    [Fact]
    public void CrudUpdateAttribute_Usage_AllowsMethodOnly()
    {
        var usage = typeof(CrudUpdateAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        usage.ValidOn.ShouldBe(AttributeTargets.Method);
        usage.AllowMultiple.ShouldBeFalse();
    }

    [Fact]
    public void CrudActionAttribute_Usage_AllowsMethodOnly()
    {
        var usage = typeof(CrudActionAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        usage.ValidOn.ShouldBe(AttributeTargets.Method);
        usage.AllowMultiple.ShouldBeFalse();
    }

    [Fact]
    public void CrudActionAttribute_NoArguments_DefaultsToUnsetRouteAndPostVerb()
    {
        var attribute = new CrudActionAttribute();

        attribute.Route.ShouldBeNull();
        attribute.Verb.ShouldBe(CrudActionVerb.Post);
        attribute.Name.ShouldBeNull();
    }

    [Fact]
    public void CrudActionAttribute_WithRouteArgument_SetsRoute()
    {
        var attribute = new CrudActionAttribute("approval");

        attribute.Route.ShouldBe("approval");
    }

    [Fact]
    public void CrudActionAttribute_VerbSetExplicitly_RoundTrips()
    {
        var attribute = new CrudActionAttribute { Verb = CrudActionVerb.Patch };

        attribute.Verb.ShouldBe(CrudActionVerb.Patch);
    }

    [Fact]
    public void CrudActionVerb_UnderlyingValues_PostIsDefaultAndPutPatchFollow()
    {
        // CrudModelBuilder.ResolveActionHttpMethod (CrudGenerator.cs) switches on the enum's raw int value
        // (0/1/2); a reorder here would silently flip which HTTP method every existing [CrudAction(Verb: ...)]
        // resolves to.
        ((int)CrudActionVerb.Post).ShouldBe(0);
        ((int)CrudActionVerb.Put).ShouldBe(1);
        ((int)CrudActionVerb.Patch).ShouldBe(2);
    }
}
