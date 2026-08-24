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
}
